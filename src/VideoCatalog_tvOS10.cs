using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aerial;

/// <summary>
/// Video catalog for tvOS 10-23: supports 1080p H.264 only, with accessibility labels (no localization).
/// </summary>
internal sealed class VideoCatalog_tvOS10 : IVideoCatalog
{
    /// <summary>tvOS 10: 1080p + H.264 only, no localization or points of interest</summary>
    private const string CatalogUrl = "http://a1.phobos.apple.com/us/r1000/000/Features/atv/AutumnResources/videos/entries.json";

    public IReadOnlyList<Video> Videos { get; private set; } = [];

    public async Task InitializeAsync()
    {
        // Download JSON directly from HTTP
        string? json = await Downloader.DownloadAsync(CatalogUrl, "entries.json").ConfigureAwait(false);
        Videos = ExtractVideos(json);
    }

    private static IReadOnlyList<Video> ExtractVideos(string? json)
    {
        var videos = new List<Video>();

        if (string.IsNullOrWhiteSpace(json))
            return videos;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            CollectVideoEntries(document.RootElement, videos);
        }
        catch (JsonException)
        {
        }

        return videos;
    }

    private static void CollectVideoEntries(JsonElement element, ICollection<Video> videos)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                ProcessVideoObject(element, videos);
                break;

            case JsonValueKind.Array:
                foreach (JsonElement child in element.EnumerateArray())
                    CollectVideoEntries(child, videos);
                break;
        }
    }

    private static void ProcessVideoObject(JsonElement element, ICollection<Video> videos)
    {
        var urlEntries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? description = null;

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                // Continue traversing for nested objects/arrays
                CollectVideoEntries(property.Value, videos);
                continue;
            }

            string value = property.Value.GetString() ?? string.Empty;

            if (property.NameEquals("url"))
            {
                urlEntries["url-1080-H264"] = value;
            }
            else if (property.NameEquals("accessibilityLabel") && description is null)
            {
                description = value;
            }
        }

        // Create Video if we have a valid URL
        if (urlEntries.TryGetValue("url-1080-H264", out string? hdUrl) &&
            !string.IsNullOrWhiteSpace(hdUrl) &&
            Uri.TryCreate(hdUrl, UriKind.Absolute, out _))
        {
            videos.Add(new Video(description ?? "", new Dictionary<int, string>(), urlEntries));
        }
    }
}
