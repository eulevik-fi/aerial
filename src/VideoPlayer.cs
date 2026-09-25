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
    /// <summary>Track all active VideoPlayer instances for global subtitle control.</summary>
    private static readonly List<VideoPlayer> _allPlayers = [];
    
    private readonly MediaPlayer _mediaPlayer;
    private readonly System.Windows.Forms.Control? _hostControl;
    private readonly string _name;
    private Video? _currentVideo;
    private bool _bufferingReported;

    public event Action? Ended;
    public event Action? Failed;

    /// <param name="hostControl">
    /// The control whose surface the video is rendered onto. The handle is
    /// attached lazily in <see cref="Attach"/> once the form is visible,
    /// because the control's Win32 handle can be recreated during form
    /// show/DPI adjustment.
    /// </param>
    public VideoPlayer(System.Windows.Forms.Control hostControl, string name = "screen")
    {
        _hostControl = hostControl;
        _name = name;
        _mediaPlayer = new MediaPlayer(VLCManager.Instance)
        {
            EnableHardwareDecoding = true,
        };

        _mediaPlayer.Buffering += (_, e) =>
        {
            Logging.Log($"[{_name}] Buffering ({e.Cache:0.##}%)");
            _bufferingReported = true;
            if (e.Cache >= 100)
                ShowVideoOutput();
        };

        _mediaPlayer.Playing += (_, _) =>
        {
            Logging.Log($"[{_name}] Playing");
            if (!_bufferingReported)
                ShowVideoOutput();
        };

        _mediaPlayer.Paused += (_, _) =>
        {
            Logging.Log($"[{_name}] Paused");
        };

        _mediaPlayer.Stopped += (_, _) =>
        {
            Logging.Log($"[{_name}] Stopped");
        };

        _mediaPlayer.EndReached += (_, _) =>
        {
            Logging.Log($"[{_name}] Media ended (EndReached)");
            Ended?.Invoke();
        };
        _mediaPlayer.EncounteredError += (_, _) =>
        {
            Logging.Log($"[{_name}] Media playback error (EncounteredError)");
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

        Logging.Log($"[{_name}] Attach handle {_hostControl.Handle}");
        _mediaPlayer.Hwnd = _hostControl.Handle;
    }

    public void Attach(IntPtr hwnd)
    {
        Logging.Log($"[{_name}] Attach handle {hwnd}");
        _mediaPlayer.Hwnd = hwnd;
    }

    /// <summary>Starts streaming playback of the given video.</summary>
    public void Play(Video video, bool showHelpCaption = false, MonitorInfo? monitorInfo = null)
    {
        if (video is null)
            return;

        _currentVideo = video;
        var url = video.GetPreferredUrlForMonitor(monitorInfo);
        
        if (url.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            var builder = new UriBuilder(url)
            {
                Scheme = Uri.UriSchemeHttp,
                Port = url.IsDefaultPort ? -1 : url.Port,
            };
            url = builder.Uri;
        }

        Logging.Log($"[{_name}] Play({url})");
        Logging.Log($"[{_name}] Preparing to play: {url.AbsoluteUri}");
        _bufferingReported = false;
        HideVideoOutput();
        _mediaPlayer.Stop();
        using var media = new Media(VLCManager.Instance, url.AbsoluteUri, FromType.FromLocation);

        Logging.Log($"[{_name}] Starting playback for {url.AbsoluteUri}");
        _mediaPlayer.Play(media);
        if (showHelpCaption)
        {
            Logging.Log($"[{_name}] Showing help caption");
            AddSubtitleFromContent(SrtGeneration.GenerateHelp());
            return;
        }

        AddSubtitle();
    }

    private void HideVideoOutput()
    {
        SetVideoOutputVisibility(false);
    }

    private void ShowVideoOutput()
    {
        SetVideoOutputVisibility(true);
    }

    private void SetVideoOutputVisibility(bool visible)
    {
        if (IsPreviewPlayer(_name) || _hostControl is null || _hostControl.IsDisposed)
            return;

        if (_hostControl.InvokeRequired)
        {
            try
            {
                _hostControl.BeginInvoke(() => SetVideoOutputVisibility(visible));
            }
            catch (InvalidOperationException)
            {
            }
            return;
        }

        _hostControl.Visible = visible;
    }


    public void Stop()
    {
        Logging.Log($"[{_name}] Stop()");
        _mediaPlayer.Stop();
    }

    private static bool IsPreviewPlayer(string name)
    {
        return string.Equals(name, "preview", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanAddSubtitle()
    {
        return !IsPreviewPlayer(_name) &&
               _mediaPlayer.Media is not null &&
               _currentVideo is not null;
    }

    private bool CanHideSubtitle()
    {
        return !IsPreviewPlayer(_name);
    }

    public void AddSubtitle()
    {
        if (!CanAddSubtitle() || _currentVideo is null)
            return;

        Logging.Log($"[{_name}] Adding subtitle");
        string srtContent = _currentVideo.PointsOfInterest.Count > 0
            ? SrtGeneration.GenerateFromPointsOfInterest(_currentVideo.PointsOfInterest)
            : SrtGeneration.GenerateFromDescription(_currentVideo.Description);

        AddSubtitleFromContent(srtContent);
    }

    private void AddSubtitleFromContent(string srtContent)
    {
        if (!CanAddSubtitle())
            return;

        try
        {
            string subtitlePath = Path.Combine(
                Path.GetTempPath(),
                $"video-subtitle-{Guid.NewGuid()}.srt");

            Logging.Log($"[{_name}] Writing subtitle temp file: {subtitlePath}");
            File.WriteAllText(subtitlePath, srtContent);

            string fileUrl = new Uri(subtitlePath).AbsoluteUri;
            Logging.Log($"[{_name}] Attaching subtitle slave: {fileUrl}");
            _mediaPlayer.AddSlave(MediaSlaveType.Subtitle, fileUrl, select: true);
            Logging.Log($"[{_name}] Subtitle attached successfully");
        }
        catch (Exception ex)
        {
            Logging.Log($"[{_name}] Failed to add subtitle: {ex.Message}");
        }
    }

    /// <summary>Add subtitle to all active players.</summary>
    public static void AddSubtitleToAll()
    {
        ForAllNonPreviewPlayers(player => player.AddSubtitle());
    }

    /// <summary>Hide subtitles on all active players by setting SPU to -1.</summary>
    public static void HideSubtitlesFromAll()
    {
        ForAllNonPreviewPlayers(player =>
        {
            if (player.CanHideSubtitle())
            {
                Logging.Log($"[{player._name}] Hiding subtitles");
                player._mediaPlayer.SetSpu(-1);
            }
        });
    }

    /// <summary>Execute an action on all non-preview players.</summary>
    private static void ForAllNonPreviewPlayers(Action<VideoPlayer> action)
    {
        VideoPlayer[] players;

        lock (_allPlayers)
        {
            players = _allPlayers
                .Where(player => !IsPreviewPlayer(player._name))
                .ToArray();
        }

        foreach (var player in players)
            action(player);
    }

    /// <summary>Toggle subtitle display on all players between shown and hidden.</summary>
    public static void ToggleSubtitles()
    {
        var captionsEnabled = CaptionsState.ToggleSubtitles();
        Logging.Log($"[Captions] ToggleSubtitles -> {captionsEnabled}");
        if (captionsEnabled)
        {
            AddSubtitleToAll();
        }
        else
        {
            HideSubtitlesFromAll();
        }
    }

    public void Dispose()
    {
        Logging.Log($"[{_name}] Dispose()");
        _mediaPlayer.Stop();
        _mediaPlayer.Dispose();
        
        // Unregister from the global list
        lock (_allPlayers)
        {
            _allPlayers.Remove(this);
        }
    }

}

