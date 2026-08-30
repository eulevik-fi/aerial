using System;
using System.Windows.Forms;

namespace Aerial;

/// <summary>
/// Handles keyboard input for special keys (Shift) in the screensaver.
/// </summary>
internal sealed class KeyHandler
{
    private bool _shiftKeyDown;
    private readonly Action _onShiftKeyDown;

    public KeyHandler(Action onShiftToggle)
    {
        _onShiftKeyDown = onShiftToggle ?? throw new ArgumentNullException(nameof(onShiftToggle));
    }

    public void HandleKeyDown(KeyEventArgs e)
    {
        if (IsShiftKey(e))
        {
            if (!_shiftKeyDown)
            {
                _shiftKeyDown = true;
                _onShiftKeyDown?.Invoke();
            }
            e.Handled = true;
            return;
        }
    }

    public void HandleKeyUp(KeyEventArgs e)
    {
        if (IsShiftKey(e))
        {
            if (_shiftKeyDown)
            {
                _shiftKeyDown = false;
            }
            e.Handled = true;
            return;
        }
    }

    private static bool IsShiftKey(KeyEventArgs e)
    {
        return e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.LShiftKey || e.KeyCode == Keys.RShiftKey;
    }
}
