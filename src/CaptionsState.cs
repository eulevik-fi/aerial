using System;
using System.IO;

namespace Aerial;

internal static class CaptionsState
{
    private const string FileName = "use-captions.txt";

    /// <summary>Whether subtitles are currently displayed across all players.</summary>
    internal static bool SubtitlesShown { get; private set; }

    private static string GetFullPathName()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aerial");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, FileName);
    }

    public static void Initialize()
    {
        SubtitlesShown = File.Exists(GetFullPathName());
    }

    public static bool ToggleSubtitles()
    {
        SubtitlesShown = !SubtitlesShown;
        PersistSubtitleState(SubtitlesShown);
        return SubtitlesShown;
    }

    private static void PersistSubtitleState(bool subtitlesShown)
    {
        string path = GetFullPathName();
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
