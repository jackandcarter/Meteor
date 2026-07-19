using System.Diagnostics;

namespace AetherXIV.Launcher.Core;

public sealed record RuntimeValidationResult(
    bool IsReady,
    string Message,
    string? VersionText,
    string PrefixPath,
    string LogPath);

public static class RuntimeValidator
{
    public static Task<RuntimeValidationResult> ValidateAsync(
        ManagedRuntimeInstall install,
        string prefixPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(install);
        WineRuntimeProfile profile = install.ToWineRuntimeProfile(prefixPath);
        return ValidateAsync(profile, prefixPath, cancellationToken);
    }

    public static async Task<RuntimeValidationResult> ValidateAsync(
        WineRuntimeProfile profile,
        string prefixPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Kind == WineRuntimeKind.NativeWindows)
        {
            return new RuntimeValidationResult(
                true,
                "Windows native launch does not require Wine validation.",
                null,
                "",
                RuntimeLaunchDiagnostics.CreateLogPath());
        }

        if (string.IsNullOrWhiteSpace(profile.Command))
            throw new InvalidOperationException("Runtime command is required.");

        if (!File.Exists(profile.Command) && !CommandExistsOnPath(profile.Command))
            throw new FileNotFoundException("Runtime executable was not found.", profile.Command);

        if (profile.Kind == WineRuntimeKind.WhiskyBottle)
        {
            if (!WhiskyRuntimeEnvironment.TryCreateWineProfile(
                    profile.Command,
                    profile.BottleName ?? "",
                    out WineRuntimeProfile whiskyWineProfile,
                    out string whiskyError))
            {
                return new RuntimeValidationResult(
                    false,
                    $"Whisky runtime resolution failed: {whiskyError}",
                    null,
                    $"Whisky:{profile.BottleName}",
                    RuntimeLaunchDiagnostics.CreateLogPath("runtime-validate"));
            }

            return await ValidateAsync(
                whiskyWineProfile,
                whiskyWineProfile.PrefixPath ?? prefixPath,
                cancellationToken);
        }

        string logPath = RuntimeLaunchDiagnostics.CreateLogPath("runtime-validate");
        Dictionary<string, string> environment = new(profile.Environment);
        string runtimeTarget = profile.Name;
        string? normalizedPrefix = null;
        if (profile.Kind == WineRuntimeKind.WinePrefix)
        {
            string selectedPrefix = !string.IsNullOrWhiteSpace(profile.PrefixPath)
                ? profile.PrefixPath
                : prefixPath;
            normalizedPrefix = Path.GetFullPath(selectedPrefix);
            Directory.CreateDirectory(normalizedPrefix);
            environment["WINEPREFIX"] = normalizedPrefix;
            runtimeTarget = normalizedPrefix;
        }
        else if (profile.Kind == WineRuntimeKind.CrossOverBottle)
        {
            if (string.IsNullOrWhiteSpace(profile.BottleName))
            {
                return new RuntimeValidationResult(
                    false,
                    "CrossOver bottle name is required.",
                    null,
                    "CrossOver",
                    logPath);
            }

            environment["CX_BOTTLE"] = profile.BottleName;
            environment.Remove("WINEPREFIX");
            runtimeTarget = $"CrossOver bottle {profile.BottleName}";
        }
        else if (environment.TryGetValue("WINEPREFIX", out string? explicitPrefix)
                 && !string.IsNullOrWhiteSpace(explicitPrefix))
        {
            normalizedPrefix = Path.GetFullPath(explicitPrefix);
            Directory.CreateDirectory(normalizedPrefix);
            environment["WINEPREFIX"] = normalizedPrefix;
            runtimeTarget = normalizedPrefix;
        }

        ProcessRunResult version = await RunAndLogAsync(
            profile.Command,
            "--version",
            environment,
            logPath,
            TimeSpan.FromSeconds(20),
            cancellationToken);
        if (version.ExitCode != 0)
        {
            return new RuntimeValidationResult(
                false,
                $"Runtime version check failed with exit code {version.ExitCode}.",
                version.Output.Trim(),
                runtimeTarget,
                logPath);
        }

