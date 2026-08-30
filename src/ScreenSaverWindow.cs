using System;
using System.Windows.Forms;

namespace Aerial;

/// <summary>
/// Full-screen black form shown on a single display. One instance is created
/// per attached monitor so multi-monitor setups are covered.
/// </summary>
internal sealed class ScreenSaverWindow : Form
{
    private readonly MonitorInfo _monitorInfo;
    private bool _closed;
    private VideoPlayer? _videoPlayer;
    private DateTime _showTime;
    private KeyHandler? _keyHandler;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public VideoPlayer? VideoPlayer
    {
        get => _videoPlayer;
        set => _videoPlayer = value;
    }

    public ScreenSaverWindow(MonitorInfo monitorInfo)
    {
        _monitorInfo = monitorInfo ?? throw new ArgumentNullException(nameof(monitorInfo));

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Black;
        DoubleBuffered = true;
        KeyPreview = true;

        // Position on the target display using its bounds. This works for
        // any DPI / zoom ratio because Screen.Bounds is already expressed in
        // the coordinate space of this process (per-monitor DPI aware).
        Bounds = _monitorInfo.Screen.Bounds;

        Cursor.Hide();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _showTime = DateTime.Now;
        _keyHandler = new KeyHandler(() => VideoPlayer.ToggleSubtitles());
        Capture = true;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x80; // WS_EX_TOOLWINDOW - keep out of alt-tab
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_SYSCOMMAND = 0x0112;
        const int SC_MOVE = 0xF010;
        const int SC_MONITORPOWER = 0xF170;
        const int WM_DISPLAYCHANGE = 0x007E;

        switch (m.Msg)
        {
            case WM_SYSCOMMAND:
                int cmd = m.WParam.ToInt32() & 0xFFF0;
                if (cmd == SC_MOVE || cmd == SC_MONITORPOWER)
                    return; // swallow so the user can't drag or blank us
                break;

            case WM_DISPLAYCHANGE:
                // Display configuration has changed; reinitialize the window
                ReinitializeForDisplayChange();
                break;
        }

        base.WndProc(ref m);
    }

    private void ReinitializeForDisplayChange()
    {
        try
        {
            // Rediscover monitors to get updated bounds
            MonitorInfo.Discover();

            // Update this window's bounds to the current monitor's bounds
            if (_monitorInfo.Screen is not null)
            {
                Bounds = _monitorInfo.Screen.Bounds;
            }

            // Reset the grace period timer so inputs are ignored for 1 second after display change
            _showTime = DateTime.Now;

            // Reattach video player to the new window handle in case DPI or rendering context changed
            if (_videoPlayer is not null)
            {
                _videoPlayer.Attach();
            }
        }
        catch (Exception ex)
        {
            // Log but don't crash on display change errors
            Logging.Log($"Error reinitializing after display change: {ex.Message}");
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        try
        {
            // Let KeyHandler process shift and control keys
            _keyHandler?.HandleKeyDown(e);
            if (e.Handled)
                return;

            // Ignore keyboard input for the first second to prevent accidental exit
            if ((DateTime.Now - _showTime).TotalSeconds < 1)
                return;

            CloseAll();
            base.OnKeyDown(e);
        }
        catch (Exception ex)
        {
            Logging.Log($"[OnKeyDown Error] {ex.GetType().Name}: {ex.Message}");
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        try
        {
            _keyHandler?.HandleKeyUp(e);
            if (e.Handled)
                return;

            base.OnKeyUp(e);
        }
        catch (Exception ex)
        {
            Logging.Log($"[OnKeyUp Error] {ex.GetType().Name}: {ex.Message}");
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        try
        {
            // Ignore mouse input for the first second to prevent accidental exit
            if ((DateTime.Now - _showTime).TotalSeconds < 1)
                return;

            CloseAll();
            base.OnMouseDown(e);
        }
        catch (Exception ex)
        {
            Logging.Log($"[OnMouseDown Error] {ex.GetType().Name}: {ex.Message}");
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        // Only react to real movement once the mouse has entered the form,
        // otherwise the initial placement triggers an immediate exit.
        if (!_closed && ClientRectangle.Contains(PointToClient(Cursor.Position)))
        {
            // Handled by IdleExitTracker for cross-display accuracy.
        }
        base.OnMouseMove(e);
    }

    private void CloseAll()
    {
        if (_closed)
            return;
        _closed = true;
        IdleExitTracker.Exit();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Cursor.Show();
        base.OnFormClosed(e);
    }
}
