using System;
using System.Collections.Generic;

namespace Aerial;

/// <summary>
/// Represents a video with a URL and description.
/// </summary>
internal sealed class Video : IEquatable<Video>
{
    private const string UrlHD = "url-1080-H264";
    private const string Url4K = "url-4K-SDR";

    // It is required that Urls contains the 1080p fallback entry.
    /// <summary>The default streaming URL for the video.</summary>
    public Uri Url =>
        TryGetUrlForKey(UrlHD)
            ?? throw new InvalidOperationException("Video requires Urls to contain url-1080-H264.");

    public Uri GetPreferredUrlForMonitor(MonitorInfo? monitorInfo)
    {
        if (monitorInfo is not null && monitorInfo.IsLargeDisplay)
        {
            var url4K = TryGetUrlForKey(Url4K);
            if (url4K is not null)
                return url4K;
        }

        return Url;
    }

    private Uri? TryGetUrlForKey(string key)
    {
        if (Urls.TryGetValue(key, out string? urlValue) &&
            TryCreateUri(urlValue, out Uri? uri))
        {
            return uri;
        }
        return null;
    }

    private static bool TryCreateUri(string? value, out Uri uri)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out uri!);
    }

    /// <summary>All discovered URL variants, keyed by the original JSON property name.</summary>
    public IReadOnlyDictionary<string, string> Urls { get; }

    /// <summary>Description text to display as subtitle.</summary>
    public string Description { get; }

    /// <summary>Descriptions keyed by point-in-time in seconds.</summary>
    public IReadOnlyDictionary<int, string> PointsOfInterest { get; }

    public Video(
        string description,
        IReadOnlyDictionary<int, string>? pointsOfInterest,
        IReadOnlyDictionary<string, string>? urls)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
        PointsOfInterest = pointsOfInterest ?? new Dictionary<int, string>();
        Urls = urls is not null ? new Dictionary<string, string>(urls) : new Dictionary<string, string>();
    }

    public override bool Equals(object? obj) => Equals(obj as Video);

    public bool Equals(Video? other)
    {
        return other is not null && Url.Equals(other.Url);
    }

    public override int GetHashCode() => Url.GetHashCode();

    public override string ToString() => Description;
}
