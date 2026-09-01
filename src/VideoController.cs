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
    private const int MruLimit = 10;
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aerial");

    private static string MruPath => Path.Combine(CacheDirectory, "video-mru.txt");
    private static readonly object MruGate = new();
    private static readonly LinkedList<string> RecentVideos = [];

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
            return RecentVideos.Any(url =>
                string.Equals(url, video.Url.AbsoluteUri, StringComparison.OrdinalIgnoreCase));
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
        // Find and remove existing entry (case-insensitive)
        var existing = RecentVideos.FirstOrDefault(url =>
            string.Equals(url, absoluteUri, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            RecentVideos.Remove(existing);

        // Add to front
        RecentVideos.AddFirst(absoluteUri);
        TrimMruToLimit();
    }

    private static void TrimMruToLimit()
    {
        while (RecentVideos.Count > MruLimit)
        {
            RecentVideos.RemoveLast();
        }
    }

    private static void PersistRecentVideos()
    {
        try
        {
            File.WriteAllLines(MruPath, RecentVideos);
        }
        catch (IOException)
        {
            // Persistence is best-effort; retain the in-memory MRU.
        }
    }

    /// <summary>Records a played video, keeping the most recent entries up to the MRU limit.</summary>
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
                    !RecentVideos.Any(existing =>
                        string.Equals(existing, url, StringComparison.OrdinalIgnoreCase)))
                    RecentVideos.AddLast(url);

                if (RecentVideos.Count == MruLimit)
                    break;
            }
        }
    }

}
