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

    public static void Download(string url, string cacheFileName)
    {
        Directory.CreateDirectory(CacheDirectory);
        string cachePath = Path.Combine(CacheDirectory, cacheFileName);
        string cacheDirectory = Path.GetDirectoryName(cachePath)!;
        string markerPath = Path.Combine(cacheDirectory, "downloaded.txt");
        Directory.CreateDirectory(cacheDirectory);

        if (File.Exists(markerPath) && File.Exists(cachePath))
            return;

        try
        {
            using var http = CreateHttpClient();
            if (TarExtraction.IsTarUrl(url))
            {
                byte[] archive = DownloadBytes(http, url);
                string? json = TarExtraction.ExtractFiles(archive).Json;
                if (json is not null)
                {
                    File.WriteAllText(cachePath, json);
                    File.WriteAllText(markerPath, string.Empty);
                }
            }
            else
            {
                File.WriteAllText(cachePath, DownloadString(http, url));
                File.WriteAllText(markerPath, string.Empty);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
        }
    }

    /// <summary>Downloads and caches both JSON and binary files from a tar archive.</summary>
    public static void DownloadTar(string url, string jsonCacheFileName, string binaryCacheFileName)
    {
        Directory.CreateDirectory(CacheDirectory);
        string jsonCachePath = Path.Combine(CacheDirectory, jsonCacheFileName);
        string binaryCachePath = Path.Combine(CacheDirectory, binaryCacheFileName);
        string cacheDirectory = Path.GetDirectoryName(jsonCachePath)!;
        string markerPath = Path.Combine(cacheDirectory, "downloaded.txt");
        Directory.CreateDirectory(cacheDirectory);

        if (File.Exists(markerPath) && File.Exists(jsonCachePath) && File.Exists(binaryCachePath))
            return;

        try
        {
            using var http = CreateHttpClient();
            byte[] archive = DownloadBytes(http, url);
            var extracted = TarExtraction.ExtractFiles(archive);

            if (extracted.Json is not null)
                File.WriteAllText(jsonCachePath, extracted.Json);

            if (extracted.Binary is not null)
                File.WriteAllBytes(binaryCachePath, extracted.Binary);

            if (extracted.Json is not null && extracted.Binary is not null)
                File.WriteAllText(markerPath, string.Empty);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
        }
    }

    private static byte[] DownloadBytes(HttpClient http, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = http.Send(request);
        response.EnsureSuccessStatusCode();
        return response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
    }

    private static string DownloadString(HttpClient http, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = http.Send(request);
        response.EnsureSuccessStatusCode();
        return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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

}
