using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Aerial;

/// <summary>
/// Describes a monitor discovered at startup with its generated name and pixel resolution.
/// </summary>
internal sealed class MonitorInfo
{
    private static List<MonitorInfo>? _all;

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public Screen Screen { get; }

    public MonitorInfo(string name, Screen screen)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Monitor name cannot be null or empty.", nameof(name));
        if (screen is null)
            throw new ArgumentNullException(nameof(screen));

        Rectangle bounds = screen.Bounds;
        if (bounds.Width <= 0)
            throw new ArgumentOutOfRangeException(nameof(screen), "Monitor width must be greater than zero.");
        if (bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(screen), "Monitor height must be greater than zero.");

        Name = name;
        Width = bounds.Width;
        Height = bounds.Height;
        Screen = screen;
    }

    /// <summary>
    /// Lazily initialized list of all monitors discovered at startup.
    /// Call Discover() to force rediscovery after hardware changes.
    /// </summary>
    public static IReadOnlyList<MonitorInfo> All => _all ??= DiscoverMonitors();

    public bool IsLargeDisplay => Width > 1920 || Height > 1080;

    /// <summary>Force rediscovery of monitors (e.g., after display configuration changes).</summary>
    public static IReadOnlyList<MonitorInfo> Discover()
    {
        _all = DiscoverMonitors();
        return _all;
    }

    private static List<MonitorInfo> DiscoverMonitors()
    {
        List<MonitorInfo> discovered = [];
        for (int index = 0; index < Screen.AllScreens.Length; index++)
        {
            Screen screen = Screen.AllScreens[index];
            discovered.Add(new MonitorInfo($"screen{index}", screen));
        }

        return discovered;
    }
}
