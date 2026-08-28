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

    public async Task<string?> DownloadAsync(string url, string cacheFileName)
    {
        Directory.CreateDirectory(CacheDirectory);
        string cachePath = Path.Combine(CacheDirectory, cacheFileName);
        string? content = null;

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Aerial-Screensaver/1.0");
            content = await http.GetStringAsync(url).ConfigureAwait(false);

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
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (File.Exists(cachePath))
                content = await File.ReadAllTextAsync(cachePath).ConfigureAwait(false);
        }

        return content;
    }
}
