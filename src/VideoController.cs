using System;
using System.Collections.Generic;
using System.IO;

namespace Aerial;

/// <summary>
/// Downloads the Apple Aerial video catalog (entries.json), caches it on disk
/// so subsequent runs can work offline, and extracts URL values from it.
/// </summary>
internal static class VideoController
{
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aerial");

    private static string MruPath => Path.Combine(CacheDirectory, "video-mru.txt");
    private static readonly object MruGate = new();
    private static readonly List<string> RecentVideos = [];

    /// <summary>Loads the recently played video list.</summary>
    public static async Task InitializeAsync()
    {
        Directory.CreateDirectory(CacheDirectory);
        LoadMru();
    }

    public static bool IsInMru(Video video)
    {
        lock (MruGate)
        {
            return RecentVideos.Contains(video.Url.AbsoluteUri, StringComparer.OrdinalIgnoreCase);
        }
    }

    public static Video? SelectNextVideo(
        IReadOnlyList<Video> videos,
        Video current,
        HashSet<Video> activeVideos)
    {
        var availableVideos = videos
            .Where(video => video != current &&
                            !activeVideos.Contains(video) &&
                            !IsInMru(video))
            .ToArray();

        return availableVideos.Length == 0
            ? null
            : availableVideos[Random.Shared.Next(availableVideos.Length)];
    }

    private static void AddRecentVideo(string absoluteUri)
    {
        RecentVideos.RemoveAll(existing =>
            string.Equals(existing, absoluteUri, StringComparison.OrdinalIgnoreCase));
        RecentVideos.Insert(0, absoluteUri);
        if (RecentVideos.Count > 10)
            RecentVideos.RemoveRange(10, RecentVideos.Count - 10);
    }

    private static void PersistRecentVideos()
    {
        try
        {
            File.WriteAllLines(MruPath, RecentVideos.ToArray());
        }
        catch (IOException)
        {
            // Persistence is best-effort; retain the in-memory MRU.
        }
    }

    /// <summary>Records a played video, keeping the 10 most recent entries.</summary>
    public static void RecordPlayed(Video video)
    {
        lock (MruGate)
        {
            AddRecentVideo(video.Url.AbsoluteUri);
            PersistRecentVideos();
        }
    }

    private static void LoadMru()
    {
        lock (MruGate)
        {
            RecentVideos.Clear();
            if (!File.Exists(MruPath))
                return;

            foreach (string url in File.ReadLines(MruPath))
            {
                if (!string.IsNullOrWhiteSpace(url) &&
                    !RecentVideos.Contains(url, StringComparer.OrdinalIgnoreCase))
                    RecentVideos.Add(url);

                if (RecentVideos.Count == 10)
                    break;
            }
        }
    }

}
