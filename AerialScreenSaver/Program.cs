using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using LibVLCSharp.WinForms;

namespace Aerial;

internal static class Program
{
    // private const string CatalogUrl = "http://a1.phobos.apple.com/us/r1000/000/Features/atv/AutumnResources/videos/entries.json";
    private const string CatalogUrl = "https://sylvan.apple.com/itunes-assets/Aerials126/v4/c0/45/d9/c045d9d0-9606-1535-62fe-189edb4f79eb/resources-atv-23J-2.tar";

    internal const string PreviewExitEventName = "Local\\Aerial-Screensaver-Preview-Exit";

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [STAThread]
    private static int Main(string[] args)
    {
        VideoPlayer.PrepareLog();
        VideoPlayer.Log($"AerialScreenSaver starting. Args: [{string.Join(", ", args.Select(arg => $"\"{arg}\""))}]");

        ApplicationConfiguration.Initialize();

        string arg = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;

        switch (arg)
        {
            case "/s":
            case "-s":
                RunFullScreen();
                return 0;

            case "/c":
            case "-c":
                SignalPreviewExit();
                ShowOptionsMessage();
                return 0;

            case "/p":
            case "-p":
                if (args.Length >= 2 && long.TryParse(args[1], out long hwndValue))
                {
                    ShowPreview(new IntPtr(hwndValue));
                }
                return 0;

            default:
                if (arg.StartsWith("/c:", StringComparison.OrdinalIgnoreCase) ||
                    arg.StartsWith("-c:", StringComparison.OrdinalIgnoreCase))
                {
                    SignalPreviewExit();
                    ShowOptionsMessage();
                    return 0;
                }
                return 0;
            }
    }

    private static void SignalPreviewExit()
    {
        try
        {
            if (EventWaitHandle.TryOpenExisting(PreviewExitEventName, out EventWaitHandle? signal))
                signal!.Set();
            signal?.Dispose();
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void ShowOptionsMessage()
    {
        MessageBox.Show(
            "This screen saver has no options that you can set.",
            "Aerial",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    /// <summary>/s - run one full-screen form per attached display.</summary>
    private static void RunFullScreen()
    {
        // Initialize LibVLC and load the shared URL collection.
        VideoPlayer.InitializeCore();
        // Fetch (or refresh) the video catalog before showing anything.
        Videos.InitializeAsync().GetAwaiter().GetResult();
        var catalog = new Catalog(CatalogUrl);
        catalog.InitializeAsync().GetAwaiter().GetResult();

        using var idleTracker = new IdleExitTracker();
        var queue = new VideoQueue(catalog.Videos);
        if (!queue.Start())
            return;

        idleTracker.Start();

        Application.Run();
        queue.Dispose();
    }

    /// <summary>/p &lt;hwnd&gt; - render inside the little preview window of the
    /// Windows screensaver settings dialog.</summary>
    private static void ShowPreview(IntPtr parentHwnd)
    {
        if (parentHwnd == IntPtr.Zero)
            return;

        VideoPlayer.InitializeCore();
        Videos.InitializeAsync().GetAwaiter().GetResult();
        var catalog = new Catalog(CatalogUrl);
        catalog.InitializeAsync().GetAwaiter().GetResult();

        var availableVideos = catalog.Videos
            .Where(video => !Videos.IsInMru(video))
            .ToArray();
        if (availableVideos.Length == 0)
            return;

        using var exitSignal = new EventWaitHandle(
            false,
            EventResetMode.ManualReset,
            PreviewExitEventName);
        using var monitor = new System.Windows.Forms.Timer { Interval = 500 };
        using var player = new VideoPlayer(parentHwnd, "preview");
        Video video = availableVideos[Random.Shared.Next(availableVideos.Length)];
        monitor.Tick += (_, _) =>
        {
            if (exitSignal.WaitOne(0) ||
                !IsWindow(parentHwnd) ||
                !IsWindowVisible(parentHwnd))
                Application.ExitThread();
        };
        player.Attach(parentHwnd);
        Videos.RecordPlayed(video);
        player.Play(video);
        monitor.Start();
        Application.Run();
    }
}
