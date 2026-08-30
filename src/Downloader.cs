using System;
using System.Formats.Tar;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Aerial;

internal sealed class Downloader
{
    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Aerial");

    public static async Task<string?> DownloadAsync(string url, string cacheFileName)
    {
        Directory.CreateDirectory(CacheDirectory);
        string cachePath = Path.Combine(CacheDirectory, cacheFileName);
        string? content = null;

        try
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };
            using var http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15),
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Aerial-Screensaver/1.0");
            if (IsTarUrl(url))
            {
                content = ExtractFilesFromTar(http, url).Json;
            }
            else
            {
                content = await http.GetStringAsync(url).ConfigureAwait(false);
            }

            if (content is not null)
            {
                try
                {
                    await File.WriteAllTextAsync(cachePath, content).ConfigureAwait(false);
                }
                catch (IOException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            if (File.Exists(cachePath))
                content = await File.ReadAllTextAsync(cachePath).ConfigureAwait(false);
        }

        return content;
    }

    /// <summary>Downloads and caches binary plist file from tar archive.</summary>
    public static async Task<byte[]?> DownloadBinaryAsync(string url, string cacheFileName)
    {
        Directory.CreateDirectory(CacheDirectory);
        string cachePath = Path.Combine(CacheDirectory, cacheFileName);
        byte[]? content = null;

        try
        {
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            };
            using var http = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15),
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Aerial-Screensaver/1.0");

            if (IsTarUrl(url))
                content = ExtractFilesFromTar(http, url).Binary;

            if (content is not null)
            {
                try
                {
                    await File.WriteAllBytesAsync(cachePath, content).ConfigureAwait(false);
                }
                catch (IOException)
                {
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            if (File.Exists(cachePath))
                content = await File.ReadAllBytesAsync(cachePath).ConfigureAwait(false);
        }

        return content;
    }

    private static bool IsTarUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
            uri.AbsolutePath.EndsWith(".tar", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TarExtractionResult
    {
        public string? Json { get; init; }
        public byte[]? Binary { get; init; }
    }

    private static TarExtractionResult ExtractFilesFromTar(HttpClient http, string url)
    {
        byte[] archive = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        using var archiveStream = new MemoryStream(archive, writable: false);
        using var reader = new TarReader(archiveStream);

        string? json = null;
        byte[]? binary = null;

        TarEntry? entry;
        while ((entry = reader.GetNextEntry(copyData: true)) is not null)
        {
            if (entry.EntryType != TarEntryType.RegularFile || entry.DataStream is null)
                continue;

            if (string.Equals(entry.Name, "./entries.json", StringComparison.Ordinal))
            {
                using var contentReader = new StreamReader(entry.DataStream);
                json = contentReader.ReadToEnd();
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

        return new TarExtractionResult { Json = json, Binary = binary };
    }
}
