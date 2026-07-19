using System.Diagnostics;

namespace AetherXIV.Launcher.Core;

public sealed record WineRuntimeConfigurationSettings(
    LauncherOperatingSystem OperatingSystem,
    bool UsePrefixLocalDocuments = true);

public sealed record WineRuntimeConfigurationResult(
    bool IsReady,
    string Message,
    string RuntimeTarget,
    string LogPath);

public sealed record WineRegistrySetting(
    string Key,
    string ValueName,
    string Type,
    string Data);

public sealed record WineUserDocumentsTarget(
    string HostDocumentsPath,
    string WindowsDocumentsPath,
    string HostFfxivConfigPath);

public static class WineRuntimeConfigurator
{
    private const string WineExplorerDesktopsKey = @"HKCU\Software\Wine\Explorer\Desktops";
    private const string LegacyDesktopValuePrefix = "EchoGateXIV-";
    public const string MouseWarpOverrideDefault = "enable";

    public static IReadOnlyList<WineRegistrySetting> BuildRegistrySettings(
        WineRuntimeConfigurationSettings settings,
        string? windowsDocumentsPath = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        List<WineRegistrySetting> registrySettings = new()
        {
            new WineRegistrySetting(
                @"HKCU\Software\Wine\DirectInput",
                "MouseWarpOverride",
                "REG_SZ",
                MouseWarpOverrideDefault),
        };

        if (!string.IsNullOrWhiteSpace(windowsDocumentsPath))
        {
            registrySettings.Add(new WineRegistrySetting(
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders",
                "Personal",
                "REG_SZ",
                windowsDocumentsPath));
            registrySettings.Add(new WineRegistrySetting(
                @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders",
                "Personal",
                "REG_EXPAND_SZ",
                windowsDocumentsPath));
        }

        if (settings.OperatingSystem == LauncherOperatingSystem.MacOS)
        {
            registrySettings.Add(new WineRegistrySetting(
                @"HKCU\Software\Wine\Mac Driver",
                "CaptureDisplaysForFullscreen",
                "REG_SZ",
                "y"));
        }
        else if (settings.OperatingSystem == LauncherOperatingSystem.Linux)
        {
            registrySettings.Add(new WineRegistrySetting(
                @"HKCU\Software\Wine\X11 Driver",
                "GrabFullscreen",
                "REG_SZ",
                "Y"));
        }

        return registrySettings;
    }

    public static bool TryCreatePrefixLocalDocuments(
        string prefixPath,
        out WineUserDocumentsTarget target,
        out string error)
    {
        target = new WineUserDocumentsTarget("", "", "");
        error = "";

        if (string.IsNullOrWhiteSpace(prefixPath))
        {
            error = "Wine prefix path is required before configuring FFXIV settings storage.";
            return false;
        }

        string normalizedPrefix = Path.GetFullPath(prefixPath);
        string usersRoot = Path.Combine(normalizedPrefix, "drive_c", "users");
        string userName = ResolveWineUserName(usersRoot);
        string userRoot = Path.Combine(usersRoot, userName);
        string documentsPath = Path.Combine(userRoot, "AetherXIV Documents");
        string ffxivConfigPath = Path.Combine(documentsPath, "My Games", "FINAL FANTASY XIV");

        Directory.CreateDirectory(ffxivConfigPath);

        target = new WineUserDocumentsTarget(
            documentsPath,
            $@"C:\users\{userName}\AetherXIV Documents",
            ffxivConfigPath);
        return true;
    }

    public static string BuildRegAddArguments(WineRegistrySetting setting)
    {
        ArgumentNullException.ThrowIfNull(setting);

        return string.Join(" ", new[]
        {
            "reg",
            "add",
            CommandLineArguments.Quote(setting.Key),
            "/v",
            CommandLineArguments.Quote(setting.ValueName),
            "/t",
            setting.Type,
            "/d",
            CommandLineArguments.Quote(setting.Data),
            "/f"
        });
    }

