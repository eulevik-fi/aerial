using System;
using System.Windows.Forms;

namespace Aerial;

/// <summary>
/// Exits the screensaver when any mouse movement or key press occurs.
/// Mouse position changes are tracked manually because WM_MOUSEMOVE is not
/// delivered reliably to borderless top-most forms on every monitor.
/// </summary>
internal sealed class IdleExitTracker : IDisposable
{
    private Point _lastMousePos;
    private System.Windows.Forms.Timer? _timer;

    public void Start()
    {
        _lastMousePos = Cursor.Position;
        _timer = new System.Windows.Forms.Timer { Interval = 100 };
        _timer.Tick += (_, _) =>
        {
            var pos = Cursor.Position;
            if (pos != _lastMousePos)
            {
                Exit();
                return;
            }
            _lastMousePos = pos;
        };
        _timer.Start();
    }

    public static void Exit()
    {
        Application.Exit();
    }

    public void Dispose() => _timer?.Dispose();
}
