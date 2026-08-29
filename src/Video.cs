using System;

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

    public Video(Uri url)
        : this(url, url.AbsoluteUri)
    {
    }

    public Video(Uri url, string description)
    {
        Url = url ?? throw new ArgumentNullException(nameof(url));
        Description = description ?? throw new ArgumentNullException(nameof(description));
    }

    public override bool Equals(object? obj) => Equals(obj as Video);

    public bool Equals(Video? other)
    {
        return other is not null && Url.Equals(other.Url);
    }

    public override int GetHashCode() => Url.GetHashCode();

    public override string ToString() => Description;
}
