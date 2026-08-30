using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Aerial;

/// <summary>
/// Describes a monitor discovered at startup with its generated name and pixel resolution.
/// </summary>
internal sealed class Monitor
{
    private static readonly List<Monitor> _all = DiscoverMonitors();

    public string Name { get; }
    public int Width { get; }
    public int Height { get; }
    public Screen Screen { get; }

    public Monitor(string name, Screen screen)
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

    public static IReadOnlyList<Monitor> All => _all;

    public bool IsLargeDisplay => Width > 1920 || Height > 1080;

    public static IReadOnlyList<Monitor> Discover()
    {
        var discovered = DiscoverMonitors();
        _all.Clear();
        _all.AddRange(discovered);
        return _all;
    }

    private static List<Monitor> DiscoverMonitors()
    {
        List<Monitor> discovered = [];
        for (int index = 0; index < Screen.AllScreens.Length; index++)
        {
            Screen screen = Screen.AllScreens[index];
            discovered.Add(new Monitor($"screen{index}", screen));
        }

        return discovered;
    }
}
