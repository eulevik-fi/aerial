using System;
using System.Collections.Generic;
using System.Linq;

namespace Aerial;

internal static class SrtGeneration
{
    public static string GenerateFromPointsOfInterest(IReadOnlyDictionary<int, string> pointsOfInterest)
    {
        var poiEntries = pointsOfInterest
            .OrderBy(kvp => kvp.Key)
            .ToList();

        if (poiEntries.Count == 0)
            throw new InvalidOperationException("No points of interest were provided.");

        var lines = new List<string>();

        for (int i = 0; i < poiEntries.Count; i++)
        {
            var current = poiEntries[i];
            var nextStart = (i + 1 < poiEntries.Count) ? poiEntries[i + 1].Key : 10 * 60 * 60;
            var text = current.Value.Trim();

            if (string.IsNullOrWhiteSpace(text))
                continue;

            int startSeconds = current.Key;
            int endSeconds = CalculateEndSeconds(startSeconds, nextStart);

            string startTime = TimeSpan.FromSeconds(startSeconds).ToString(@"hh\:mm\:ss\,fff");
            string endTime = TimeSpan.FromSeconds(endSeconds).ToString(@"hh\:mm\:ss\,fff");
            lines.Add($"1{Environment.NewLine}{startTime} --> {endTime}{Environment.NewLine}{text}");
        }

        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    private static int CalculateEndSeconds(int startSeconds, int nextStart)
    {
        // Use nextStart if it's after the current subtitle, otherwise add 1 second
        return nextStart > startSeconds ? nextStart : startSeconds + 1;
    }

    public static string GenerateFromDescription(string description)
    {
        return $@"1
00:00:00,000 --> 10:00:00,000
{description}";
    }

    public static string GenerateHelp()
    {
        return @"1
00:00:00,000 --> 00:00:03,000
Tap Shift for captions...";
    }
}
