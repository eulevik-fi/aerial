using System;
using System.Collections.Generic;

namespace Aerial;

/// <summary>
/// Represents a video with a URL and description.
/// </summary>
internal sealed class Video : IEquatable<Video>
{
    /// <summary>The streaming URL for the video.</summary>
    public Uri Url { get; }

    /// <summary>Description text to display as subtitle.</summary>
    public string Description { get; }

    /// <summary>Descriptions keyed by point-in-time in seconds.</summary>
    public IReadOnlyDictionary<int, string> PointsOfInterest { get; }

    public Video(Uri url)
        : this(url, url.AbsoluteUri)
    {
    }

    public Video(Uri url, string description)
        : this(url, description, new Dictionary<int, string>())
    {
    }

    public Video(Uri url, string description, IReadOnlyDictionary<int, string>? pointsOfInterest)
    {
        Url = url ?? throw new ArgumentNullException(nameof(url));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        PointsOfInterest = pointsOfInterest ?? new Dictionary<int, string>();
    }

    public override bool Equals(object? obj) => Equals(obj as Video);

    public bool Equals(Video? other)
    {
        return other is not null && Url.Equals(other.Url);
    }

    public override int GetHashCode() => Url.GetHashCode();

    public override string ToString() => Description;
}
