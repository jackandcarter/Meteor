using System.Runtime.InteropServices;

namespace AetherXIV.Launcher.Core;

public enum LauncherOperatingSystem
{
    Windows,
    MacOS,
    Linux,
    Unknown
}

public sealed record LauncherPlatform(LauncherOperatingSystem OperatingSystem, string RuntimeIdentifier)
{
    public static LauncherPlatform Current => Detect();

    public bool RequiresCompatibilityRuntime => OperatingSystem is LauncherOperatingSystem.MacOS or LauncherOperatingSystem.Linux;

    public bool UsesNativeWindowsClient => OperatingSystem == LauncherOperatingSystem.Windows;

    public static LauncherPlatform Detect()
    {
        Architecture architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
        string architectureId = architecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => architecture.ToString().ToLowerInvariant()
        };

        if (System.OperatingSystem.IsWindows())
            return new LauncherPlatform(LauncherOperatingSystem.Windows, $"win-{architectureId}");

        if (System.OperatingSystem.IsMacOS())
            return new LauncherPlatform(LauncherOperatingSystem.MacOS, $"osx-{architectureId}");

        if (System.OperatingSystem.IsLinux())
            return new LauncherPlatform(LauncherOperatingSystem.Linux, $"linux-{architectureId}");

        return new LauncherPlatform(LauncherOperatingSystem.Unknown, $"unknown-{architectureId}");
    }
}
