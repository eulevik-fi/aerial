using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Aerial;

/// <summary>
/// Full-screen red form shown on a single display. One instance is created
/// per attached monitor so multi-monitor setups are covered.
/// </summary>
internal sealed class ScreensaverForm : Form
{
    private readonly Screen _screen;
    private bool _closed;

    public ScreensaverForm(Screen screen)
    {
        _screen = screen;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Red;
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
        CloseAll();
        base.OnKeyDown(e);
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
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private readonly IntPtr _parentHwnd;

    public PreviewForm(IntPtr parentHwnd)
    {
        _parentHwnd = parentHwnd;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        BackColor = Color.Red;
        StartPosition = FormStartPosition.Manual;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        SetParent(Handle, _parentHwnd);

        if (GetClientRect(_parentHwnd, out RECT rc))
        {
            // The preview rect is small and lives in a single-DPI dialog, so
            // plain pixel math is fine here.
            Location = new Point(rc.Left, rc.Top);
            Size = new Size(rc.Right - rc.Left, rc.Bottom - rc.Top);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var brush = new SolidBrush(Color.Red);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }
}
