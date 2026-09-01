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
            // IVideoCatalog catalog = new VideoCatalog_tvOS10();
            IVideoCatalog catalog = new VideoCatalog_tvOS26();
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
