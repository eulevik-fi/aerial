using System;
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
            using var http = CreateHttpClient();
            if (TarExtraction.IsTarUrl(url))
            {
                byte[] archive = await http.GetByteArrayAsync(url).ConfigureAwait(false);
                content = TarExtraction.ExtractFiles(archive).Json;
            }
            else
            {
                content = await http.GetStringAsync(url).ConfigureAwait(false);
            }

            if (content is not null)
            {
                await TryCacheContentAsync(cachePath, () => File.WriteAllTextAsync(cachePath, content));
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
            using var http = CreateHttpClient();
            if (TarExtraction.IsTarUrl(url))
            {
                byte[] archive = await http.GetByteArrayAsync(url).ConfigureAwait(false);
                content = TarExtraction.ExtractFiles(archive).Binary;
            }

            if (content is not null)
            {
                await TryCacheContentAsync(cachePath, () => File.WriteAllBytesAsync(cachePath, content));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            if (File.Exists(cachePath))
                content = await File.ReadAllBytesAsync(cachePath).ConfigureAwait(false);
        }

        return content;
    }

    /// <summary>Downloads and caches both JSON and binary files from a tar archive.</summary>
    public static async Task<(string? Json, byte[]? Binary)> DownloadTarAsync(string url, string jsonCacheFileName, string binaryCacheFileName)
    {
        Directory.CreateDirectory(CacheDirectory);
        string jsonCachePath = Path.Combine(CacheDirectory, jsonCacheFileName);
        string binaryCachePath = Path.Combine(CacheDirectory, binaryCacheFileName);
        string? json = null;
        byte[]? binary = null;

        try
        {
            using var http = CreateHttpClient();
            byte[] archive = await http.GetByteArrayAsync(url).ConfigureAwait(false);
            var extracted = TarExtraction.ExtractFiles(archive);
            json = extracted.Json;
            binary = extracted.Binary;

            if (json is not null)
            {
                await TryCacheContentAsync(jsonCachePath, () => File.WriteAllTextAsync(jsonCachePath, json));
            }

            if (binary is not null)
            {
                await TryCacheContentAsync(binaryCachePath, () => File.WriteAllBytesAsync(binaryCachePath, binary));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            if (File.Exists(jsonCachePath))
                json = await File.ReadAllTextAsync(jsonCachePath).ConfigureAwait(false);

            if (File.Exists(binaryCachePath))
                binary = await File.ReadAllBytesAsync(binaryCachePath).ConfigureAwait(false);
        }

        return (json, binary);
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        var http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Aerial-Screensaver/1.0");
        return http;
    }

    private static async Task TryCacheContentAsync(string cachePath, Func<Task> writeOperation)
    {
        try
        {
            await writeOperation().ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }

}
