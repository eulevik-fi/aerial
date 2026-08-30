using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    
    /// <summary>Track all active VideoPlayer instances for global subtitle control.</summary>
    private static readonly List<VideoPlayer> _allPlayers = [];
    
    /// <summary>Track whether subtitles are currently displayed across all players.</summary>
    internal static bool _subtitlesShown = File.Exists(CaptionsState.GetUseCaptionsPath());

    private readonly MediaPlayer _mediaPlayer;
    private readonly System.Windows.Forms.Control? _hostControl;
    private readonly string _name;
    private bool _disposed;
    private volatile bool _shuttingDown;
    private Video? _currentVideo;

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

        Logging.Log("Initializing LibVLC core...");
        Core.Initialize();

        // Point the core at the deployed native binaries explicitly, in case
        // automatic resolution picks the wrong location.
        var libDir = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");
        if (Directory.Exists(libDir))
        {
            Logging.Log($"Native libvlc found at {libDir}");
        }
        else
        {
            Logging.Log($"WARNING: native libvlc directory missing: {libDir}");
        }

        string[] subtitleOptions = new string[]
        {
            "--freetype-font=Tahoma", // sans-serif, reliably available
            "--freetype-rel-fontsize=36", // very small
            "--subsdec-align=9", // bottom left
            "--sub-margin=30", // margin padding
        };
    
        _libVlc = new LibVLC(enableDebugLogs: false,
            subtitleOptions);
        CaptionsState.SyncSubtitleStateFromDisk();
        Logging.Log("LibVLC core initialized.");
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
        
        // Register this player in the global list
        lock (_allPlayers)
        {
            _allPlayers.Add(this);
        }
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

    /// <summary>Starts streaming playback of the given video.</summary>
    public void Play(Video video)
    {
        if (_shuttingDown || _disposed || video is null)
            return;

        _currentVideo = video;
        var url = video.Url;
        
        if (url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(url)
            {
                Scheme = Uri.UriSchemeHttp,
                Port = url.IsDefaultPort ? -1 : url.Port,
            };
            url = builder.Uri;
        }

        CaptionsState.SyncSubtitleStateFromDisk();

        Logging.Log($"[{_name}] Play({url})");
        _mediaPlayer.Stop();
        using var media = new Media(_libVlc!, url.AbsoluteUri, FromType.FromLocation);

        media.AddOption(":no-gnutls-system-trust");
        media.AddOption(":http-reconnect");

        var started = _mediaPlayer.Play(media);
        if (_subtitlesShown)
        {
            var retry = new System.Windows.Forms.Timer
            {
                Interval = 250,
            };
            retry.Tick += (_, _) =>
            {
                try
                {
                    retry.Stop();
                    retry.Dispose();
                    if (_subtitlesShown)
                        AddSubtitle();
                }
                catch (Exception ex)
                {
                    Logging.Log($"[{_name}] Subtitle retry failed: {ex.Message}");
                }
            };
            retry.Start();
        }
    }

    /// <summary>Compatibility overload for Uri (converts to Video internally).</summary>
    public void Play(Uri url)
    {
        if (url is null)
            return;
        Play(new Video(url));
    }

    public void Stop()
    {
        BeginShutdown();
        _mediaPlayer.Stop();
    }

    public void AddSubtitle()
    {
        if (_shuttingDown || _disposed || _mediaPlayer.Media is null || _currentVideo is null)
            return;

        try
        {
            // Create temporary SRT file with video description or point-of-interest captions.
            string subtitlePath = Path.Combine(
                Path.GetTempPath(),
                $"video-subtitle-{Guid.NewGuid()}.srt");

            string srtContent = _currentVideo.PointsOfInterest.Count > 0
                ? SrtGeneration.GenerateFromPointsOfInterest(_currentVideo.PointsOfInterest)
                : SrtGeneration.GenerateFromDescription(_currentVideo.Description);

            File.WriteAllText(subtitlePath, srtContent);
            Logging.Log($"[{_name}] Generated SRT file: {subtitlePath} ({srtContent.Length} chars)");

            // Convert to file:// URL
            string fileUrl = new Uri(subtitlePath).AbsoluteUri;

            // AddSlave with select=true
            _mediaPlayer.AddSlave(MediaSlaveType.Subtitle, fileUrl, select: true);
        }
        catch (Exception ex)
        {
            Logging.Log($"[{_name}] Failed to add subtitle: {ex.Message}");
        }
    }

    /// <summary>Add subtitle to all active players.</summary>
    public static void AddSubtitleToAll()
    {
        lock (_allPlayers)
        {
            foreach (var player in _allPlayers)
            {
                player.AddSubtitle();
            }
        }
        _subtitlesShown = true;
        CaptionsState.PersistSubtitleState(_subtitlesShown);
    }

    /// <summary>Hide subtitles on all active players by setting SPU to -1.</summary>
    public static void HideSubtitlesFromAll()
    {
        lock (_allPlayers)
        {
            foreach (var player in _allPlayers)
            {
                if (!player._shuttingDown && !player._disposed)
                {
                    player._mediaPlayer.SetSpu(-1);
                }
            }
        }
        _subtitlesShown = false;
        CaptionsState.PersistSubtitleState(_subtitlesShown);
    }

    /// <summary>Toggle subtitle display on all players between shown and hidden.</summary>
    public static void ToggleSubtitles()
    {
        if (_subtitlesShown)
        {
            HideSubtitlesFromAll();
        }
        else
        {
            AddSubtitleToAll();
        }
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
        
        // Unregister from the global list
        lock (_allPlayers)
        {
            _allPlayers.Remove(this);
        }
    }

}

