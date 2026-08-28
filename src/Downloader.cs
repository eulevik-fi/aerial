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

    public async Task<string?> DownloadAsync(string url, string cacheFileName)
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
            content = IsTarUrl(url)
                ? DownloadEntriesJsonFromTar(http, url)
                : await http.GetStringAsync(url).ConfigureAwait(false);

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

    private static bool IsTarUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
            uri.AbsolutePath.EndsWith(".tar", StringComparison.OrdinalIgnoreCase);
    }

    private static string? DownloadEntriesJsonFromTar(HttpClient http, string url)
    {
        byte[] archive = http.GetByteArrayAsync(url).GetAwaiter().GetResult();
        using var archiveStream = new MemoryStream(archive, writable: false);
        using var reader = new TarReader(archiveStream);

        TarEntry? entry;
        while ((entry = reader.GetNextEntry(copyData: true)) is not null)
        {
            if (entry.EntryType != TarEntryType.RegularFile ||
                !string.Equals(entry.Name, "./entries.json", StringComparison.Ordinal) ||
                entry.DataStream is null)
                continue;

            using var contentReader = new StreamReader(entry.DataStream);
            return contentReader.ReadToEnd();
        }

        return null;
    }
}
