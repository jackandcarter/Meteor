namespace AetherXIV.Launcher.Core;

public sealed record RuntimeSetupGuidance(string Title, string Summary, Uri GuideUri)
{
    private const string MacOSGuideUrl = "https://wiki.winehq.org/MacOS";
    private const string DebianUbuntuGuideUrl = "https://gitlab.winehq.org/wine/wine/-/wikis/Debian-Ubuntu";
    private const string WineDownloadUrl = "https://www.winehq.org/download";
    private const string SteamOsGuideUrl = "https://github.com/jackandcarter/AetherXIV/blob/2.0/docs/setup/STEAMOS.md";

    public static RuntimeSetupGuidance ForCurrentPlatform() => ForPlatform(LauncherPlatform.Current);

    public static RuntimeSetupGuidance ForPlatform(
        LauncherPlatform platform,
        Func<string, string?>? readTextFile = null)
    {
        ArgumentNullException.ThrowIfNull(platform);

        if (platform.OperatingSystem == LauncherOperatingSystem.Windows)
        {
            return new RuntimeSetupGuidance(
                "Windows native runtime",
                "Windows launches the FFXIV client directly and does not require Wine.",
                new Uri(WineDownloadUrl));
        }

        if (platform.OperatingSystem == LauncherOperatingSystem.MacOS)
        {
            return new RuntimeSetupGuidance(
                "Set up Wine for macOS",
                "Install a trusted Wine build that supports 32-bit Windows applications. Apple-silicon Macs may also require Rosetta. Return here, scan runtimes, and validate before launching.",
                new Uri(MacOSGuideUrl));
        }

        if (platform.OperatingSystem == LauncherOperatingSystem.Linux)
        {
            string osRelease = ReadOsRelease(readTextFile);
            if (IsSteamOs(osRelease))
            {
                return new RuntimeSetupGuidance(
                    "Set up Wine for SteamOS",
                    "Use a runtime stored in persistent user storage; avoid modifying SteamOS's read-only base image. Return here, scan runtimes, and validate before launching.",
                    new Uri(SteamOsGuideUrl));
            }

            if (IsDebianFamily(osRelease))
            {
                return new RuntimeSetupGuidance(
                    "Set up Wine for Linux",
                    "Follow the WineHQ instructions for your exact Debian or Ubuntu release, including 32-bit support where required. Return here, scan runtimes, and validate before launching.",
                    new Uri(DebianUbuntuGuideUrl));
            }

            return new RuntimeSetupGuidance(
                "Set up Wine for Linux",
                "Install Wine through your distribution's supported method with 32-bit client and graphics support. Return here, scan runtimes, and validate before launching.",
                new Uri(WineDownloadUrl));
        }

        return new RuntimeSetupGuidance(
            "Set up a compatibility runtime",
            "Install a Wine-compatible runtime for this platform, then scan and validate it before launching.",
            new Uri(WineDownloadUrl));
    }

    private static string ReadOsRelease(Func<string, string?>? readTextFile)
    {
        try
        {
            return (readTextFile ?? File.ReadAllText)("/etc/os-release") ?? "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    private static bool IsSteamOs(string osRelease)
    {
        return HasReleaseValue(osRelease, "ID", "steamos")
            || HasReleaseValue(osRelease, "ID_LIKE", "steamos");
    }

    private static bool IsDebianFamily(string osRelease)
    {
        return HasReleaseValue(osRelease, "ID", "debian")
            || HasReleaseValue(osRelease, "ID", "ubuntu")
            || HasReleaseValue(osRelease, "ID_LIKE", "debian")
            || HasReleaseValue(osRelease, "ID_LIKE", "ubuntu");
    }

    private static bool HasReleaseValue(string osRelease, string key, string expected)
    {
        foreach (string line in osRelease.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = line.IndexOf('=');
            if (separator <= 0 || !line[..separator].Equals(key, StringComparison.OrdinalIgnoreCase))
                continue;

            string value = line[(separator + 1)..].Trim().Trim('"', '\'');
            if (value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains(expected, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
