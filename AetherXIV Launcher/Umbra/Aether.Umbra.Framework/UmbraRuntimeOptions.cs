namespace Aether.Umbra.Framework;

public sealed record UmbraRuntimeOptions(
    string LogPath,
    string PluginDirectory,
    string CacheDirectory,
    string DevBridgeDirectory,
    string DevBridgeControlPath,
    bool DevBridgeInitiallyEnabled,
    int DevBridgePort,
    bool SafeMode,
    IReadOnlyList<string> RepositoryUrls,
    IReadOnlyList<UmbraRepositorySource> RepositorySources)
{
    public const int DefaultDevBridgePort = 8797;

    public static UmbraRuntimeOptions FromEnvironment(string? explicitLogPath = null)
    {
        string logPath = string.IsNullOrWhiteSpace(explicitLogPath)
            ? GetUmbraEnvironment("LOG")
            : explicitLogPath;
        if (string.IsNullOrWhiteSpace(logPath))
            logPath = Path.Combine(AppContext.BaseDirectory, "umbra-framework.log");

        string pluginDirectory = GetUmbraEnvironment("PLUGIN_DIR");
        if (string.IsNullOrWhiteSpace(pluginDirectory))
            pluginDirectory = Path.Combine(AppContext.BaseDirectory, "Plugins");

        string cacheDirectory = GetUmbraEnvironment("CACHE_DIR");
        if (string.IsNullOrWhiteSpace(cacheDirectory))
            cacheDirectory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(pluginDirectory)) ?? AppContext.BaseDirectory, "Cache");

        string devBridgeDirectory = GetUmbraEnvironment("DEV_BRIDGE_DIR");
        if (string.IsNullOrWhiteSpace(devBridgeDirectory))
            devBridgeDirectory = Path.Combine(cacheDirectory, "DevBridge");

        string devBridgeControlPath = GetUmbraEnvironment("DEV_BRIDGE_CONTROL");
        if (string.IsNullOrWhiteSpace(devBridgeControlPath))
            devBridgeControlPath = Path.Combine(devBridgeDirectory, "control.json");

        bool devBridgeInitiallyEnabled = IsTruthy(GetUmbraEnvironment("DEV_BRIDGE"));
        int devBridgePort = ParsePort(
            GetUmbraEnvironment("DEV_BRIDGE_PORT"),
            DefaultDevBridgePort);

        bool safeMode = IsTruthy(GetUmbraEnvironment("SAFE_MODE"));
        IReadOnlyList<string> repositories = ParseRepositoryUrls(
            GetUmbraEnvironment("REPOSITORY_URLS"));
        IReadOnlyList<UmbraRepositorySource> repositorySources = UmbraRepositorySource.FromJson(
            GetUmbraEnvironment("REPOSITORIES_JSON"));
        if (repositorySources.Count == 0)
            repositorySources = UmbraRepositorySource.FromUrls(repositories, UmbraRepositorySource.Custom);

        return new UmbraRuntimeOptions(
            Path.GetFullPath(logPath),
            Path.GetFullPath(pluginDirectory),
            Path.GetFullPath(cacheDirectory),
            Path.GetFullPath(devBridgeDirectory),
            Path.GetFullPath(devBridgeControlPath),
            devBridgeInitiallyEnabled,
            devBridgePort,
            safeMode,
            repositorySources.Select(source => source.Url).ToArray(),
            repositorySources);
    }

    private static string GetUmbraEnvironment(string suffix)
    {
        return Environment.GetEnvironmentVariable($"AETHER_UMBRA_{suffix}") ?? "";
    }

    private static bool IsTruthy(string? value)
    {
        return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ParseRepositoryUrls(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        return value
            .Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int ParsePort(string? value, int defaultValue)
    {
        if (!int.TryParse(value, out int parsed))
            return defaultValue;

        return parsed is >= 1024 and <= 65535 ? parsed : defaultValue;
    }
}
