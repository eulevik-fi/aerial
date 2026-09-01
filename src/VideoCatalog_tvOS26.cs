using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aerial;

/// <summary>
/// Video catalog for tvOS 26+: supports 4K HDR/SDR, HEVC, H.264, with localized descriptions from TAR archive.
/// </summary>
internal sealed class VideoCatalog_tvOS26 : IVideoCatalog
{
    /// <summary>tvOS 26: 4K + SDR/HDR + HEVC, 1080p + H.264, localised descriptions in TAR archive</summary>
    private const string CatalogUrl = "https://sylvan.apple.com/itunes-assets/Aerials126/v4/c0/45/d9/c045d9d0-9606-1535-62fe-189edb4f79eb/resources-atv-23J-2.tar";

    private LocalizableStrings? _localizableStrings;

    public IReadOnlyList<Video> Videos { get; private set; } = [];

    public async Task InitializeAsync()
    {
        // Download both files in a single call (from TAR)
        var (json, plistData) = await Downloader.DownloadTarAsync(CatalogUrl, "entries.json", "Localizable.nocache.strings").ConfigureAwait(false);

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
                ProcessVideoObject(element, videos, localizableStrings);
                break;

            case JsonValueKind.Array:
                foreach (JsonElement child in element.EnumerateArray())
                    CollectVideoEntries(child, videos, localizableStrings);
                break;
        }
    }

    private static void ProcessVideoObject(JsonElement element, ICollection<Video> videos, LocalizableStrings? localizableStrings)
    {
        var urlEntries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pointsOfInterest = new Dictionary<int, string>();
        string? description = null;
        string? accessibilityLabel = null;
        string? localizedNameKey = null;

        // Extract metadata and traverse in a single pass
        foreach (JsonProperty property in element.EnumerateObject())
        {
            ExtractStringProperties(property, ref accessibilityLabel, ref localizedNameKey, urlEntries);
            ExtractPointsOfInterest(property, pointsOfInterest, localizableStrings);
            
            // Continue traversing for nested objects/arrays
            CollectVideoEntries(property.Value, videos, localizableStrings);
        }

        // Resolve the description from available sources
        description = ResolveDescription(localizedNameKey, accessibilityLabel, localizableStrings);

        // Create Video if we have a valid HD URL
        if (urlEntries.TryGetValue("url-1080-H264", out string? hdUrl) &&
            !string.IsNullOrWhiteSpace(hdUrl) &&
            Uri.TryCreate(hdUrl, UriKind.Absolute, out _))
        {
            videos.Add(new Video(description ?? "", pointsOfInterest, urlEntries));
        }
    }

    private static void ExtractStringProperties(JsonProperty property, ref string? accessibilityLabel, ref string? localizedNameKey, Dictionary<string, string> urlEntries)
    {
        if (property.Value.ValueKind != JsonValueKind.String)
            return;

        string value = property.Value.GetString() ?? string.Empty;

        if (property.Name.StartsWith("url-", StringComparison.OrdinalIgnoreCase))
        {
            urlEntries[property.Name] = value;
        }
        else if (property.NameEquals("accessibilityLabel"))
        {
            accessibilityLabel = value;
        }
        else if (property.NameEquals("localizedNameKey"))
        {
            localizedNameKey = value;
        }
    }

    private static void ExtractPointsOfInterest(JsonProperty property, Dictionary<int, string> pointsOfInterest, LocalizableStrings? localizableStrings)
    {
        if (!property.NameEquals("pointsOfInterest") || property.Value.ValueKind != JsonValueKind.Object)
            return;

        foreach (JsonProperty point in property.Value.EnumerateObject())
        {
            if (!int.TryParse(point.Name, out int timeInSeconds))
                continue;

            string lookupKey = point.Value.ValueKind == JsonValueKind.String ? (point.Value.GetString() ?? string.Empty) : string.Empty;
            string localizedDescription = string.IsNullOrWhiteSpace(lookupKey)
                ? string.Empty
                : (localizableStrings?.GetDescription(lookupKey) ?? lookupKey);

            pointsOfInterest[timeInSeconds] = localizedDescription;
        }
    }

    private static string? ResolveDescription(string? localizedNameKey, string? accessibilityLabel, LocalizableStrings? localizableStrings)
    {
        // Try localized name key first
        if (!string.IsNullOrWhiteSpace(localizedNameKey) && localizableStrings is not null)
        {
            return localizableStrings.GetDescription(localizedNameKey);
        }

        // Fall back to accessibility label
        return accessibilityLabel;
    }
}
