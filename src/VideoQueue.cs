using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using LibVLCSharp.WinForms;

namespace Aerial;

internal sealed class VideoQueue : IDisposable
{
    private readonly IReadOnlyList<Video> _videos;
    private readonly List<ScreenSaverWindow> _forms = [];
    private readonly List<VideoPlayer> _players = [];
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
            return _forms.Count > 0;

        _started = true;
        var initialVideos = _videos
            .Where(video => !VideoController.IsInMru(video))
            .ToList();

        if (initialVideos.Count == 0)
            return false;

        for (int index = initialVideos.Count - 1; index > 0; index--)
        {
            int swapIndex = Random.Shared.Next(index + 1);
            (initialVideos[index], initialVideos[swapIndex]) =
                (initialVideos[swapIndex], initialVideos[index]);
        }

        foreach (var monitor in Monitor.All)
        {
            var form = new ScreenSaverWindow(monitor);
            var videoView = new VideoView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
            };
            form.Controls.Add(videoView);

            var player = new VideoPlayer(videoView, monitor.Name);
            _players.Add(player);
            form.VideoPlayer = player;  // Connect player to form for shift key handling
            bool hasShownInitialHint = false;
            int nextVideoQueued = 0;
            Video currentVideo;
            lock (_videoGate)
            {
                currentVideo = initialVideos[_forms.Count % initialVideos.Count];
                _activeVideos.Add(currentVideo);
            }

            void PlayNextVideo()
            {
                Video? nextVideo;
                lock (_videoGate)
                {
                    _activeVideos.Remove(currentVideo);
                    nextVideo = VideoController.SelectNextVideo(_videos, currentVideo, _activeVideos);
                    if (nextVideo is not null)
                    {
                        currentVideo = nextVideo;
                        _activeVideos.Add(currentVideo);
                    }
                }

                if (nextVideo is null || _disposed)
                    return;

                VideoController.RecordPlayed(nextVideo);
                player.Play(nextVideo, monitor);
            }

            void QueueNextVideo()
            {
                if (Interlocked.Exchange(ref nextVideoQueued, 1) != 0 ||
                    _disposed ||
                    form.IsDisposed ||
                    !form.IsHandleCreated)
                    return;

                try
                {
                    form.BeginInvoke((Action)(() =>
                    {
                        Interlocked.Exchange(ref nextVideoQueued, 0);
                        PlayNextVideo();
                    }));
                }
                catch (InvalidOperationException)
                {
                    Interlocked.Exchange(ref nextVideoQueued, 0);
                }
            }

            player.Ended += QueueNextVideo;
            player.Failed += QueueNextVideo;

            form.Shown += (_, _) =>
            {
                if (_disposed)
                    return;

                player.Attach();
                VideoController.RecordPlayed(currentVideo);
                bool showCaptionHint = !VideoPlayer._subtitlesShown && !hasShownInitialHint;
                hasShownInitialHint = true;
                player.Play(currentVideo, showCaptionHint, monitor);
            };
            _forms.Add(form);
        }

        foreach (var form in _forms)
            form.Show();

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        foreach (var player in _players)
            player.BeginShutdown();

        foreach (var player in _players)
            player.Dispose();
        foreach (var form in _forms)
            form.Dispose();
    }
}
