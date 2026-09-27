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

    public static string? Download(string url, string cacheFileName)
    {
        Directory.CreateDirectory(CacheDirectory);
        string cachePath = Path.Combine(CacheDirectory, cacheFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        string? content = null;

        try
        {
            using var http = CreateHttpClient();
            if (TarExtraction.IsTarUrl(url))
            {
                byte[] archive = http.Send(new HttpRequestMessage(HttpMethod.Get, url)).Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                content = TarExtraction.ExtractFiles(archive).Json;
            }
            else
            {
                content = http.Send(new HttpRequestMessage(HttpMethod.Get, url)).Content.ReadAsStringAsync().GetAwaiter().GetResult();
            }

            if (content is not null)
            {
                TryCacheContent(cachePath, () => File.WriteAllText(cachePath, content));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            if (File.Exists(cachePath))
                content = File.ReadAllText(cachePath);
        }

        return content;
    }

    /// <summary>Downloads and caches binary plist file from tar archive.</summary>
    public static byte[]? DownloadBinary(string url, string cacheFileName)
    {
        Directory.CreateDirectory(CacheDirectory);
        string cachePath = Path.Combine(CacheDirectory, cacheFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        byte[]? content = null;

        try
        {
            using var http = CreateHttpClient();
            if (TarExtraction.IsTarUrl(url))
            {
                byte[] archive = http.Send(new HttpRequestMessage(HttpMethod.Get, url)).Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                content = TarExtraction.ExtractFiles(archive).Binary;
            }

            if (content is not null)
            {
                TryCacheContent(cachePath, () => File.WriteAllBytes(cachePath, content));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            if (File.Exists(cachePath))
                content = File.ReadAllBytes(cachePath);
        }

        return content;
    }

    /// <summary>Downloads and caches both JSON and binary files from a tar archive.</summary>
    public static (string? Json, byte[]? Binary) DownloadTar(string url, string jsonCacheFileName, string binaryCacheFileName)
    {
        Directory.CreateDirectory(CacheDirectory);
        string jsonCachePath = Path.Combine(CacheDirectory, jsonCacheFileName);
        string binaryCachePath = Path.Combine(CacheDirectory, binaryCacheFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonCachePath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(binaryCachePath)!);
        string? json = null;
        byte[]? binary = null;

        try
        {
            using var http = CreateHttpClient();
            byte[] archive = http.Send(new HttpRequestMessage(HttpMethod.Get, url)).Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            var extracted = TarExtraction.ExtractFiles(archive);
            json = extracted.Json;
            binary = extracted.Binary;

            if (json is not null)
            {
                TryCacheContent(jsonCachePath, () => File.WriteAllText(jsonCachePath, json));
            }

            if (binary is not null)
            {
                TryCacheContent(binaryCachePath, () => File.WriteAllBytes(binaryCachePath, binary));
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            if (File.Exists(jsonCachePath))
                json = File.ReadAllText(jsonCachePath);

            if (File.Exists(binaryCachePath))
                binary = File.ReadAllBytes(binaryCachePath);
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

    private static void TryCacheContent(string cachePath, Action writeOperation)
    {
        try
        {
            writeOperation();
        }
        catch (IOException)
        {
        }
    }

}
