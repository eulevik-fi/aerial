using System;
using Microsoft.Win32;

namespace Aerial;

public static class RegistryPowerReader
{
    private const string REGISTRY_PATH = @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes";
    private const string SUB_VIDEO = "7516b95f-f776-4464-8c53-06167f40cc99";
    private const string VIDEOIDLE = "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e";

    public static int GetRegistryMonitorTimeoutInSeconds()
    {
        try
        {
            using RegistryKey? schemesKey = Registry.LocalMachine.OpenSubKey(REGISTRY_PATH);
            if (schemesKey == null)
            {
                return 0;
            }

            string? activeSchemeGuid = schemesKey.GetValue("ActivePowerScheme")?.ToString();
            if (string.IsNullOrEmpty(activeSchemeGuid))
            {
                return 0;
            }

            string videoTimeoutPath = $@"{REGISTRY_PATH}\{activeSchemeGuid}\{SUB_VIDEO}\{VIDEOIDLE}";

            using RegistryKey? timeoutKey = Registry.LocalMachine.OpenSubKey(videoTimeoutPath);
            if (timeoutKey == null)
            {
                return 0;
            }

            object? acValue = timeoutKey.GetValue("ACSettingIndex");
            if (acValue != null)
            {
                return Convert.ToInt32(acValue);
            }
        }
        catch (Exception ex)
        {
            Logging.Log($"[RegistryPowerReader] Failed to read monitor timeout: {ex.GetType().Name}: {ex.Message}");
        }

        return 0;
    }
}
