using System;
using System.Windows.Forms;
using LibVLCSharp.WinForms;

namespace Aerial;

internal static class Program
{
    internal const string PreviewExitEventName = "Local\\Aerial-Screensaver-Preview-Exit";
}

/// <summary>
/// Standalone app variant: shows the same full-screen screensaver surface on
/// every attached display, streaming an aerial video on each one via LibVLC.
/// </summary>
internal static class AerialApp
{
    // private const string CatalogUrl = "http://a1.phobos.apple.com/us/r1000/000/Features/atv/AutumnResources/videos/entries.json";
    private const string CatalogUrl = "https://sylvan.apple.com/itunes-assets/Aerials126/v4/c0/45/d9/c045d9d0-9606-1535-62fe-189edb4f79eb/resources-atv-23J-2.tar";
    
    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();

        VideoPlayer.PrepareLog();
        VideoPlayer.Log("=== AerialApp starting ===");

        VideoPlayer.InitializeCore();
        Videos.InitializeAsync().GetAwaiter().GetResult();
        VideoPlayer.Log($"Catalog URL: {CatalogUrl}");
        var catalog = new Catalog(CatalogUrl);
        catalog.InitializeAsync().GetAwaiter().GetResult();
        VideoPlayer.Log($"Catalog loaded: {catalog.UrlValues.Count} assets");

        Uri[] videoUrls = catalog.UrlValues
            .Select(url => Uri.TryCreate(url, UriKind.Absolute, out Uri? videoUrl) ? videoUrl : null)
            .Where(videoUrl => videoUrl is not null)
            .Select(videoUrl => videoUrl!)
            .Distinct()
            .ToArray();

        var initialVideos = videoUrls
            .Where(video => !Videos.IsInMru(video))
            .ToList();

        if (initialVideos.Count == 0)
            return 0;

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
            int nextVideoQueued = 0;
            Uri currentVideo;
            lock (videoGate)
            {
                currentVideo = initialVideos[forms.Count % initialVideos.Count];
                activeVideos.Add(currentVideo);
            }

            void PlayNextVideo()
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
            }

            void QueueNextVideo()
            {
                if (Interlocked.Exchange(ref nextVideoQueued, 1) != 0 ||
                    form.IsDisposed)
                    return;

                form.BeginInvoke((Action)(() =>
                {
                    Interlocked.Exchange(ref nextVideoQueued, 0);
                    PlayNextVideo();
                }));
            }

            player.Ended += QueueNextVideo;
            player.Failed += QueueNextVideo;

            form.Shown += (_, _) =>
            {
                player.Attach();
                Videos.RecordPlayed(currentVideo);
                player.Play(currentVideo);
            };

            forms.Add(form);
        }

        foreach (var form in forms)
            form.Show();

        idleTracker.Start();
        Application.Run();

        foreach (var player in players)
            player.BeginShutdown();

        foreach (var player in players)
            player.Dispose();
        foreach (var form in forms)
            form.Dispose();

        return 0;
    }
}
