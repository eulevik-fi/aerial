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
    private static readonly Uri VideoUrl =
        //new("https://sylvan.apple.com/Aerials/2x/Videos/LA_A006_C008_2K_SDR_HEVC.mov");
    new("https://lorem.video/720p");

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
                BackColor = Color.Red,
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
