using System;
using System.Collections.Generic;
using Claunia.PropertyList;

namespace Aerial;

/// <summary>
/// Parses the TVIdleScreenStrings plist file and provides localized string lookups.
/// </summary>
internal sealed class LocalizableStrings
{
    private readonly Dictionary<string, string> _localizations;

    public LocalizableStrings(string plistFilePath)
    {
        _localizations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(plistFilePath) || !File.Exists(plistFilePath))
            return;

        try
        {
            byte[] plistData = File.ReadAllBytes(plistFilePath);
            if (plistData.Length == 0)
                return;

            NSObject parsed = PropertyListParser.Parse(plistData);
            ExtractStrings(parsed);
        }
        catch (Exception ex)
        {
            Logging.Log($"Failed to parse plist: {ex.Message}");
        }
    }

    /// <summary>
    /// Look up a localization key and return its value.
    /// Returns null if the key is not found.
    /// </summary>
    public string? GetDescription(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return _localizations.TryGetValue(key, out var value) ? value : null;
    }

    private void ExtractStrings(NSObject obj)
    {
        if (obj is NSDictionary dict)
        {
            foreach (var kvp in dict)
            {
                if (kvp.Value is NSString str)
                {
                    _localizations[kvp.Key] = str.ToString();
                }
                else
                {
                    ExtractStrings(kvp.Value);
                }
            }
        }
        else if (obj is NSArray arr)
        {
            foreach (var item in arr)
            {
                ExtractStrings(item);
            }
        }
    }
}
