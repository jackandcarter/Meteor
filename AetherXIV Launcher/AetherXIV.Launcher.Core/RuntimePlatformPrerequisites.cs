using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AetherXIV.Launcher.Core;

public sealed record RuntimePrerequisiteResult(
    bool IsReady,
    string Message,
    IReadOnlyList<string> MissingLibraries,
    IReadOnlyList<string> Warnings);

public static class RuntimePlatformPrerequisites
{
    private sealed record ProcessResult(int ExitCode, string Output, string Error, bool TimedOut);

    private static readonly string[] LinuxDriverNames =
    [
        "winex11.drv.so",
        "winealsa.drv.so",
        "winepulse.drv.so",
        "winegstreamer.so",
        "winevulkan.so"
    ];

    public static async Task<RuntimePrerequisiteResult> CheckAsync(
        string wineCommand,
        CancellationToken cancellationToken = default)
    {
        if (OperatingSystem.IsMacOS() && RuntimeInformation.OSArchitecture == Architecture.Arm64)
            return await CheckRosettaAsync(wineCommand, cancellationToken);

        if (OperatingSystem.IsLinux())
            return await CheckLinuxLibrariesAsync(wineCommand, cancellationToken);

        return Ready();
    }

    public static bool RequiresRosetta(LauncherPlatform platform, Architecture architecture)
    {
        return platform.OperatingSystem == LauncherOperatingSystem.MacOS
            && architecture == Architecture.Arm64;
    }

    public static IReadOnlyList<string> ParseMissingLinuxLibraries(string output)
    {
        HashSet<string> missing = new(StringComparer.Ordinal);
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int marker = line.IndexOf("=> not found", StringComparison.Ordinal);
            if (marker <= 0)
                continue;

            string library = line[..marker].Trim();
            if (!string.IsNullOrWhiteSpace(library))
                missing.Add(library);
        }

