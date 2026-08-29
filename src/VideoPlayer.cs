using System;
using System.IO;
using LibVLCSharp.Shared;

namespace Aerial;

/// <summary>
/// Plays a video inside a WinForms control using LibVLCSharp. One instance
/// wraps a single MediaPlayer bound to the given control's window handle.
/// </summary>
internal sealed class VideoPlayer : IDisposable
{
    /// <summary>Shared LibVLC core - initialize once per process.</summary>
    private static LibVLC? _libVlc;

    private readonly MediaPlayer _mediaPlayer;
    private readonly System.Windows.Forms.Control? _hostControl;
    private readonly string _name;
    private bool _disposed;
    private volatile bool _shuttingDown;

    public event Action? Ended;
    public event Action? Failed;

    /// <summary>
    /// Initializes the LibVLC core. Call once at startup, before creating any
    /// VideoPlayer instances.
    /// </summary>
    public static void InitializeCore()
    {
        if (_libVlc is not null)
            return;

        Log("Initializing LibVLC core...");
        Core.Initialize();

        // Point the core at the deployed native binaries explicitly, in case
        // automatic resolution picks the wrong location.
        var libDir = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");
        if (Directory.Exists(libDir))
        {
            Log($"Native libvlc found at {libDir}");
        }
        else
        {
            Log($"WARNING: native libvlc directory missing: {libDir}");
        }

        _libVlc = new LibVLC(enableDebugLogs: false,
            "");
        Log("LibVLC core initialized.");
    }

    /// <param name="hostControl">
    /// The control whose surface the video is rendered onto. The handle is
    /// attached lazily in <see cref="Attach"/> once the form is visible,
    /// because the control's Win32 handle can be recreated during form
    /// show/DPI adjustment.
    /// </param>
    public VideoPlayer(System.Windows.Forms.Control hostControl, string name = "screen")
        : this(hostControl, null, name)
    {
    }

    public VideoPlayer(IntPtr hwnd, string name = "screen")
        : this(null, hwnd, name)
    {
    }

    private VideoPlayer(
        System.Windows.Forms.Control? hostControl,
        IntPtr? hwnd,
        string name)
    {
        if (_libVlc is null)
            throw new InvalidOperationException(
                "Call VideoPlayer.InitializeCore() before creating a VideoPlayer.");

        _hostControl = hostControl;
        _name = name;
        _mediaPlayer = new MediaPlayer(_libVlc)
        {
            EnableHardwareDecoding = true,
        };

        _mediaPlayer.EndReached += (_, _) =>
        {
            if (!_shuttingDown)
                Ended?.Invoke();
        };
        _mediaPlayer.EncounteredError += (_, _) =>
        {
            if (!_shuttingDown)
                Failed?.Invoke();
        };
    }

    /// <summary>
    /// Binds playback to the host control's current window handle. Call after
    /// the form has been shown.
    /// </summary>
    public void Attach()
    {
        if (_hostControl is null)
            throw new InvalidOperationException("This player requires Attach(IntPtr) for a direct window handle.");

        _mediaPlayer.Hwnd = _hostControl.Handle;
    }

    public void Attach(IntPtr hwnd)
    {
        _mediaPlayer.Hwnd = hwnd;
    }

    /// <summary>Starts streaming playback of the given URL.</summary>
    public void Play(Uri url)
    {
        if (_shuttingDown || _disposed)
            return;

        if (url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(url)
            {
                Scheme = Uri.UriSchemeHttp,
                Port = url.IsDefaultPort ? -1 : url.Port,
            };
            url = builder.Uri;
        }

        Log($"[{_name}] Play({url})");
        _mediaPlayer.Stop();
        using var media = new Media(_libVlc!, url.AbsoluteUri, FromType.FromLocation);

        media.AddOption(":no-gnutls-system-trust");
        media.AddOption(":http-reconnect");

        var started = _mediaPlayer.Play(media);
    }

    public void Stop()
    {
        BeginShutdown();
        _mediaPlayer.Stop();
    }

    public void BeginShutdown()
    {
        _shuttingDown = true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        BeginShutdown();
        _disposed = true;

        _mediaPlayer.Stop();
        _mediaPlayer.Dispose();
    }

    internal static void PrepareLog()
    {
        try
        {
            string logPath = GetLogPath();
            if (!File.Exists(logPath))
                return;

            if (DateTime.Now - File.GetCreationTime(logPath) > TimeSpan.FromMinutes(5))
            {
                File.Delete(logPath);
                File.WriteAllText(logPath, string.Empty);
            }
        }
        catch (IOException)
        {
        }
    }

    internal static void Log(string message)
    {
        try
        {
            string logPath = GetLogPath();
            File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Logging must never break playback.
        }
    }

    private static string GetLogPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aerial");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "aerial-log.txt");
    }
}

