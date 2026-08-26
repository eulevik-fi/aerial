using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Aerial;

/// <summary>
/// Standalone app variant: shows the same full-screen red screensaver surface
/// on every attached display and loads the video catalog, but without any of
/// the /s /c /p screensaver plumbing.
/// </summary>
internal static class AerialApp
{
    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();

        // Fetch (or refresh) the video catalog before showing anything.
        Videos.InitializeAsync().GetAwaiter().GetResult();

        using var idleTracker = new IdleExitTracker();

        var forms = new List<ScreensaverForm>();
        foreach (Screen screen in Screen.AllScreens)
        {
            forms.Add(new ScreensaverForm(screen));
        }

        foreach (var form in forms)
        {
            form.Show();
        }

        idleTracker.Start();

        Application.Run();

        foreach (var form in forms)
        {
            form.Dispose();
        }

        return 0;
    }
}