        return missing.Order(StringComparer.Ordinal).ToArray();
    }

    public static string LinuxDependencyGuidance(string? osReleaseText)
    {
        Dictionary<string, string> values = ParseOsRelease(osReleaseText);
        string id = values.GetValueOrDefault("ID") ?? "";
        string idLike = values.GetValueOrDefault("ID_LIKE") ?? "";
        string family = $"{id} {idLike}".ToLowerInvariant();

        if (family.Contains("debian", StringComparison.Ordinal) || family.Contains("ubuntu", StringComparison.Ordinal))
            return "Install the reported libraries from Software Center, or use your distribution's apt packages, then select Validate Runtime again.";

        if (family.Contains("arch", StringComparison.Ordinal) || id == "steamos")
            return "Install the reported libraries in a persistent SteamOS/Arch environment, then select Validate Runtime again.";

        if (family.Contains("fedora", StringComparison.Ordinal) || family.Contains("rhel", StringComparison.Ordinal))
            return "Install the reported libraries with your distribution's software manager, then select Validate Runtime again.";

        return "Install the reported shared libraries with your distribution's software manager, then select Validate Runtime again.";
    }

    private static async Task<RuntimePrerequisiteResult> CheckRosettaAsync(
        string wineCommand,
        CancellationToken cancellationToken)
    {
        ProcessResult result = await ProbeRosettaAsync(cancellationToken);
        if (result.ExitCode != 0)
        {
            string? appBundle = FindContainingAppBundle(wineCommand);
            if (appBundle is not null)
            {
                ProcessResult openResult = await RunAsync(
                    "/usr/bin/open",
                    ["-n", appBundle, "--args", "--version"],
                    TimeSpan.FromSeconds(30),
                    cancellationToken,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["WINEDEBUG"] = "-all",
                        ["WINEPREFIX"] = RuntimeInstallStore.ManagedPrefixPath
                    });
                if (openResult.ExitCode == 0)
                {
                    DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(10);
                    while (DateTimeOffset.UtcNow < deadline)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                        result = await ProbeRosettaAsync(cancellationToken);
                        if (result.ExitCode == 0)
                            break;
                    }

                    if (result.ExitCode != 0)
                        result = result with { TimedOut = true };
                }
                else
                {
                    result = openResult;
                }
            }
        }

        if (result.ExitCode == 0)
        {
            List<string> warnings = [];
            if (!Directory.Exists("/Library/Frameworks/GStreamer.framework"))
            {
                warnings.Add(
                    "Optional macOS multimedia support is not installed. The game can launch without GStreamer, but some movies or media may not play.");
            }

            return new RuntimePrerequisiteResult(
                true,
                "Rosetta is ready for the Intel Wine runtime.",
                Array.Empty<string>(),
                warnings);
        }

        string detail = result.TimedOut
            ? "The Rosetta installation prompt was not completed within ten minutes."
            : FirstUsefulLine(result.Error, result.Output);
        return new RuntimePrerequisiteResult(
            false,
            $"Rosetta is required on Apple silicon. Complete the macOS installation prompt and select Validate Runtime again. {detail}".Trim(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static Task<ProcessResult> ProbeRosettaAsync(CancellationToken cancellationToken)
    {
        return RunAsync(
            "/usr/bin/arch",
            ["-x86_64", "/usr/bin/true"],
            TimeSpan.FromSeconds(15),
            cancellationToken);
    }

    private static string? FindContainingAppBundle(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return null;

        DirectoryInfo? directory = File.Exists(command)
            ? new FileInfo(command).Directory
            : null;
        while (directory is not null)
        {
            if (directory.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return directory.FullName;
            directory = directory.Parent;
        }

        return null;
    }

    private static async Task<RuntimePrerequisiteResult> CheckLinuxLibrariesAsync(
        string wineCommand,
        CancellationToken cancellationToken)
    {
        string? ldd = File.Exists("/usr/bin/ldd") ? "/usr/bin/ldd" : FindCommandOnPath("ldd");
        if (ldd is null)
        {
            return new RuntimePrerequisiteResult(
                false,
                "The Linux ldd utility is required to check Wine's host libraries.",
                Array.Empty<string>(),
                Array.Empty<string>());
        }

        List<string> targets = FindLinuxRuntimeTargets(wineCommand);
        HashSet<string> missing = new(StringComparer.Ordinal);
        foreach (string target in targets)
        {
            ProcessResult result = await RunAsync(ldd, [target], TimeSpan.FromSeconds(15), cancellationToken);
            foreach (string library in ParseMissingLinuxLibraries($"{result.Output}\n{result.Error}"))
                missing.Add(library);
        }

        if (missing.Count == 0)
            return Ready("Required Linux host libraries are available.");

        string osRelease = File.Exists("/etc/os-release")
            ? await File.ReadAllTextAsync("/etc/os-release", cancellationToken)
            : "";
        string[] missingList = missing.Order(StringComparer.Ordinal).ToArray();
        return new RuntimePrerequisiteResult(
            false,
            $"Wine is installed, but Linux is missing: {string.Join(", ", missingList)}. {LinuxDependencyGuidance(osRelease)}",
            missingList,
            Array.Empty<string>());
    }

    private static List<string> FindLinuxRuntimeTargets(string wineCommand)
    {
        List<string> targets = [];
        string resolvedCommand = File.Exists(wineCommand)
            ? Path.GetFullPath(wineCommand)
            : FindCommandOnPath(wineCommand) ?? wineCommand;
        if (File.Exists(resolvedCommand))
            targets.Add(resolvedCommand);

        string? binDirectory = Path.GetDirectoryName(resolvedCommand);
        if (string.IsNullOrWhiteSpace(binDirectory))
            return targets;

        string wineserver = Path.Combine(binDirectory, "wineserver");
        if (File.Exists(wineserver))
            targets.Add(wineserver);

        DirectoryInfo? root = Directory.GetParent(binDirectory);
        if (root is null || !root.Exists)
            return targets;

        HashSet<string> wanted = new(LinuxDriverNames, StringComparer.Ordinal);
        try
        {
            foreach (string library in Directory.EnumerateFiles(root.FullName, "*.so", SearchOption.AllDirectories))
            {
                if (wanted.Contains(Path.GetFileName(library)))
                    targets.Add(library);
            }
        }
        catch (UnauthorizedAccessException)
        {
            // The executable and wineserver still provide a useful minimum check.
        }

        return targets.Distinct(StringComparer.Ordinal).ToList();
    }

    private static async Task<ProcessResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);
        if (environment is not null)
        {
            foreach (KeyValuePair<string, string> pair in environment)
                startInfo.Environment[pair.Key] = pair.Value;
        }

        using Process process = new() { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new ProcessResult(-1, "", ex.Message, false);
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        Task waitTask = process.WaitForExitAsync(cancellationToken);
        Task completed = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken));
        if (completed != waitTask)
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // The process may have exited while the timeout was handled.
            }

            return new ProcessResult(-1, await outputTask, await errorTask, true);
        }

        return new ProcessResult(process.ExitCode, await outputTask, await errorTask, false);
    }

    private static RuntimePrerequisiteResult Ready(string message = "Platform runtime prerequisites are ready") =>
        new(true, message, Array.Empty<string>(), Array.Empty<string>());

    private static string FirstUsefulLine(params string[] values)
    {
        foreach (string value in values)
        {
            string? line = value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(line))
                return line;
        }

        return "macOS did not confirm that Rosetta is available.";
    }

    private static Dictionary<string, string> ParseOsRelease(string? text)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = line.IndexOf('=');
            if (separator <= 0)
                continue;

            values[line[..separator]] = line[(separator + 1)..].Trim().Trim('"', '\'');
        }

        return values;
    }

    private static string? FindCommandOnPath(string command)
    {
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            string candidate = Path.Combine(directory, command);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
