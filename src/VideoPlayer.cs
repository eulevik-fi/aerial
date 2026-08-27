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
    private readonly System.Windows.Forms.Control _hostControl;
    private readonly string _name;
    private bool _disposed;

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

        _libVlc = new LibVLC(enableDebugLogs: true,
            "");
        _libVlc.Log += (_, e) => Log($"[vlc] {e.Message}");
        Log("LibVLC core initialized.");
    }

    /// <param name="hostControl">
    /// The control whose surface the video is rendered onto. The handle is
    /// attached lazily in <see cref="Attach"/> once the form is visible,
    /// because the control's Win32 handle can be recreated during form
    /// show/DPI adjustment.
    /// </param>
    public VideoPlayer(System.Windows.Forms.Control hostControl, string name = "screen")
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

        _mediaPlayer.Playing += (_, _) => Log($"[{_name}] event: Playing");
        _mediaPlayer.TimeChanged += (_, e) =>
        {
            // Throttle: only log every ~5 seconds of playback.
            if (e.Time % 5000 < 300)
                Log($"[{_name}] event: TimeChanged t={e.Time}ms");
        };
        _mediaPlayer.EncounteredError += (_, _) => Log($"[{_name}] event: ENCOUNTERED_ERROR");
        _mediaPlayer.EndReached += (_, _) => Log($"[{_name}] event: EndReached");
        _mediaPlayer.Buffering += (_, e) =>
        {
            if (e.Cache % 25 == 0)
                Log($"[{_name}] event: Buffering {e.Cache}%");
        };
        _mediaPlayer.Opening += (_, _) => Log($"[{_name}] event: Opening");

        Log($"[{_name}] VideoPlayer created for control {_hostControl.GetType().Name}");
    }

    /// <summary>
    /// Binds playback to the host control's current window handle. Call after
    /// the form has been shown.
    /// </summary>
    public void Attach()
    {
        _mediaPlayer.Hwnd = _hostControl.Handle;
        Log($"[{_name}] attached to hwnd=0x{_hostControl.Handle.ToInt64():X} size={_hostControl.Width}x{_hostControl.Height}");
    }

    /// <summary>Starts streaming playback of the given URL, looping forever.</summary>
    public void Play(Uri url)
    {
        Log($"[{_name}] Play({url})");
        using var media = new Media(_libVlc!, url.AbsoluteUri, FromType.FromLocation);
        media.AddOption(":input-repeat=65535"); // loop indefinitely

        var started = _mediaPlayer.Play(media);
        Log($"[{_name}] MediaPlayer.Play() returned {started}, state={_mediaPlayer.State}");
    }

    public void Stop()
    {
        Log($"[{_name}] Stop(), state={_mediaPlayer.State}");
        _mediaPlayer.Stop();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        Log($"[{_name}] Dispose(), state={_mediaPlayer.State}");
        _mediaPlayer.Stop();
        _mediaPlayer.Dispose();
    }

    /// <summary>
    /// Appends a line to %LOCALAPPDATA%\Aerial\videoplayer.log.
    /// </summary>
    internal static void Log(string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Aerial");
            Directory.CreateDirectory(dir);
            File.AppendAllText(
                Path.Combine(dir, "videoplayer.log"),
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Logging must never break playback.
        }
    }
}

