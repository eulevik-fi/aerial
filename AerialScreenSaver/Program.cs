using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using LibVLCSharp.WinForms;

namespace Aerial;

internal static class Program
{
    internal const string PreviewExitEventName = "Local\\Aerial-Screensaver-Preview-Exit";

    private static Uri? SelectNextVideo(
        IReadOnlyList<Uri> videoUrls,
        Uri current,
        HashSet<Uri> activeVideos)
    {
        var availableVideos = videoUrls
            .Where(video => video != current &&
                            !activeVideos.Contains(video) &&
                            !Videos.IsInMru(video))
            .ToArray();

        return availableVideos.Length == 0
            ? null
            : availableVideos[Random.Shared.Next(availableVideos.Length)];
    }

    // Prevents multiple instances of the screensaver from running at once,
    // which can happen when Windows launches it on several monitors.
    private static Mutex? _mutex;

    [STAThread]
    private static int Main(string[] args)
    {
        _mutex = new Mutex(true, "Local\\Aerial-Screensaver-Mutex", out bool firstInstance);

        ApplicationConfiguration.Initialize();

        string arg = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;

        try
        {
            switch (arg)
            {
                case "/s":
                case "-s":
                    if (!firstInstance)
                        return 0;
                    RunFullScreen();
                    return 0;

                case "/c":
                case "-c":
                    SignalPreviewExit();
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
                        return 0;
                    }
                    return 0;
            }
        }
        finally
        {
            _mutex.Dispose();
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

    /// <summary>/s - run one full-screen form per attached display.</summary>
    private static void RunFullScreen()
    {
        VideoPlayer.TruncateLog();

        // Initialize LibVLC and load the shared URL collection.
        VideoPlayer.InitializeCore();
        // Fetch (or refresh) the video catalog before showing anything.
        Videos.InitializeAsync().GetAwaiter().GetResult();

        Uri[] videoUrls = Videos.UrlValues
            .Select(url => Uri.TryCreate(url, UriKind.Absolute, out Uri? videoUrl) ? videoUrl : null)
            .Where(videoUrl => videoUrl is not null)
            .Select(videoUrl => videoUrl!)
            .Distinct()
            .ToArray();

        var initialVideos = videoUrls
            .Where(video => !Videos.IsInMru(video))
            .ToList();

        if (initialVideos.Count == 0)
            return;

        for (int index = initialVideos.Count - 1; index > 0; index--)
        {
            int swapIndex = Random.Shared.Next(index + 1);
            (initialVideos[index], initialVideos[swapIndex]) =
                (initialVideos[swapIndex], initialVideos[index]);
        }

        using var idleTracker = new IdleExitTracker();

        var forms = new List<ScreensaverForm>();
        var players = new List<VideoPlayer>();
        var activeVideos = new HashSet<Uri>();
        var videoGate = new object();

        foreach (Screen screen in Screen.AllScreens)
        {
            var form = new ScreensaverForm(screen);
            var videoView = new VideoView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
            };
            form.Controls.Add(videoView);

            var player = new VideoPlayer(videoView, $"screen{forms.Count}");
            players.Add(player);
            Uri currentVideo;
            lock (videoGate)
            {
                currentVideo = initialVideos[forms.Count % initialVideos.Count];
                activeVideos.Add(currentVideo);
            }

            player.Ended += () =>
            {
                Uri? nextVideo;
                lock (videoGate)
                {
                    activeVideos.Remove(currentVideo);
                    nextVideo = SelectNextVideo(videoUrls, currentVideo, activeVideos);
                    if (nextVideo is not null)
                    {
                        currentVideo = nextVideo;
                        activeVideos.Add(currentVideo);
                    }
                }

                if (nextVideo is null)
                    return;

                Videos.RecordPlayed(nextVideo);
                player.Play(nextVideo);
            };

            form.Shown += (_, _) =>
            {
                player.Attach();
                Videos.RecordPlayed(currentVideo);
                player.Play(currentVideo);
            };
            forms.Add(form);
        }

        // Show all forms before entering the message loop so every display
        // paints its red surface simultaneously.
        foreach (var form in forms)
        {
            form.Show();
        }

        idleTracker.Start();

        Application.Run();

        foreach (var player in players)
            player.Dispose();

        foreach (var form in forms)
        {
            form.Dispose();
        }
    }

    /// <summary>/p &lt;hwnd&gt; - render inside the little preview window of the
    /// Windows screensaver settings dialog.</summary>
    private static void ShowPreview(IntPtr parentHwnd)
    {
        if (parentHwnd == IntPtr.Zero)
            return;

        using var preview = new PreviewForm(parentHwnd);
        Application.Run(preview);
    }
}
