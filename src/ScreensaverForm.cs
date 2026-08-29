using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Aerial;

/// <summary>
/// Full-screen black form shown on a single display. One instance is created
/// per attached monitor so multi-monitor setups are covered.
/// </summary>
internal sealed class ScreensaverForm : Form
{
    private readonly Screen _screen;
    private bool _closed;
    private bool _shiftKeyDown;
    private VideoPlayer? _videoPlayer;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public VideoPlayer? VideoPlayer
    {
        get => _videoPlayer;
        set => _videoPlayer = value;
    }

    public ScreensaverForm(Screen screen)
    {
        _screen = screen;
        _shiftKeyDown = false;

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
        Bounds = screen.Bounds;

        Cursor.Hide();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
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

        switch (m.Msg)
        {
            case WM_SYSCOMMAND:
                int cmd = m.WParam.ToInt32() & 0xFFF0;
                if (cmd == SC_MOVE || cmd == SC_MONITORPOWER)
                    return; // swallow so the user can't drag or blank us
                break;
        }

        base.WndProc(ref m);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsShiftKey(e))
        {
            if (!_shiftKeyDown)
            {
                _shiftKeyDown = true;
                VideoPlayer.ToggleSubtitles();
            }
            return;
        }

        CloseAll();
        base.OnKeyDown(e);
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        if (IsShiftKey(e))
        {
            if (_shiftKeyDown)
            {
                _shiftKeyDown = false;
            }
            return;
        }

        base.OnKeyUp(e);
    }

    private bool IsShiftKey(KeyEventArgs e)
    {
        return e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.LShiftKey || e.KeyCode == Keys.RShiftKey;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        CloseAll();
        base.OnMouseDown(e);
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

/// <summary>
/// Renders inside the small preview rectangle of the Windows screensaver
/// settings dialog (/p &lt;hwnd&gt;). The parent window handle is supplied by
/// Windows itself.
/// </summary>
internal sealed class PreviewForm : Form
{
    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private readonly IntPtr _parentHwnd;
    private readonly EventWaitHandle _exitSignal;
    private readonly System.Windows.Forms.Timer _parentMonitor;

    public PreviewForm(IntPtr parentHwnd)
    {
        _parentHwnd = parentHwnd;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        BackColor = Color.Black;
        StartPosition = FormStartPosition.Manual;

        _exitSignal = new EventWaitHandle(
            false,
            EventResetMode.ManualReset,
            Program.PreviewExitEventName);

        _parentMonitor = new System.Windows.Forms.Timer { Interval = 500 };
        _parentMonitor.Tick += (_, _) =>
        {
            if (_exitSignal.WaitOne(0) ||
                !IsWindow(_parentHwnd) ||
                !IsWindowVisible(_parentHwnd))
                Close();
        };
        _parentMonitor.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _parentMonitor.Dispose();
            _exitSignal.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.Style = (cp.Style & ~0x00C00000) | 0x40000000; // WS_CHILD
            cp.Parent = _parentHwnd;
            cp.ExStyle &= ~0x00040000; // WS_EX_APPWINDOW
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        if (GetClientRect(_parentHwnd, out RECT rc))
        {
            // Child-window coordinates start at the parent's client origin.
            Location = new Point(0, 0);
            Size = new Size(rc.Right - rc.Left, rc.Bottom - rc.Top);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var brush = new SolidBrush(Color.Black);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}
