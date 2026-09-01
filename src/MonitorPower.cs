using System;
using System.Runtime.InteropServices;

namespace Aerial;

public static class MonitorPower
{
    private const int WM_SYSCOMMAND = 0x0112;
    private const int SC_MONITORPOWER = 0xF170;

    // Power state definitions: -1 = ON, 1 = Low Power, 2 = OFF
    private const int MONITOR_OFF = 2;

    // Target all connected physical display monitors
    private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static void TurnOffMonitors()
    {
        SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)MONITOR_OFF);
    }
}
