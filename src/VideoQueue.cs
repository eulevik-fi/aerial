using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using LibVLCSharp.WinForms;

namespace Aerial;

internal sealed class VideoQueue : IDisposable
{
    private readonly IReadOnlyList<Video> _videos;
    private readonly List<ScreensaverForm> _forms = [];
    private readonly List<VideoPlayer> _players = [];
    private readonly HashSet<Video> _activeVideos = [];
    private readonly object _videoGate = new();
    private bool _started;
    private bool _disposed;

    public VideoQueue(IReadOnlyList<Video> videos)
    {
        _videos = videos;
    }

    /// <summary>Compatibility constructor that converts Uri list to Video list.</summary>
    public VideoQueue(IReadOnlyList<Uri> videoUrls)
        : this(videoUrls.Select(url => new Video(url)).ToList())
    {
    }

    public bool Start()
    {
        if (_started)
            return _forms.Count > 0;

        _started = true;
        var initialVideos = _videos
            .Where(video => !Videos.IsInMru(video))
            .ToList();

        if (initialVideos.Count == 0)
            return false;

        for (int index = initialVideos.Count - 1; index > 0; index--)
        {
            int swapIndex = Random.Shared.Next(index + 1);
            (initialVideos[index], initialVideos[swapIndex]) =
                (initialVideos[swapIndex], initialVideos[index]);
        }

        foreach (Screen screen in Screen.AllScreens)
        {
            var form = new ScreensaverForm(screen);
            var videoView = new VideoView
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
            };
            form.Controls.Add(videoView);

            var player = new VideoPlayer(videoView, $"screen{_forms.Count}");
            _players.Add(player);
            form.VideoPlayer = player;  // Connect player to form for shift key handling
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
                    nextVideo = Videos.SelectNextVideo(_videos, currentVideo, _activeVideos);
                    if (nextVideo is not null)
                    {
                        currentVideo = nextVideo;
                        _activeVideos.Add(currentVideo);
                    }
                }

                if (nextVideo is null || _disposed)
                    return;

                Videos.RecordPlayed(nextVideo);
                player.Play(nextVideo);
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
                Videos.RecordPlayed(currentVideo);
                player.Play(currentVideo);
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
