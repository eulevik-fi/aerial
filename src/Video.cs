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
        TryCreateUri(Urls.TryGetValue(UrlHD, out string? urlHd) ? urlHd : null, out Uri? resolvedHd)
            ? resolvedHd
            : throw new InvalidOperationException("Video requires Urls to contain url-1080-H264.");

    public Uri GetPreferredUrlForMonitor(Monitor? monitor)
    {
        if (monitor is null)
            return Url;

        if (monitor.IsLargeDisplay)
            return TryCreateUri(Urls.TryGetValue(Url4K, out string? url4K) ? url4K : null, out Uri? resolved4K)
                ? resolved4K
                : Url;

        return Url;
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

    public Video(string description)
        : this(description, new Dictionary<int, string>(), new Dictionary<string, string>())
    {
    }

    public Video(string description, IReadOnlyDictionary<int, string>? pointsOfInterest)
        : this(description, pointsOfInterest, new Dictionary<string, string>())
    {
    }

    public Video(
        string description,
        IReadOnlyDictionary<int, string>? pointsOfInterest,
        IReadOnlyDictionary<string, string>? urls)
    {
        Description = description ?? throw new ArgumentNullException(nameof(description));
        PointsOfInterest = pointsOfInterest ?? new Dictionary<int, string>();

        Dictionary<string, string> urlMap = urls is null ? new Dictionary<string, string>() : new Dictionary<string, string>(urls);
        Urls = urlMap;
    }

    public override bool Equals(object? obj) => Equals(obj as Video);

    public bool Equals(Video? other)
    {
        return other is not null && Url.Equals(other.Url);
    }

    public override int GetHashCode() => Url.GetHashCode();

    public override string ToString() => Description;
}
