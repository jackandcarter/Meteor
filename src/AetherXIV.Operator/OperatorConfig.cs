using System.Text.Json;

namespace AetherXIV.Operator;

public sealed record AetherXivDatabaseConfig(
    string Host,
    ushort Port,
    string Name,
    string User,
    string Password);

public sealed record AetherXivEndpointConfig(string Bind, string Advertise);

public enum AetherXivDevLogLevel
{
    Off,
    Basic,
    Verbose
}

public sealed record AetherXivDevLoggingConfig(
    bool Enabled,
    AetherXivDevLogLevel Level,
    bool NetworkTrace,
    bool ServerTrace);

public sealed record AetherXivOperatorConfig(
    string WorkspaceRoot,
    string DotnetPath,
    string DataRoot,
    string DiagnosticsDirectory,
    string ScriptsRoot,
    bool TraceEnabled,
    AetherXivDevLoggingConfig DevLogging,
    AetherXivDatabaseConfig Database,
    AetherXivEndpointConfig LauncherServices,
    AetherXivEndpointConfig Map,
    AetherXivEndpointConfig World,
    AetherXivEndpointConfig Lobby,
    string WorldMapRoute,
    uint WorldMapRouteZone,
    bool AllowLocalAccountCreation,
    bool AutoRepairDatabase)
{
    public static string DefaultDiagnosticsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AetherXIV",
        "Diagnostics");

    public static AetherXivOperatorConfig CreateDefault(string? workspaceRoot = null)
    {
        string root = AetherXivOperatorPaths.FindWorkspaceRoot(workspaceRoot ?? Environment.CurrentDirectory);
        string scriptsRoot = AetherXivOperatorPaths.ResolveScriptsRoot(root);
        return new AetherXivOperatorConfig(
            root,
            ResolveDotnetPath(),
            root,
            DefaultDiagnosticsDirectory,
            scriptsRoot,
            true,
            new AetherXivDevLoggingConfig(true, AetherXivDevLogLevel.Basic, true, true),
            new AetherXivDatabaseConfig("127.0.0.1", 3306, "ffxiv_server", "aetherxiv", "aether_dev"),
            new AetherXivEndpointConfig("127.0.0.1:8080", "127.0.0.1:8080"),
            new AetherXivEndpointConfig("127.0.0.1:1989", "127.0.0.1:1989"),
            new AetherXivEndpointConfig("127.0.0.1:54992", "127.0.0.1:54992"),
            new AetherXivEndpointConfig("127.0.0.1:54994", "127.0.0.1:54994"),
            "127.0.0.1:1989",
            209,
            true,
            true);
    }

    public AetherXivOperatorConfig Normalize()
    {
        string root = Path.GetFullPath(String.IsNullOrWhiteSpace(WorkspaceRoot) ? Environment.CurrentDirectory : WorkspaceRoot);
        string dataRoot = Path.GetFullPath(String.IsNullOrWhiteSpace(DataRoot) ? root : DataRoot);
        string diagnosticsRoot = Path.GetFullPath(String.IsNullOrWhiteSpace(DiagnosticsDirectory)
            ? DefaultDiagnosticsDirectory
            : DiagnosticsDirectory);
        string scriptsRoot = Path.GetFullPath(String.IsNullOrWhiteSpace(ScriptsRoot)
            ? AetherXivOperatorPaths.ResolveScriptsRoot(root)
            : ScriptsRoot);

        return this with
        {
            WorkspaceRoot = root,
            DotnetPath = String.IsNullOrWhiteSpace(DotnetPath) ? ResolveDotnetPath() : DotnetPath,
            DataRoot = dataRoot,
            DiagnosticsDirectory = diagnosticsRoot,
            ScriptsRoot = scriptsRoot,
            DevLogging = DevLogging ?? new AetherXivDevLoggingConfig(TraceEnabled, AetherXivDevLogLevel.Basic, TraceEnabled, TraceEnabled)
        };
    }

    private static string ResolveDotnetPath()
    {
        string? explicitPath = Environment.GetEnvironmentVariable("DOTNET_BIN");
        if (!String.IsNullOrWhiteSpace(explicitPath))
            return explicitPath;

        const string macBundledPath = "/usr/local/share/dotnet/dotnet";
        return File.Exists(macBundledPath) ? macBundledPath : "dotnet";
    }
}

