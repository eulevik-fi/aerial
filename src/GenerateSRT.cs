using System;
using System.Collections.Generic;
using System.Linq;

namespace Aerial;

internal static class GenerateSrt
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
            int endSeconds = Math.Max(startSeconds, nextStart);
            if (endSeconds <= startSeconds)
            {
                endSeconds = startSeconds + 1;
            }

            string startTime = TimeSpan.FromSeconds(startSeconds).ToString(@"hh\:mm\:ss\,fff");
            string endTime = TimeSpan.FromSeconds(endSeconds).ToString(@"hh\:mm\:ss\,fff");
            lines.Add($"1\n{startTime} --> {endTime}\n{text}");
        }

        return string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    public static string GenerateFromDescription(string description)
    {
        return $@"1
00:00:00,000 --> 10:00:00,000
{description}";
    }
}
