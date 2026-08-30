using System;
using System.Collections.Generic;

namespace Aerial;

/// <summary>
/// Represents a video with a URL and description.
/// </summary>
internal sealed class Video : IEquatable<Video>
{
    // It is required that Urls contains the "url-1080-H264" entry.
    /// <summary>The streaming URL for the video.</summary>
    public Uri Url =>
        Urls.TryGetValue("url-1080-H264", out string? highDefinitionUrl) &&
        Uri.TryCreate(highDefinitionUrl, UriKind.Absolute, out Uri? resolvedHighDefinitionUrl)
            ? resolvedHighDefinitionUrl
            : throw new InvalidOperationException("Video requires Urls to contain url-1080-H264.");

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