        if (normalizedPrefix is null)
        {
            await File.AppendAllTextAsync(
                logPath,
                $"prefix_managed_by_runtime={runtimeTarget}{Environment.NewLine}",
                cancellationToken);
        }
        else if (IsPrefixInitialized(normalizedPrefix))
        {
            await File.AppendAllTextAsync(
                logPath,
                $"prefix_already_initialized={normalizedPrefix}{Environment.NewLine}",
                cancellationToken);
        }
        else
        {
            (string winebootCommand, string winebootArguments) = ResolveWinebootCommand(profile.Command);
            ProcessRunResult winebootResult = await RunAndLogAsync(
                winebootCommand,
                winebootArguments,
                environment,
                logPath,
                TimeSpan.FromSeconds(90),
                cancellationToken);
            if (winebootResult.ExitCode != 0)
            {
                return new RuntimeValidationResult(
                    false,
                    $"Prefix setup failed with exit code {winebootResult.ExitCode}.",
                    version.Output.Trim(),
                    runtimeTarget,
                    logPath);
            }

            string wineserver = ResolveSiblingTool(profile.Command, "wineserver");
            if (File.Exists(wineserver) || CommandExistsOnPath(wineserver))
            {
                await RunAndLogAsync(
                    wineserver,
                    "-w",
                    environment,
                    logPath,
                    TimeSpan.FromSeconds(30),
                    cancellationToken);
            }
        }

        string? helperPath = ClientLaunchHelperLocator.Find();
        if (!string.IsNullOrWhiteSpace(helperPath))
        {
            ProcessRunResult helperProbe = await RunAndLogAsync(
                profile.Command,
                profile.BuildArguments(helperPath, "--probe"),
                environment,
                logPath,
                TimeSpan.FromSeconds(30),
                cancellationToken);

            if (helperProbe.ExitCode != 0)
            {
                return new RuntimeValidationResult(
                    false,
                    "Runtime cannot start the bundled FFXIV 1.x launch helper. Select a Wine/CrossOver runtime with Windows .NET support or use a self-contained helper package.",
                    version.Output.Trim(),
                    runtimeTarget,
                    logPath);
            }
        }

        return new RuntimeValidationResult(
            true,
            "Runtime, Wine prefix, and client launch helper are ready.",
            version.Output.Trim(),
            runtimeTarget,
            logPath);
    }

    private static async Task<ProcessRunResult> RunAndLogAsync(
        string command,
        string arguments,
        IReadOnlyDictionary<string, string> environment,
        string logPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await File.AppendAllTextAsync(logPath, $"$ {command} {arguments}{Environment.NewLine}", cancellationToken);

        ProcessStartInfo startInfo = new()
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (KeyValuePair<string, string> pair in environment)
            startInfo.Environment[pair.Key] = pair.Value;

        using Process process = new() { StartInfo = startInfo };
        process.Start();

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
                // The process may have exited between timeout and kill.
            }

            throw new TimeoutException($"Runtime command timed out: {command} {arguments}");
        }

        string output = await outputTask;
        string error = await errorTask;
        await File.AppendAllTextAsync(
            logPath,
            output + error + $"exit={process.ExitCode}{Environment.NewLine}",
            cancellationToken);

        return new ProcessRunResult(process.ExitCode, output, error);
    }

    private static string ResolveSiblingTool(string command, string toolName)
    {
        string? directory = Path.GetDirectoryName(command);
        if (string.IsNullOrWhiteSpace(directory))
            return toolName;

        string candidate = Path.Combine(directory, toolName);
        return File.Exists(candidate) ? candidate : toolName;
    }

    private static (string Command, string Arguments) ResolveWinebootCommand(string wineCommand)
    {
        string? directory = Path.GetDirectoryName(wineCommand);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            string sibling = Path.Combine(directory, "wineboot");
            if (File.Exists(sibling))
                return (sibling, "-u");
        }

        if (CommandExistsOnPath("wineboot"))
            return ("wineboot", "-u");

        return (wineCommand, "wineboot -u");
    }

    private static bool IsPrefixInitialized(string prefixPath)
    {
        return File.Exists(Path.Combine(prefixPath, "system.reg"))
            && File.Exists(Path.Combine(prefixPath, "user.reg"));
    }

    private static bool CommandExistsOnPath(string command)
    {
        if (command.Contains(Path.DirectorySeparatorChar)
            || command.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Split(Path.PathSeparator).Any(directory =>
            File.Exists(Path.Combine(directory, command)));
    }

    private sealed record ProcessRunResult(int ExitCode, string Output, string Error);
}
