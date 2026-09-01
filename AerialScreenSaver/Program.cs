using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using LibVLCSharp.WinForms;

namespace Aerial;

internal static class Program
{
    internal const string PreviewExitEventName = "Local\\Aerial-Screensaver-Preview-Exit";
    private const string MutexName = "Local\\Aerial-Screensaver-Instance";
    
    private static Mutex? _instanceMutex;

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    private static extern uint SetErrorMode(uint uMode);
    
    private const uint SEM_FAILCRITICALERRORS = 0x0001;
    private const uint SEM_NOGPFAULTERRORBOX = 0x0002;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            // Suppress Windows error reporting
            SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX);

            Logging.PrepareLog();
            Logging.Log($"=== AerialScreenSaver starting. Args: [{string.Join(", ", args.Select(arg => $"\"{arg}\""))}] ===");

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

            ApplicationConfiguration.Initialize();
            return HandleCommand(args);
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

    private static int HandleCommand(string[] args)
    {
        string arg = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;

        switch (arg)
        {
            case "/s":
            case "-s":
                return RunFullScreenMode();

            case "/c":
            case "-c":
                return RunConfigMode();

            case "/p":
            case "-p":
                return RunPreviewMode(args);

            default:
                return IsConfigArgument(arg) ? RunConfigMode() : 0;
        }
    }

    private static bool IsConfigArgument(string arg)
    {
        return arg.StartsWith("/c:", StringComparison.OrdinalIgnoreCase) ||
               arg.StartsWith("-c:", StringComparison.OrdinalIgnoreCase);
    }

    private static int RunFullScreenMode()
    {
        if (!TryAcquireInstanceMutex())
        {
            Logging.Log("Another instance of the screen saver is already running. Exiting.");
            return 0;
        }

        try
        {
            RunFullScreen();
        }
        finally
        {
            _instanceMutex?.Dispose();
        }

        return 0;
    }

    private static int RunConfigMode()
    {
        SignalPreviewExit();
        ShowOptionsMessage();
        return 0;
    }

    private static int RunPreviewMode(string[] args)
    {
        if (args.Length >= 2 && long.TryParse(args[1], out long hwndValue))
        {
            ShowPreview(new IntPtr(hwndValue));
        }

        return 0;
    }

    private static bool TryAcquireInstanceMutex()
    {
        try
        {
            _instanceMutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                _instanceMutex.Dispose();
                _instanceMutex = null;
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Logging.Log($"Error acquiring instance mutex: {ex.Message}");
            return false;
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
        try
        {
            MonitorInfo.Discover();

            var catalog = InitializePlayback();

            using var idleTracker = new IdleExitTracker();
            var queue = new VideoQueue(catalog.Videos);
            if (!queue.Start())
                return;

            idleTracker.Start();
            HandleMonitorPower();

            Application.Run();
            queue.Dispose();
        }
        catch (Exception ex)
        {
            Logging.Log($"[RunFullScreen Error] {ex.GetType().Name}: {ex.Message}");
            Logging.Log($"StackTrace: {ex.StackTrace}");
        }
    }

    // Done by hand, because Windows disables monitor sleep with full-screen hardware-accelerated video.
    private static void HandleMonitorPower()
    {
        int registryMonitorTimeoutSeconds = RegistryPowerReader.GetRegistryMonitorTimeoutInSeconds();
        Logging.Log($"[MonitorPower] ScreenSaver timeout in seconds: {registryMonitorTimeoutSeconds}");

        if (registryMonitorTimeoutSeconds == 0)
        {
            return;
        }

        var turnOffMonitorsTimer = new System.Windows.Forms.Timer { Interval = registryMonitorTimeoutSeconds * 1000 };
        turnOffMonitorsTimer.Tick += (_, _) =>
        {
            try
            {
                Logging.Log($"[MonitorPower] Turning off monitors after {registryMonitorTimeoutSeconds} seconds.");
                MonitorPower.TurnOffMonitors();
            }
            catch (Exception ex)
            {
                Logging.Log($"[MonitorPower] TurnOffMonitors failed: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                turnOffMonitorsTimer.Stop();
                turnOffMonitorsTimer.Dispose();
            }
        };
        turnOffMonitorsTimer.Start();
    }

    /// <summary>/p <hwnd> - render inside the little preview window of the
    /// Windows screensaver settings dialog.</summary>
    private static void ShowPreview(IntPtr parentHwnd)
    {
        try
        {
            if (parentHwnd == IntPtr.Zero)
                return;

            var catalog = InitializePlayback();

            var availableVideos = catalog.Videos
                .Where(video => !VideoController.IsInMru(video))
                .ToArray();
            if (availableVideos.Length == 0)
                return;

            using var exitSignal = new EventWaitHandle(
                false,
                EventResetMode.ManualReset,
                PreviewExitEventName);
            using var monitor = new System.Windows.Forms.Timer { Interval = 500 };
            using var previewHost = new System.Windows.Forms.Control();
            using var player = new VideoPlayer(previewHost, "preview");
            Video video = availableVideos[Random.Shared.Next(availableVideos.Length)];
            monitor.Tick += (_, _) =>
            {
                try
                {
                    if (exitSignal.WaitOne(0) ||
                        !IsWindow(parentHwnd) ||
                        !IsWindowVisible(parentHwnd))
                        Application.ExitThread();
                }
                catch (Exception ex)
                {
                    Logging.Log($"[Preview monitor tick error] {ex.Message}");
                }
            };
            player.Attach(parentHwnd);
            VideoController.RecordPlayed(video);
            player.Play(video);
            monitor.Start();
            Application.Run();
        }
        catch (Exception ex)
        {
            Logging.Log($"[ShowPreview Error] {ex.GetType().Name}: {ex.Message}");
            Logging.Log($"StackTrace: {ex.StackTrace}");
        }
    }

    /// <summary>Initializes LibVLC and loads the video catalog.</summary>
    private static IVideoCatalog InitializePlayback()
    {
        // Initialize LibVLC and load the shared URL collection.
        VideoPlayer.InitializeCore();
        // Fetch (or refresh) the video catalog before showing anything.
        VideoController.InitializeAsync().GetAwaiter().GetResult();
        // var catalog = new VideoCatalog_tvOS10();
        var catalog = new VideoCatalog_tvOS26();
        catalog.InitializeAsync().GetAwaiter().GetResult();
        return catalog;
    }
}
