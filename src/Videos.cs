using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Aerial;

/// <summary>
/// Downloads the Apple Aerial video catalog (entries.json), caches it on disk
/// so subsequent runs can work offline, and parses it into typed structures.
/// </summary>
internal static class Videos
{
    private const string CatalogUrl = "https://sylvan.apple.com/Aerials/2x/entries.json";

    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aerial");

    private static string CachePath => Path.Combine(CacheDirectory, "entries.json");

    /// <summary>Parsed catalog, or null if nothing could be downloaded or loaded.</summary>
    public static AerialCatalog? Catalog { get; private set; }

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

        Catalog = Parse(json);
    }

    /// <summary>Deserializes the catalog JSON; returns null on malformed input.</summary>
    public static AerialCatalog? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            var catalog = JsonSerializer.Deserialize<AerialCatalog>(json, options);
            catalog?.Assets.RemoveAll(a => a is null || string.IsNullOrEmpty(a.Url));
            return catalog;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Root object of entries.json.</summary>
public sealed class AerialCatalog
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("assets")]
    public List<AerialVideo?> Assets { get; set; } = [];
}

/// <summary>One aerial video entry.</summary>
public sealed class AerialVideo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("url-1080-SDR")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("accessibilityLabel")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("categories")]
    public List<string> Categories { get; set; } = [];

    [JsonPropertyName("contentDetails")]
    public AerialContentDetails? ContentDetails { get; set; }
}

/// <summary>Per-video metadata (duration, soundtrack).</summary>
public sealed class AerialContentDetails
{
    [JsonPropertyName("duration")]
    public double DurationSeconds { get; set; }

    [JsonPropertyName("trax")]
    public List<string> Soundtrack { get; set; } = [];
}
