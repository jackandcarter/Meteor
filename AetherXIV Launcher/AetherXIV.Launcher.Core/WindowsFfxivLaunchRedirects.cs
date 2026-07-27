using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace AetherXIV.Launcher.Core;

public sealed record WindowsFfxivLaunchRedirect(
    string ExecutableName,
    string RegistryView,
    string RegistryPath,
    string DebuggerCommand);

public static class WindowsFfxivLaunchRedirects
{
    public const string RepairCommand = "--remove-novum-ffxiv-launch-redirects";

    private const string ImageExecutionOptionsSubKey =
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options";

    private static readonly string[] TargetExecutableNames =
    [
        "ffxivboot.exe",
        "ffxivlogin.exe",
        "ffxivgame.exe"
    ];

    public static IReadOnlyList<string> FfxivExecutableNames => TargetExecutableNames;

    public static bool IsNovumDebuggerCommand(string? debuggerCommand)
    {
        if (string.IsNullOrWhiteSpace(debuggerCommand))
            return false;

        return Regex.IsMatch(
            debuggerCommand,
            @"(?:^|[\\/])NovumLauncher\.exe(?:[""\s]|$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<WindowsFfxivLaunchRedirect> FindNovumRedirects()
    {
        List<WindowsFfxivLaunchRedirect> redirects = [];
        foreach (RegistryView view in GetRegistryViews())
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            foreach (string executableName in TargetExecutableNames)
            {
                string subKeyPath = $@"{ImageExecutionOptionsSubKey}\{executableName}";
                using RegistryKey? key = baseKey.OpenSubKey(subKeyPath, writable: false);
                object? rawValue = key?.GetValue(
                    "Debugger",
                    null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames);
                string? debuggerCommand = rawValue as string;
                if (!IsNovumDebuggerCommand(debuggerCommand))
                    continue;

                redirects.Add(new WindowsFfxivLaunchRedirect(
                    executableName,
                    FormatRegistryView(view),
                    FormatRegistryPath(view, executableName),
                    debuggerCommand!));
            }
        }

        return redirects;
    }

    [SupportedOSPlatform("windows")]
    public static int RemoveNovumRedirects()
    {
        int removed = 0;
        foreach (RegistryView view in GetRegistryViews())
        {
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            foreach (string executableName in TargetExecutableNames)
            {
                string subKeyPath = $@"{ImageExecutionOptionsSubKey}\{executableName}";
                using RegistryKey? key = baseKey.OpenSubKey(subKeyPath, writable: true);
                object? rawValue = key?.GetValue(
                    "Debugger",
                    null,
                    RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (!IsNovumDebuggerCommand(rawValue as string))
                    continue;

                key!.DeleteValue("Debugger", throwOnMissingValue: false);
                removed++;
            }
        }

        return removed;
    }

    [SupportedOSPlatform("windows")]
    private static IEnumerable<RegistryView> GetRegistryViews()
    {
        if (Environment.Is64BitOperatingSystem)
            yield return RegistryView.Registry64;

        yield return RegistryView.Registry32;
    }

    [SupportedOSPlatform("windows")]
    private static string FormatRegistryView(RegistryView view) =>
        view == RegistryView.Registry32 ? "32-bit" : "64-bit";

    [SupportedOSPlatform("windows")]
    private static string FormatRegistryPath(RegistryView view, string executableName)
    {
        string wow6432Node = Environment.Is64BitOperatingSystem && view == RegistryView.Registry32
            ? @"WOW6432Node\"
            : "";
        return $@"HKEY_LOCAL_MACHINE\SOFTWARE\{wow6432Node}Microsoft\Windows NT\CurrentVersion\Image File Execution Options\{executableName}";
    }
}