public static class AetherXivOperatorPaths
{
    public static string FindWorkspaceRoot(string startPath)
    {
        string current = Path.GetFullPath(String.IsNullOrWhiteSpace(startPath) ? Environment.CurrentDirectory : startPath);
        if (File.Exists(current))
            current = Path.GetDirectoryName(current) ?? Environment.CurrentDirectory;

        DirectoryInfo? directory = new(current);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AetherXIV.sln")))
                return directory.FullName;

            if (IsPackagedRoot(directory.FullName))
                return directory.FullName;

            string macResources = Path.Combine(directory.FullName, "Resources");
            if (IsPackagedRoot(macResources))
                return macResources;

            directory = directory.Parent;
        }

        return current;
    }

    public static bool IsPackagedRoot(string path)
    {
        if (!Directory.Exists(path))
            return false;

        string mapHost = Path.Combine(path, "servers", "map", HostExecutableName("AetherXIV.Core.Map"));
        string worldHost = Path.Combine(path, "servers", "world", HostExecutableName("AetherXIV.Core.World"));
        string lobbyHost = Path.Combine(path, "servers", "lobby", HostExecutableName("AetherXIV.Core.Lobby"));
        string scriptsRoot = ResolveScriptsRoot(path);
        return File.Exists(mapHost)
            && File.Exists(worldHost)
            && File.Exists(lobbyHost)
            && Directory.Exists(scriptsRoot);
    }

    public static string ResolveScriptsRoot(string root)
    {
        string packaged = Path.Combine(root, "servers", "map", "scripts");
        return Directory.Exists(packaged)
            ? packaged
            : Path.Combine(root, "Data", "scripts");
    }

    public static string ResolveLuaManifestPath(string root)
    {
        string packaged = Path.Combine(root, "servers", "map", "scripts.manifest.json");
        return File.Exists(packaged)
            ? packaged
            : Path.Combine(root, "Data", "seeds", "lua-tree", "manifest.json");
    }

    public static string ResolveStaticActorsPath(string root)
    {
        string packaged = Path.Combine(root, "servers", "map", "staticactors.bin");
        return File.Exists(packaged)
            ? packaged
            : Path.Combine(root, "Data", "seeds", "static-actors", "staticactors.bin");
    }

    private static string HostExecutableName(string baseName) =>
        OperatingSystem.IsWindows() ? $"{baseName}.exe" : baseName;
}

public static class AetherXivOperatorConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AetherXIV",
        "Core",
        "core-settings.json");

    private static string LegacyDefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AetherXIV",
        "Operator",
        "operator-settings.json");

    public static AetherXivOperatorConfig LoadOrCreate(string? path = null, string? workspaceRoot = null)
    {
        string configPath = path ?? DefaultPath;
        if (path is null && !File.Exists(configPath) && File.Exists(LegacyDefaultPath))
        {
            string legacyJson = File.ReadAllText(LegacyDefaultPath);
            AetherXivOperatorConfig? legacyConfig = JsonSerializer.Deserialize<AetherXivOperatorConfig>(legacyJson, JsonOptions);
            AetherXivOperatorConfig migrated = (legacyConfig ?? AetherXivOperatorConfig.CreateDefault(workspaceRoot)).Normalize();
            Save(migrated, configPath);
            return migrated;
        }

        if (!File.Exists(configPath))
        {
            AetherXivOperatorConfig created = AetherXivOperatorConfig.CreateDefault(workspaceRoot);
            Save(created, configPath);
            return created;
        }

        string json = File.ReadAllText(configPath);
        AetherXivOperatorConfig? config = JsonSerializer.Deserialize<AetherXivOperatorConfig>(json, JsonOptions);
        AetherXivOperatorConfig normalized = (config ?? AetherXivOperatorConfig.CreateDefault(workspaceRoot)).Normalize();
        return normalized;
    }

    public static void Save(AetherXivOperatorConfig config, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        string configPath = path ?? DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".");
        File.WriteAllText(configPath, JsonSerializer.Serialize(config.Normalize(), JsonOptions));
    }
}
