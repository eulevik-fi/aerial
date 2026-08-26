using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace Aerial;

internal static class Program
{
    // Prevents multiple instances of the screensaver from running at once,
    // which can happen when Windows launches it on several monitors.
    private static Mutex? _mutex;

    [STAThread]
    private static int Main(string[] args)
    {
        _mutex = new Mutex(true, "Local\\Aerial-Screensaver-Mutex", out bool firstInstance);

        ApplicationConfiguration.Initialize();

        string arg = args.Length > 0 ? args[0].ToLowerInvariant() : string.Empty;

        try
        {
            switch (arg)
            {
                case "/s":
                case "-s":
                    if (!firstInstance)
                        return 0;
                    RunFullScreen();
                    return 0;

                case "/c":
                case "-c":
                    ShowOptions();
                    return 0;

                case "/p":
                case "-p":
                    if (args.Length >= 2 && long.TryParse(args[1], out long hwndValue))
                    {
                        ShowPreview(new IntPtr(hwndValue));
                    }
                    return 0;

                default:
                    // No recognized argument: behave like the configure dialog.
                    ShowOptions();
                    return 0;
            }
        }
        finally
        {
            _mutex.Dispose();
        }
    }

    /// <summary>/s - run one full-screen form per attached display.</summary>
    private static void RunFullScreen()
    {
        // Fetch (or refresh) the video catalog before showing anything.
        Videos.InitializeAsync().GetAwaiter().GetResult();

        using var idleTracker = new IdleExitTracker();

        var forms = new List<ScreensaverForm>();
        foreach (Screen screen in Screen.AllScreens)
        {
            var form = new ScreensaverForm(screen);
            forms.Add(form);
        }

        // Show all forms before entering the message loop so every display
        // paints its red surface simultaneously.
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
    }

    /// <summary>/c - configuration dialog.</summary>
    private static void ShowOptions()
    {
        MessageBox.Show(
            "No options... yet.",
            "Aerial Screensaver",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    /// <summary>/p &lt;hwnd&gt; - render inside the little preview window of the
    /// Windows screensaver settings dialog.</summary>
    private static void ShowPreview(IntPtr parentHwnd)
    {
        if (parentHwnd == IntPtr.Zero)
            return;

        using var preview = new PreviewForm(parentHwnd);
        Application.Run(preview);
    }
}
