using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using LibVLCSharp.WinForms;

namespace Aerial;

internal sealed class VideoQueue : IDisposable
{
    private readonly IReadOnlyList<Video> _videos;
    private readonly List<MonitorDisplay> _displays = [];
    private readonly HashSet<Video> _activeVideos = [];
    private readonly object _videoGate = new();
    private bool _started;
    private bool _disposed;

    public VideoQueue(IReadOnlyList<Video> videos)
    {
        _videos = videos;
    }

    public bool Start()
    {
        if (_started)
            return _displays.Count > 0;

        _started = true;
        var initialVideos = GetShuffledInitialVideos();
        if (initialVideos.Count == 0)
            return false;

        foreach (var monitorInfo in MonitorInfo.All)
        {
            var form = CreateMonitorForm(monitorInfo);
            var player = CreatePlayerForForm(form, monitorInfo, initialVideos);
            _displays.Add(new MonitorDisplay(form, player));
        }

        foreach (var display in _displays)
            display.Form.Show();

        return true;
    }

    private List<Video> GetShuffledInitialVideos()
    {
        var initialVideos = _videos
            .Where(video => !VideoController.IsInMru(video))
            .ToList();

        for (int index = initialVideos.Count - 1; index > 0; index--)
        {
            int swapIndex = Random.Shared.Next(index + 1);
            (initialVideos[index], initialVideos[swapIndex]) =
                (initialVideos[swapIndex], initialVideos[index]);
        }

        return initialVideos;
    }

    private ScreenSaverWindow CreateMonitorForm(MonitorInfo monitorInfo)
    {
        var form = new ScreenSaverWindow(monitorInfo);
        var videoView = new VideoView
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
        };
        form.Controls.Add(videoView);
        return form;
    }

    private VideoPlayer CreatePlayerForForm(ScreenSaverWindow form, MonitorInfo monitorInfo, List<Video> initialVideos)
    {
        var player = new VideoPlayer(form.Controls[0] as VideoView ?? throw new InvalidOperationException("Expected video view."), monitorInfo.Name);
        form.VideoPlayer = player;

        var state = new MonitorPlaybackState();
        lock (_videoGate)
        {
            state.CurrentVideo = initialVideos[_displays.Count % initialVideos.Count];
            _activeVideos.Add(state.CurrentVideo);
        }

        player.Ended += () => QueueNextVideo(form, player, monitorInfo, state);
        player.Failed += () => QueueNextVideo(form, player, monitorInfo, state);

        form.Shown += (_, _) =>
        {
            try
            {
                if (_disposed)
                    return;

                player.Attach();
                VideoController.RecordPlayed(state.CurrentVideo);
                bool showCaptionHint = !VideoPlayer._subtitlesShown && !state.HasShownInitialHint;
                state.HasShownInitialHint = true;
                player.Play(state.CurrentVideo, showCaptionHint, monitorInfo);
            }
            catch (Exception ex)
            {
                Logging.Log($"[Form.Shown Error] {ex.GetType().Name}: {ex.Message}");
            }
        };

        return player;
    }

    private void QueueNextVideo(ScreenSaverWindow form, VideoPlayer player, MonitorInfo monitorInfo, MonitorPlaybackState state)
    {
        if (Interlocked.Exchange(ref state.NextVideoQueued, 1) != 0 ||
            _disposed ||
            form.IsDisposed ||
            !form.IsHandleCreated)
            return;

        try
        {
            form.BeginInvoke((Action)(() =>
            {
                Interlocked.Exchange(ref state.NextVideoQueued, 0);
                PlayNextVideo(player, monitorInfo, state);
            }));
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref state.NextVideoQueued, 0);
        }
    }

    private void PlayNextVideo(VideoPlayer player, MonitorInfo monitorInfo, MonitorPlaybackState state)
    {
        try
        {
            Video? nextVideo;
            lock (_videoGate)
            {
                _activeVideos.Remove(state.CurrentVideo);
                nextVideo = VideoController.SelectNextVideo(_videos, state.CurrentVideo, _activeVideos);
                if (nextVideo is not null)
                {
                    state.CurrentVideo = nextVideo;
                    _activeVideos.Add(state.CurrentVideo);
                }
            }

            if (nextVideo is null || _disposed)
                return;

            VideoController.RecordPlayed(nextVideo);
            player.Play(nextVideo, monitorInfo: monitorInfo);
        }
        catch (Exception ex)
        {
            Logging.Log($"[PlayNextVideo Error] {ex.GetType().Name}: {ex.Message}");
        }
    }

    private sealed class MonitorPlaybackState
    {
        public Video CurrentVideo { get; set; } = default!;
        public int NextVideoQueued;
        public bool HasShownInitialHint;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var display in _displays)
            display.Player.BeginShutdown();

        foreach (var display in _displays)
        {
            display.Player.Dispose();
            display.Form.Dispose();
        }
    }

    private sealed record MonitorDisplay(ScreenSaverWindow Form, VideoPlayer Player);
}
