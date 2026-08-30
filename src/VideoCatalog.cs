using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aerial;

internal sealed class VideoCatalog
{
    private readonly string _url;
    private LocalizableStrings? _localizableStrings;

    public VideoCatalog(string url)
    {
        _url = url;
    }

    public IReadOnlyList<Video> Videos { get; private set; } = [];

    public async Task InitializeAsync()
    {
        // Download both files in parallel
        var jsonTask = Downloader.DownloadAsync(_url, "entries.json");
        var plistTask = Downloader.DownloadBinaryAsync(_url, "Localizable.nocache.strings");

        await Task.WhenAll(jsonTask, plistTask).ConfigureAwait(false);

        string? json = await jsonTask;
        byte[]? plistData = await plistTask;

        string plistPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aerial",
            "Localizable.nocache.strings");

        Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);
        if (plistData is not null && plistData.Length > 0)
        {
            await File.WriteAllBytesAsync(plistPath, plistData).ConfigureAwait(false);
        }

        _localizableStrings = new LocalizableStrings(plistPath);
        Videos = ExtractVideos(json, _localizableStrings);
    }

    private static IReadOnlyList<Video> ExtractVideos(string? json, LocalizableStrings? localizableStrings)
    {
        var videos = new List<Video>();

        if (string.IsNullOrWhiteSpace(json))
            return videos;

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            CollectVideoEntries(document.RootElement, videos, localizableStrings);
        }
        catch (JsonException)
        {
        }

        return videos;
    }

    private static void CollectVideoEntries(JsonElement element, ICollection<Video> videos, LocalizableStrings? localizableStrings)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                string? description = null;
                string? accessibilityLabel = null;
                string? localizedNameKey = null;
                var pointsOfInterest = new Dictionary<int, string>();
                var urlEntries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        string value = property.Value.GetString() ?? string.Empty;

                        if (property.NameEquals("url"))
                        {
                            // old catalog, which is 1080 H264
                            urlEntries["url-1080-H264"] = value;
                        }

                        if (property.Name.StartsWith("url-", StringComparison.OrdinalIgnoreCase))
                        {
                            urlEntries[property.Name] = value;
                        }

                         if (property.NameEquals("accessibilityLabel"))
                        {
                            accessibilityLabel = value;
                        }

                        if (property.NameEquals("localizedNameKey"))
                        {
                            localizedNameKey = value;
                        }
                    }

                    if (property.NameEquals("pointsOfInterest") && property.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (JsonProperty point in property.Value.EnumerateObject())
                        {
                            if (!int.TryParse(point.Name, out int timeInSeconds))
                                continue;

                            string lookupKey = point.Value.ValueKind == JsonValueKind.String ? point.Value.GetString() ?? string.Empty : string.Empty;
                            string localizedDescription = string.IsNullOrWhiteSpace(lookupKey)
                                ? string.Empty
                                : (localizableStrings?.GetDescription(lookupKey) ?? lookupKey);

                            pointsOfInterest[timeInSeconds] = localizedDescription;
                        }
                    }
                }

                // Determine description if possible.
                if (!string.IsNullOrWhiteSpace(localizedNameKey) && localizableStrings is not null)
                {
                    description = localizableStrings.GetDescription(localizedNameKey);
                }

                if (description is null && !string.IsNullOrWhiteSpace(accessibilityLabel))
                {
                    description = accessibilityLabel;
                }

                // Only create a Video when the canonical 1080p H.264 URL is present.
                if (urlEntries.TryGetValue("url-1080-H264", out string? hdUrl) &&
                    !string.IsNullOrWhiteSpace(hdUrl) &&
                    Uri.TryCreate(hdUrl, UriKind.Absolute, out _))
                {
                    string desc = description ?? "";
                    videos.Add(new Video(desc, pointsOfInterest, urlEntries));
                }

                // Continue traversing for nested objects/arrays
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    CollectVideoEntries(property.Value, videos, localizableStrings);
                }
                break;

            case JsonValueKind.Array:
                foreach (JsonElement child in element.EnumerateArray())
                    CollectVideoEntries(child, videos, localizableStrings);
                break;
        }
    }
}
