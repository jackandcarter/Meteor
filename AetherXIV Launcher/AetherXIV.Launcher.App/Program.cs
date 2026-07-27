using Avalonia;
using AetherXIV.Launcher.Core;

namespace AetherXIV.Launcher.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (OperatingSystem.IsWindows()
            && args.Length == 1
            && string.Equals(
                args[0],
                WindowsFfxivLaunchRedirects.RepairCommand,
                StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                WindowsFfxivLaunchRedirects.RemoveNovumRedirects();
                return WindowsFfxivLaunchRedirects.FindNovumRedirects().Count == 0 ? 0 : 2;
            }
            catch
            {
                return 1;
            }
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
