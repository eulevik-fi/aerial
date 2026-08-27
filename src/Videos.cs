using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aerial;

/// <summary>
/// Downloads the Apple Aerial video catalog (entries.json), caches it on disk
/// so subsequent runs can work offline, and extracts URL values from it.
/// </summary>
internal static class Videos
{
    private const string CatalogUrl = "http://a1.phobos.apple.com/us/r1000/000/Features/atv/AutumnResources/videos/entries.json";

    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aerial");

    private static string CachePath => Path.Combine(CacheDirectory, "entries.json");
    private static string MruPath => Path.Combine(CacheDirectory, "video-mru.txt");
    private static readonly object MruGate = new();
    private static readonly List<string> RecentVideos = [];

    /// <summary>Values of every JSON property named exactly "url".</summary>
    public static IReadOnlyList<string> UrlValues { get; private set; } = [];

    /// <summary>Loads the catalog from cache, refreshing it from the network.</summary>
    public static async Task InitializeAsync()
    {
        Directory.CreateDirectory(CacheDirectory);

        string? json = null;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Aerial-Screensaver/1.0");
            json = await http.GetStringAsync(CatalogUrl).ConfigureAwait(false);

            try
            {
                await File.WriteAllTextAsync(CachePath, json).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // Cache write is best-effort; the in-memory copy still works.
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Network unavailable - fall back to the cached copy.
            if (File.Exists(CachePath))
                json = await File.ReadAllTextAsync(CachePath).ConfigureAwait(false);
        }

        UrlValues = ExtractUrlValues(json);
        LoadMru();

        foreach (string url in UrlValues)
        {
            Log($"Video URL: {url}");
        }
    }

    public static bool IsInMru(Uri url)
    {
        lock (MruGate)
        {
            return RecentVideos.Contains(url.AbsoluteUri, StringComparer.OrdinalIgnoreCase);
        }
    }

    public static Uri? SelectNextVideo(
        IReadOnlyList<Uri> videoUrls,
        Uri current,
        HashSet<Uri> activeVideos)
    {
        var availableVideos = videoUrls
            .Where(video => video != current &&
                            !activeVideos.Contains(video) &&
                            !IsInMru(video))
            .ToArray();

        return availableVideos.Length == 0
            ? null
            : availableVideos[Random.Shared.Next(availableVideos.Length)];
    }

    /// <summary>Records a started URL, keeping the 10 most recent entries.</summary>
    public static void RecordPlayed(Uri url)
    {
        lock (MruGate)
        {
            RecentVideos.RemoveAll(existing =>
                string.Equals(existing, url.AbsoluteUri, StringComparison.OrdinalIgnoreCase));
            RecentVideos.Insert(0, url.AbsoluteUri);
            if (RecentVideos.Count > 10)
                RecentVideos.RemoveRange(10, RecentVideos.Count - 10);

            try
            {
                File.WriteAllLines(MruPath, RecentVideos);
            }
            catch (IOException)
            {
                // Persistence is best-effort; retain the in-memory MRU.
            }
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

    /// <summary>
    /// Returns the string values of every property named exactly "url",
    /// regardless of its nesting inside objects or arrays.
    /// </summary>
    public static IReadOnlyList<string> ExtractUrlValues(string? json)
    {
        var urls = new List<string>();

        if (string.IsNullOrWhiteSpace(json))
            return urls;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            CollectUrlValues(document.RootElement, urls);
        }
        catch (JsonException)
        {
            // Return the values collected so far for malformed documents.
        }

        return urls;
    }

    private static void CollectUrlValues(JsonElement element, ICollection<string> urls)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.NameEquals("url") && property.Value.ValueKind == JsonValueKind.String)
                        urls.Add(property.Value.GetString()!);

                    CollectUrlValues(property.Value, urls);
                }
                break;

            case JsonValueKind.Array:
                foreach (JsonElement child in element.EnumerateArray())
                    CollectUrlValues(child, urls);
                break;
        }
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(
                Path.Combine(CacheDirectory, "aerial-log.txt"),
                $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Logging must never prevent catalog initialization.
        }
    }
}
