using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using LibVLCSharp.WinForms;

namespace Aerial;

internal static class Program
{
    internal const string PreviewExitEventName = "Local\\Aerial-Screensaver-Preview-Exit";

    [STAThread]
    private static int Main(string[] args)
    {
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
                    nextVideo = Videos.SelectNextVideo(videoUrls, currentVideo, activeVideos);
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

        VideoPlayer.InitializeCore();
        Videos.InitializeAsync().GetAwaiter().GetResult();

        Uri[] videoUrls = Videos.UrlValues
            .Select(url => Uri.TryCreate(url, UriKind.Absolute, out Uri? videoUrl) ? videoUrl : null)
            .Where(videoUrl => videoUrl is not null)
            .Select(videoUrl => videoUrl!)
            .Distinct()
            .ToArray();

        var availableVideos = videoUrls
            .Where(video => !Videos.IsInMru(video))
            .ToArray();
        if (availableVideos.Length == 0)
            return;

        var videoView = new VideoView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
        };

        using var preview = new PreviewForm(parentHwnd);
        preview.Controls.Add(videoView);
        using var player = new VideoPlayer(videoView, "preview");
        Uri video = availableVideos[Random.Shared.Next(availableVideos.Length)];
        preview.Shown += (_, _) =>
        {
            player.Attach();
            Videos.RecordPlayed(video);
            player.Play(video);
        };
        Application.Run(preview);
    }
}
