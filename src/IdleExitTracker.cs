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
    private DateTime _startTime;

    public void Start()
    {
        _startTime = DateTime.Now;
        _lastMousePos = Cursor.Position;
        _timer = new System.Windows.Forms.Timer { Interval = 100 };
        _timer.Tick += (_, _) =>
        {
            try
            {
                // Ignore mouse movement for the first second to prevent accidental exit
                if ((DateTime.Now - _startTime).TotalSeconds < 1)
                {
                    _lastMousePos = Cursor.Position;
                    return;
                }

                var pos = Cursor.Position;
                if (pos != _lastMousePos)
                {
                    Exit();
                    return;
                }
                _lastMousePos = pos;
            }
            catch (Exception ex)
            {
                Logging.Log($"[IdleExitTracker.Tick Error] {ex.GetType().Name}: {ex.Message}");
            }
        };
        _timer.Start();
    }

    public static void Exit()
    {
        Application.Exit();
    }

    public void Dispose() => _timer?.Dispose();
}
