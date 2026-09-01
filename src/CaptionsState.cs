using System;
using System.IO;

namespace Aerial;

internal static class CaptionsState
{
    public static string GetUseCaptionsPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aerial");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "use-captions.txt");
    }

    public static void SyncSubtitleStateFromDisk()
    {
        VideoPlayer._subtitlesShown = File.Exists(GetUseCaptionsPath());
    }

    public static void PersistSubtitleState(bool subtitlesShown)
    {
        string path = GetUseCaptionsPath();
        if (subtitlesShown)
        {
            File.WriteAllText(path, "1");
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
    }
}
