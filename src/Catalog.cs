using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aerial;

internal sealed class Catalog
{
    private readonly string _url;
    private readonly Downloader _downloader;

    public Catalog(string url, Downloader? downloader = null)
    {
        _url = url;
        _downloader = downloader ?? new Downloader();
    }

    public IReadOnlyList<Video> Videos { get; private set; } = [];

    public async Task InitializeAsync()
    {
        string? json = await _downloader
            .DownloadAsync(_url, "entries.json")
            .ConfigureAwait(false);

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
                // Check if this object has url-1080-H264 and accessibilityLabel
                string? urlValue = null;
                string? description = null;

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.NameEquals("url") && property.Value.ValueKind == JsonValueKind.String)
                        urlValue = property.Value.GetString()!;

                    if (property.NameEquals("url-1080-H264") && property.Value.ValueKind == JsonValueKind.String)
                    {
                        urlValue = property.Value.GetString()?.Replace("\\", string.Empty);
                    }
                    
                    if (property.NameEquals("accessibilityLabel") && property.Value.ValueKind == JsonValueKind.String)
                    {
                        description = property.Value.GetString();
                    }
                }

                // If we found both URL and description, create a Video
                if (!string.IsNullOrWhiteSpace(urlValue) && Uri.TryCreate(urlValue, UriKind.Absolute, out Uri? uri))
                {
                    string desc = description ?? uri.AbsoluteUri;
                    videos.Add(new Video(uri, desc));
                }

                // Continue traversing for nested objects/arrays
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    CollectVideoEntries(property.Value, videos);
                }
                break;

            case JsonValueKind.Array:
                foreach (JsonElement child in element.EnumerateArray())
                    CollectVideoEntries(child, videos);
                break;
        }
    }
}
