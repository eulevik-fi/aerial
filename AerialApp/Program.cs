using System;
using System.Collections.Generic;
using System.Windows.Forms;
using LibVLCSharp.WinForms;

namespace Aerial;

/// <summary>
/// Standalone app variant: shows the same full-screen screensaver surface on
/// every attached display, streaming an aerial video on each one via LibVLC.
/// </summary>
internal static class AerialApp
{
    // no worries about certificates:
    static String lorm = "https://lorem.video/720p";
    // plays OK, 1080p SDR:
    static String works = "http://a1.phobos.apple.com/us/r1000/000/Features/atv/AutumnResources/videos/b9-3.mov";
    // plays too slowly, not moving:
    static String gg = "https://sylvan.apple.com/itunes-assets/Aerials116/v4/cb/5b/50/cb5b5035-6701-619f-9065-3d7d0e5fbef4/GG_A_SUNSET_MarshallsBeach_c28_v5_Final01_HFR_HEVC.mov";

    private static readonly Uri VideoUrl =
        new(works);

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();

        VideoPlayer.Log("=== AerialApp starting ===");
        VideoPlayer.Log($"Video URL: {VideoUrl}");

        // Initialize the LibVLC core once for the whole process.
        VideoPlayer.InitializeCore();

        // Fetch (or refresh) the video catalog before showing anything.
        Videos.InitializeAsync().GetAwaiter().GetResult();
        VideoPlayer.Log($"Catalog loaded: {Videos.Catalog?.Assets.Count ?? 0} assets, version={Videos.Catalog?.Version ?? "<null>"}");

        using var idleTracker = new IdleExitTracker();

        var forms = new List<ScreensaverForm>();
        var players = new List<VideoPlayer>();

        foreach (Screen screen in Screen.AllScreens)
        {
            var form = new ScreensaverForm(screen);

            // A VideoView control fills the form; LibVLC renders straight onto
            // its native window handle, so it works at any DPI/zoom ratio.
            var videoView = new VideoView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
            };
            form.Controls.Add(videoView);

            var player = new VideoPlayer(videoView, $"screen{forms.Count}");
            players.Add(player);

            // Attach and start playback only once the form is visible, so the
            // video view's final window handle is used.
            form.Shown += (_, _) =>
            {
                VideoPlayer.Log($"[screen{players.IndexOf(player)}] form shown, attaching");
                player.Attach();
                player.Play(VideoUrl);
            };

            forms.Add(form);
        }

        foreach (var form in forms)
        {
            form.Show();
        }

        idleTracker.Start();

        Application.Run();

        foreach (var player in players)
        {
            player.Dispose();
        }

        foreach (var form in forms)
        {
            form.Dispose();
        }

        return 0;
    }
}
