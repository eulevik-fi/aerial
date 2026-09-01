using System;
using System.Collections.Generic;
using System.IO;
using LibVLCSharp.Shared;

namespace Aerial;

internal static class VLCManager
{
    private static LibVLC? _instance;

    public static LibVLC Instance => _instance
        ?? throw new InvalidOperationException(
            "Call VLCManager.Initialize() before accessing the LibVLC instance.");

    public static void Initialize()
    {
        if (_instance is not null)
            return;

        Logging.Log("Initializing LibVLC core...");

        var libDir = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");
        if (Directory.Exists(libDir))
        {
            Logging.Log($"Native libvlc found at {libDir}");
        }
        else
        {
            Logging.Log($"WARNING: native libvlc directory missing: {libDir}");
        }

        Core.Initialize(libDir);

        var vlcOptions = new List<string>
        {
            // Be a good citizen
            "--no-disable-screensaver",

            // There are options to disable unused features (e.g. audio),
            // but they made no difference to startup time.

            // Caption configuration
            "--freetype-rel-fontsize=48",
            "--subsdec-align=9",
            "--sub-margin=30",
            "--freetype-font=Segoe UI Semibold",
            "--freetype-opacity=255",
            "--freetype-outline-thickness=1",
            "--freetype-outline-opacity=0",
            "--freetype-shadow-color=0",
            "--freetype-shadow-opacity=140",
            "--freetype-shadow-distance=0.07",
            "--freetype-shadow-angle=-45.0"
        };

        _instance = new LibVLC(enableDebugLogs: false, vlcOptions.ToArray());
        Logging.Log("LibVLC core initialized.");
    }
}