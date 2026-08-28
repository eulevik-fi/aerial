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

    public IReadOnlyList<string> UrlValues { get; private set; } = [];

    public async Task InitializeAsync()
    {
        string? json = await _downloader
            .DownloadAsync(_url, "entries.json")
            .ConfigureAwait(false);

        UrlValues = ExtractUrlValues(json);
    }

    private static IReadOnlyList<string> ExtractUrlValues(string? json)
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
}
