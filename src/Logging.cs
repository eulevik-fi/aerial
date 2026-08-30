using System;
using System.IO;

namespace Aerial;

internal static class Logging
{
    internal static void PrepareLog()
    {
        try
        {
            string logPath = GetLogPath();
            if (!File.Exists(logPath))
                return;

            if (DateTime.Now - File.GetCreationTime(logPath) > TimeSpan.FromMinutes(5))
            {
                File.Delete(logPath);
                File.WriteAllText(logPath, string.Empty);
            }
        }
        catch (IOException)
        {
        }
    }

    internal static void Log(string message)
    {
        try
        {
            string logPath = GetLogPath();
            File.AppendAllText(logPath, $"{DateTime.Now:yyyy/MM/dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Logging must never break playback.
        }
    }

    private static string GetLogPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Aerial");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "aerial-log.txt");
    }
}