    public static string BuildRegDeleteValueArguments(string key, string valueName)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Registry key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(valueName))
            throw new ArgumentException("Registry value name is required.", nameof(valueName));

        return string.Join(" ", new[]
        {
            "reg",
            "delete",
            CommandLineArguments.Quote(key),
            "/v",
            CommandLineArguments.Quote(valueName),
            "/f"
        });
    }

    public static async Task<WineRuntimeConfigurationResult> ConfigureAsync(
        WineRuntimeProfile profile,
        string managedPrefixPath,
        WineRuntimeConfigurationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(settings);

        if (profile.Kind == WineRuntimeKind.NativeWindows)
        {
            return new WineRuntimeConfigurationResult(
                true,
                "Windows native launch does not need Wine configuration.",
                "Windows native",
                RuntimeLaunchDiagnostics.CreateLogPath("runtime-config"));
        }

        if (profile.Kind == WineRuntimeKind.WhiskyBottle)
        {
            if (!WhiskyRuntimeEnvironment.TryCreateWineProfile(
                    profile.Command,
                    profile.BottleName ?? "",
                    out WineRuntimeProfile whiskyWineProfile,
                    out string whiskyError))
            {
                return new WineRuntimeConfigurationResult(
                    false,
                    $"Whisky runtime resolution failed: {whiskyError}",
                    $"Whisky:{profile.BottleName}",
                    RuntimeLaunchDiagnostics.CreateLogPath("runtime-config"));
            }

            return await ConfigureAsync(whiskyWineProfile, managedPrefixPath, settings, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(profile.Command))
        {
            return new WineRuntimeConfigurationResult(
                false,
                "Runtime command is required before Wine settings can be applied.",
                "unknown runtime",
                RuntimeLaunchDiagnostics.CreateLogPath("runtime-config"));
        }

        if (!TryCreateRuntimeEnvironment(
                profile,
                managedPrefixPath,
                out Dictionary<string, string> environment,
                out string runtimeTarget,
                out string? normalizedPrefix,
                out string error))
        {
            return new WineRuntimeConfigurationResult(
                false,
                error,
                runtimeTarget,
                RuntimeLaunchDiagnostics.CreateLogPath("runtime-config"));
        }

        string logPath = RuntimeLaunchDiagnostics.CreateLogPath("runtime-config");
        string? windowsDocumentsPath = null;
        string configurationStorageDetail = "";
        if (settings.UsePrefixLocalDocuments && !string.IsNullOrWhiteSpace(normalizedPrefix))
        {
            if (!TryCreatePrefixLocalDocuments(
                    normalizedPrefix,
                    out WineUserDocumentsTarget documentsTarget,
                    out string documentsError))
            {
                return new WineRuntimeConfigurationResult(
                    false,
                    documentsError,
                    runtimeTarget,
                    logPath);
            }

            windowsDocumentsPath = documentsTarget.WindowsDocumentsPath;
            configurationStorageDetail = $"ffxiv_config_storage={documentsTarget.HostFfxivConfigPath}{Environment.NewLine}";
            await File.AppendAllTextAsync(logPath, configurationStorageDetail, cancellationToken);
        }
        else if (settings.UsePrefixLocalDocuments)
        {
            await File.AppendAllTextAsync(
                logPath,
                $"ffxiv_config_storage=runtime-managed prefix path unavailable for {runtimeTarget}{Environment.NewLine}",
                cancellationToken);
        }

        await RemoveLegacyDesktopSettingsAsync(
            profile.Command,
            environment,
            logPath,
            cancellationToken);

        foreach (WineRegistrySetting setting in BuildRegistrySettings(settings, windowsDocumentsPath))
        {
            ProcessRunResult result = await RunAndLogAsync(
                profile.Command,
                BuildRegAddArguments(setting),
                environment,
                logPath,
                TimeSpan.FromSeconds(30),
                cancellationToken);

            if (result.ExitCode != 0)
            {
                string detail = string.IsNullOrWhiteSpace(result.Error)
                    ? result.Output.Trim()
                    : result.Error.Trim();
                return new WineRuntimeConfigurationResult(
                    false,
                    $"Wine registry update failed with exit code {result.ExitCode}: {detail}",
                    runtimeTarget,
                    logPath);
            }
        }

        return new WineRuntimeConfigurationResult(
            true,
            string.IsNullOrWhiteSpace(configurationStorageDetail)
                ? "Wine input and fullscreen capture settings were applied."
                : "Wine input, fullscreen capture, and prefix-local FFXIV config storage were applied.",
            runtimeTarget,
            logPath);
    }

    private static bool TryCreateRuntimeEnvironment(
        WineRuntimeProfile profile,
        string managedPrefixPath,
        out Dictionary<string, string> environment,
        out string runtimeTarget,
        out string? normalizedPrefix,
        out string error)
    {
        environment = new Dictionary<string, string>(profile.Environment);
        runtimeTarget = profile.Name;
        normalizedPrefix = null;
        error = "";

        if (profile.Kind == WineRuntimeKind.WinePrefix)
        {
            string prefix = !string.IsNullOrWhiteSpace(profile.PrefixPath)
                ? profile.PrefixPath
                : managedPrefixPath;
            if (string.IsNullOrWhiteSpace(prefix))
            {
                error = "Wine prefix path is required.";
                return false;
            }

            normalizedPrefix = Path.GetFullPath(prefix);
            Directory.CreateDirectory(normalizedPrefix);
            environment["WINEPREFIX"] = normalizedPrefix;
            runtimeTarget = normalizedPrefix;
            return true;
        }

        if (profile.Kind == WineRuntimeKind.CrossOverBottle)
        {
            if (string.IsNullOrWhiteSpace(profile.BottleName))
            {
                error = "CrossOver bottle name is required.";
                return false;
            }

            environment["CX_BOTTLE"] = profile.BottleName;
            environment.Remove("WINEPREFIX");
            runtimeTarget = $"CrossOver bottle {profile.BottleName}";
            return true;
        }

        if (environment.TryGetValue("WINEPREFIX", out string? explicitPrefix)
            && !string.IsNullOrWhiteSpace(explicitPrefix))
        {
            normalizedPrefix = Path.GetFullPath(explicitPrefix);
            Directory.CreateDirectory(normalizedPrefix);
            environment["WINEPREFIX"] = normalizedPrefix;
            runtimeTarget = normalizedPrefix;
            return true;
        }

        error = "Custom runtime has no explicit Wine prefix or bottle. Select Wine prefix mode or provide a runtime that exports WINEPREFIX.";
        return false;
    }

    private static string ResolveWineUserName(string usersRoot)
    {
        string environmentUser = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(environmentUser)
            && Directory.Exists(Path.Combine(usersRoot, environmentUser)))
        {
            return environmentUser;
        }

        if (Directory.Exists(usersRoot))
        {
            string? existingUser = Directory.EnumerateDirectories(usersRoot)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .FirstOrDefault(name => !string.Equals(name, "Public", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(existingUser))
                return existingUser;
        }

        return string.IsNullOrWhiteSpace(environmentUser)
            ? "aetherxiv"
            : RuntimeInstallStore.SanitizePathSegment(environmentUser);
    }

    private static async Task RemoveLegacyDesktopSettingsAsync(
        string command,
        IReadOnlyDictionary<string, string> environment,
        string logPath,
        CancellationToken cancellationToken)
    {
        ProcessRunResult queryResult = await RunAndLogAsync(
            command,
            $"reg query {CommandLineArguments.Quote(WineExplorerDesktopsKey)}",
            environment,
            logPath,
            TimeSpan.FromSeconds(15),
            cancellationToken);

        if (queryResult.ExitCode != 0)
            return;

        foreach (string valueName in ParseLegacyDesktopValueNames(queryResult.Output))
        {
            ProcessRunResult deleteResult = await RunAndLogAsync(
                command,
                BuildRegDeleteValueArguments(WineExplorerDesktopsKey, valueName),
                environment,
                logPath,
                TimeSpan.FromSeconds(15),
                cancellationToken);

            if (deleteResult.ExitCode != 0)
            {
                await File.AppendAllTextAsync(
                    logPath,
                    $"legacy_desktop_cleanup_failed={valueName}{Environment.NewLine}",
                    cancellationToken);
            }
        }
    }

    public static IReadOnlyList<string> ParseLegacyDesktopValueNames(string registryQueryOutput)
    {
        if (string.IsNullOrWhiteSpace(registryQueryOutput))
            return Array.Empty<string>();

        List<string> valueNames = new();
        foreach (string line in registryQueryOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith(LegacyDesktopValuePrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string[] parts = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
                valueNames.Add(parts[0]);
        }

        return valueNames;
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

    private sealed record ProcessRunResult(int ExitCode, string Output, string Error);
}
