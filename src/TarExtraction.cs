using System;
using System.Formats.Tar;
using System.IO;

namespace Aerial;

/// <summary>
/// Extracts files from tar archives.
/// </summary>
internal static class TarExtraction
{
    public sealed class TarExtractionResult
    {
        public string? Json { get; init; }
        public byte[]? Binary { get; init; }
    }

    public static bool IsTarUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
            uri.AbsolutePath.EndsWith(".tar", StringComparison.OrdinalIgnoreCase);
    }

    public static TarExtractionResult ExtractFiles(byte[] archive)
    {
        using var archiveStream = new MemoryStream(archive, writable: false);
        using var reader = new TarReader(archiveStream);

        byte[]? jsonData = null;
        byte[]? binary = null;

        TarEntry? entry;
        while ((entry = reader.GetNextEntry(copyData: true)) is not null)
        {
            if (entry.EntryType != TarEntryType.RegularFile || entry.DataStream is null)
                continue;

            if (string.Equals(entry.Name, "./entries.json", StringComparison.Ordinal))
            {
                using var ms = new MemoryStream();
                entry.DataStream.CopyTo(ms);
                jsonData = ms.ToArray();
                continue;
            }

            if (string.Equals(
                    entry.Name,
                    "./TVIdleScreenStrings.bundle/en.lproj/Localizable.nocache.strings",
                    StringComparison.Ordinal))
            {
                using var ms = new MemoryStream();
                entry.DataStream.CopyTo(ms);
                binary = ms.ToArray();
            }
        }

        string? json = jsonData is not null ? System.Text.Encoding.UTF8.GetString(jsonData) : null;
        return new TarExtractionResult { Json = json, Binary = binary };
    }
}
