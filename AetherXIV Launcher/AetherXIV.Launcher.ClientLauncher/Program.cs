using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using AetherXIV.Launcher.Core;

namespace AetherXIV.Launcher.ClientLauncher;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length == 1 && string.Equals(args[0], "--probe", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("AETHERXIV_CLIENT_HELPER_OK x86-compatible umbra-capable");
            return 0;
        }

        LaunchOptions options;
        try
        {
            options = LaunchOptions.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.LogPath)) ?? ".");

            File.AppendAllText(options.LogPath, $"helper_started_at={DateTimeOffset.Now:O}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"helper_process_architecture={RuntimeInformation.ProcessArchitecture}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"helper_os_architecture={RuntimeInformation.OSArchitecture}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"helper_is_64bit_process={Environment.Is64BitProcess}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"helper_framework={RuntimeInformation.FrameworkDescription}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"helper_os_description={RuntimeInformation.OSDescription}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"helper_process_count={Environment.ProcessorCount}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"helper_current_directory={Environment.CurrentDirectory}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"game={options.GamePath}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"working_directory={options.WorkingDirectory}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"working_directory_exists={Directory.Exists(options.WorkingDirectory)}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"server_host={options.ServerHost}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"observe_ms={options.ObservationTimeoutMilliseconds}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"session_length={options.SessionId.Length}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"umbra_requested={options.Umbra.Enabled}{Environment.NewLine}");
            if (options.Umbra.Enabled)
            {
                File.AppendAllText(options.LogPath, $"umbra_bootstrap={options.Umbra.BootstrapPath}{Environment.NewLine}");
                File.AppendAllText(options.LogPath, $"umbra_framework={options.Umbra.FrameworkPath}{Environment.NewLine}");
                File.AppendAllText(options.LogPath, $"umbra_plugin_dir={options.Umbra.PluginDirectory}{Environment.NewLine}");
                File.AppendAllText(options.LogPath, $"umbra_log={options.Umbra.LogPath}{Environment.NewLine}");
                File.AppendAllText(options.LogPath, $"umbra_safe_mode={options.Umbra.SafeMode}{Environment.NewLine}");
                File.AppendAllText(options.LogPath, $"umbra_load_delay_ms={options.Umbra.LoadDelayMilliseconds}{Environment.NewLine}");
                File.AppendAllText(options.LogPath, $"umbra_repository_count={options.Umbra.RepositoryUrls.Count}{Environment.NewLine}");
                File.AppendAllText(options.LogPath, $"umbra_repository_source_count={options.Umbra.RepositorySources.Count}{Environment.NewLine}");
                File.AppendAllText(options.LogPath, $"umbra_enable_managed_on_wine={options.Umbra.EnableManagedOnWine}{Environment.NewLine}");
            }
            AppendFileProbe(options.LogPath, options.GamePath);
            AppendInstallProbe(options.LogPath, options.WorkingDirectory);

            string lobbyHost = ResolveLobbyHost(options.ServerHost, options.LogPath);
            File.AppendAllText(options.LogPath, $"resolved_lobby_host={lobbyHost}{Environment.NewLine}");

            uint nativeTick = NativeMethods.GetTickCount();
            GameLaunchToken token = GameLaunchTokenGenerator.Generate(options.SessionId, () => nativeTick);

            File.AppendAllText(options.LogPath, $"token_tick_source=kernel32.GetTickCount{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"token_tick={token.TickCount}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"token_length={token.Token.Length}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"launch_argument_length={token.LaunchArgument.Length}{Environment.NewLine}");

            ClientLaunchResult launchResult = ClientProcessLauncher.Launch(
                options,
                token,
                lobbyHost,
                message => AppendLog(options.LogPath, message));

            File.AppendAllText(options.LogPath, $"process_id={launchResult.ProcessId}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"thread_id={launchResult.ThreadId}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"exited_during_observation={launchResult.ExitedDuringObservation}{Environment.NewLine}");

            if (launchResult.ExitCode is not null)
            {
                File.AppendAllText(options.LogPath, $"exit_code={launchResult.ExitCode}{Environment.NewLine}");
                File.AppendAllText(options.LogPath, $"exit_code_hex=0x{launchResult.ExitCode.Value:X8}{Environment.NewLine}");
            }

            if (launchResult.ExitedDuringObservation)
            {
                string message = launchResult.ExitCode is null
                    ? "Game process exited during launch observation."
                    : $"Game process exited during launch observation with exit code {launchResult.ExitCode}.";
                File.AppendAllText(options.LogPath, $"launch_error=game_exited_during_observation{Environment.NewLine}");
                if (launchResult.ExitCode == 0xC0000005)
                    File.AppendAllText(options.LogPath, $"launch_error_detail=access_violation_fatal_assert_or_native_crash{Environment.NewLine}");
                File.AppendAllText(options.LogPath, $"helper_completed_at={DateTimeOffset.Now:O}{Environment.NewLine}");
                Console.Error.WriteLine(message);
                return 1;
            }

            File.AppendAllText(options.LogPath, $"helper_completed_at={DateTimeOffset.Now:O}{Environment.NewLine}");
            return 0;
        }
        catch (Exception ex)
        {
            File.AppendAllText(options.LogPath, $"helper_error_type={ex.GetType().FullName}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"helper_error_message={ex.Message}{Environment.NewLine}");
            File.AppendAllText(options.LogPath, $"helper_error={ex}{Environment.NewLine}");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void AppendLog(string logPath, string message)
    {
        File.AppendAllText(logPath, $"{message}{Environment.NewLine}");
    }

    private static void AppendFileProbe(string logPath, string path)
    {
        try
        {
            FileInfo file = new(path);
            File.AppendAllText(logPath, $"game_exists={file.Exists}{Environment.NewLine}");
            if (!file.Exists)
                return;

            File.AppendAllText(logPath, $"game_size={file.Length}{Environment.NewLine}");
            using FileStream stream = File.OpenRead(path);
            string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            File.AppendAllText(logPath, $"game_sha256={hash}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"game_probe_error={ex.Message}{Environment.NewLine}");
        }
    }

    private static string ResolveLobbyHost(string host, string logPath)
    {
        if (IPAddress.TryParse(host, out IPAddress? parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
        {
            File.AppendAllText(logPath, $"dns_mode=literal_ipv4{Environment.NewLine}");
            return parsed.ToString();
        }

        try
        {
            IPAddress[] addresses = Dns.GetHostAddresses(host);
            string ipv4List = string.Join(",", addresses
                .Where(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
                .Select(candidate => candidate.ToString()));
            File.AppendAllText(logPath, $"dns_mode=lookup{Environment.NewLine}");
            File.AppendAllText(logPath, $"dns_ipv4_addresses={ipv4List}{Environment.NewLine}");

            IPAddress? address = addresses
                .FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork);

            return address?.ToString() ?? host;
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"dns_error={ex.Message}{Environment.NewLine}");
            return host;
        }
    }

    private static void AppendInstallProbe(string logPath, string workingDirectory)
    {
        try
        {
            File.AppendAllText(logPath, $"boot_ver={ReadSmallText(Path.Combine(workingDirectory, "boot.ver"))}{Environment.NewLine}");
            File.AppendAllText(logPath, $"game_ver={ReadSmallText(Path.Combine(workingDirectory, "game.ver"))}{Environment.NewLine}");
            AppendFileProbeWithName(logPath, "ffxivboot", Path.Combine(workingDirectory, "ffxivboot.exe"));
            AppendFileProbeWithName(logPath, "ffxivlogin", Path.Combine(workingDirectory, "ffxivlogin.exe"));
            AppendFileProbeWithName(logPath, "ffxivupdater", Path.Combine(workingDirectory, "ffxivupdater.exe"));
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"install_probe_error={ex.Message}{Environment.NewLine}");
        }
    }

    private static string ReadSmallText(string path)
    {
        if (!File.Exists(path))
            return "missing";

        string value = File.ReadAllText(path).Trim();
        return string.IsNullOrWhiteSpace(value) ? "empty" : value;
    }

    private static void AppendFileProbeWithName(string logPath, string name, string path)
    {
        try
        {
            FileInfo file = new(path);
            File.AppendAllText(logPath, $"{name}_exists={file.Exists}{Environment.NewLine}");
            if (!file.Exists)
                return;

            File.AppendAllText(logPath, $"{name}_size={file.Length}{Environment.NewLine}");
            using FileStream stream = File.OpenRead(path);
            string hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            File.AppendAllText(logPath, $"{name}_sha256={hash}{Environment.NewLine}");
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"{name}_probe_error={ex.Message}{Environment.NewLine}");
        }
    }
}

internal sealed record LaunchOptions(
    string GamePath,
    string WorkingDirectory,
    string SessionId,
    string ServerHost,
    string LogPath,
    uint ObservationTimeoutMilliseconds,
    UmbraLaunchOptions Umbra)
{
    private const uint DefaultObservationTimeoutMilliseconds = 5000;

    public static LaunchOptions Parse(string[] args)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < args.Length; index++)
        {
            string key = args[index];

            if (!key.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Unexpected argument: {key}");

            if (index + 1 >= args.Length)
                throw new ArgumentException($"Missing value for {key}");

            values[key[2..]] = args[++index];
        }

        return new LaunchOptions(
            Required(values, "game"),
            Required(values, "working-directory"),
            Required(values, "session"),
            Required(values, "server-host"),
            Required(values, "log"),
            ParseObservationTimeout(values),
            ParseUmbraOptions(values));
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"Missing --{key}");

        return value;
    }

    private static uint ParseObservationTimeout(IReadOnlyDictionary<string, string> values)
    {
        if (!values.TryGetValue("observe-seconds", out string? value) || string.IsNullOrWhiteSpace(value))
            return DefaultObservationTimeoutMilliseconds;

        if (!uint.TryParse(value, out uint seconds) || seconds > 300)
            throw new ArgumentException("Invalid --observe-seconds value.");

        return seconds * 1000;
    }

    private static UmbraLaunchOptions ParseUmbraOptions(IReadOnlyDictionary<string, string> values)
    {
        if (!ParseBoolean(values, "umbra-enabled"))
            return UmbraLaunchOptions.Disabled;

        IReadOnlyList<string> repositoryUrls = UmbraRepositoryOptions.ParseRepositoryList(
            values.TryGetValue("umbra-repository-urls", out string? urls) ? urls : "");
        IReadOnlyList<UmbraRepositorySource> repositorySources = values.TryGetValue("umbra-repositories-json", out string? repositoriesJson)
            ? UmbraRepositorySource.FromJson(repositoriesJson)
            : UmbraRepositorySource.FromUrls(repositoryUrls, UmbraRepositorySource.Custom);

        return new UmbraLaunchOptions(
            true,
            ParseBoolean(values, "umbra-safe-mode"),
            ParseInt(values, "umbra-load-delay-ms", 0, 0, UmbraSettings.MaximumLoadDelayMilliseconds),
            Required(values, "umbra-bootstrap"),
            Required(values, "umbra-framework"),
            Required(values, "umbra-plugin-dir"),
            Required(values, "umbra-log"),
            repositoryUrls,
            EnableManagedOnWine: ParseBoolean(values, "umbra-enable-managed-on-wine"))
            {
                RepositorySources = repositorySources
            }
            .Normalize();
    }

    private static bool ParseBoolean(IReadOnlyDictionary<string, string> values, string key)
    {
        if (!values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            return false;

        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseInt(
        IReadOnlyDictionary<string, string> values,
        string key,
        int defaultValue,
        int minimum,
        int maximum)
    {
        if (!values.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (!int.TryParse(value, out int result))
            throw new ArgumentException($"Invalid --{key} value.");

        return Math.Clamp(result, minimum, maximum);
    }
}
