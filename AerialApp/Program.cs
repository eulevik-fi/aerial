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
        try
        {
            ApplicationConfiguration.Initialize();
            Application.ThreadException += (sender, e) =>
            {
                Logging.Log($"[ThreadException] {e.Exception?.GetType().Name}: {e.Exception?.Message}");
                Logging.Log($"StackTrace: {e.Exception?.StackTrace}");
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Logging.Log($"[UnhandledException] {ex?.GetType().Name}: {ex?.Message}");
                Logging.Log($"StackTrace: {ex?.StackTrace}");
            };

            Logging.PrepareLog();
            Logging.Log("=== AerialApp starting ===");

            MonitorInfo.Discover();

            VideoPlayer.InitializeCore();
            VideoController.InitializeAsync().GetAwaiter().GetResult();
            Logging.Log($"Catalog URL: {CatalogUrl}");
            var catalog = new VideoCatalog(CatalogUrl);
            catalog.InitializeAsync().GetAwaiter().GetResult();
            Logging.Log($"Catalog loaded: {catalog.Videos.Count} assets");

            using var idleTracker = new IdleExitTracker();
            var queue = new VideoQueue(catalog.Videos);
            if (!queue.Start())
                return 0;

            idleTracker.Start();
            Application.Run();
            queue.Dispose();

            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                Logging.Log($"[FATAL] {ex.GetType().Name}: {ex.Message}");
                Logging.Log($"StackTrace: {ex.StackTrace}");
            }
            catch
            {
                // If logging fails, silently continue
            }
            return 1;
        }
    }
}
