namespace AetherXIV.Operator;

public enum AetherXivManagedService
{
    Map,
    World,
    Lobby,
    LauncherServices
}

public enum AetherXivServiceRunState
{
    Stopped,
    Starting,
    Running,
    Stopping,
    Exited,
    Failed
}

public sealed record AetherXivServiceDefinition(
    AetherXivManagedService Kind,
    string DisplayName,
    string Description,
    string ProjectRelativePath,
    string PublishedExecutableRelativePath,
    string BindEndpoint,
    string? AdvertiseEndpoint,
    int StartOrder,
    IReadOnlyList<string> Arguments)
{
    public string ProjectPath(AetherXivOperatorConfig config) =>
        Path.GetFullPath(Path.Combine(config.WorkspaceRoot, ProjectRelativePath));

    public string PublishedExecutablePath(AetherXivOperatorConfig config) =>
        Path.GetFullPath(Path.Combine(config.WorkspaceRoot, PublishedExecutableRelativePath));

    public bool HasPublishedExecutable(AetherXivOperatorConfig config) =>
        File.Exists(PublishedExecutablePath(config));

    public IReadOnlyList<string> BuildDotnetArguments(AetherXivOperatorConfig config)
    {
        List<string> arguments =
        [
            "run",
            "--project",
            ProjectPath(config),
            "--no-restore",
            "--"
        ];
        arguments.AddRange(Arguments);
        return arguments;
    }
}

public static class AetherXivServiceCatalog
{
    public static IReadOnlyList<AetherXivManagedService> GracefulShutdownOrder { get; } =
    [
        AetherXivManagedService.LauncherServices,
        AetherXivManagedService.Lobby,
        AetherXivManagedService.Map,
        AetherXivManagedService.World
    ];

    public static IReadOnlyList<AetherXivServiceDefinition> CreateDefault(AetherXivOperatorConfig config)
    {
        AetherXivOperatorConfig normalized = config.Normalize();
        return
        [
            new AetherXivServiceDefinition(
                AetherXivManagedService.Map,
                "Map",
                "Direct-port Map server for legacy-faithful zones, actors, Lua events, directors, battle, and map packets.",
                Path.Combine("src", "AetherXIV.Core.Map", "AetherXIV.Core.Map.csproj"),
                Path.Combine("servers", "map", HostExecutableName("AetherXIV.Core.Map")),
                normalized.Map.Bind,
                normalized.Map.Advertise,
                10,
                DirectCoreArguments(normalized, normalized.Map, noConsole: true)),
            new AetherXivServiceDefinition(
                AetherXivManagedService.World,
                "World",
                "Direct-port World server for legacy-faithful session, group, chat, and Map-server coordination.",
                Path.Combine("src", "AetherXIV.Core.World", "AetherXIV.Core.World.csproj"),
                Path.Combine("servers", "world", HostExecutableName("AetherXIV.Core.World")),
                normalized.World.Bind,
                normalized.World.Advertise,
                20,
                DirectCoreArguments(normalized, normalized.World, noConsole: true)),
            new AetherXivServiceDefinition(
                AetherXivManagedService.Lobby,
                "Lobby",
                "Direct-port Lobby server for legacy-faithful authentication, character lifecycle, list, and selection.",
                Path.Combine("src", "AetherXIV.Core.Lobby", "AetherXIV.Core.Lobby.csproj"),
                Path.Combine("servers", "lobby", HostExecutableName("AetherXIV.Core.Lobby")),
                normalized.Lobby.Bind,
                normalized.Lobby.Advertise,
                30,
                DirectCoreArguments(normalized, normalized.Lobby, noConsole: false)),
            new AetherXivServiceDefinition(
                AetherXivManagedService.LauncherServices,
                "Launcher Services",
                "HTTP service for launcher config, status, news, patch manifest, runtime catalog, and account login.",
                Path.Combine("src", "AetherXIV.Launcher.Host", "AetherXIV.Launcher.Host.csproj"),
                Path.Combine("servers", "launcher-services", HostExecutableName("AetherXIV.Launcher.Host")),
                normalized.LauncherServices.Bind,
                null,
                40,
                LauncherServiceArguments(normalized))
        ];
    }

    private static IReadOnlyList<string> DirectCoreArguments(
        AetherXivOperatorConfig config,
        AetherXivEndpointConfig endpoint,
        bool noConsole)
    {
        (string host, string port) = SplitEndpoint(endpoint.Bind);
        List<string> arguments =
        [
            "--ip",
            host,
            "--port",
            port,
            "--host",
            config.Database.Host,
            "--db-port",
            config.Database.Port.ToString(),
            "--db",
            config.Database.Name,
            "--user",
            config.Database.User,
            "--p",
            config.Database.Password
        ];
        if (noConsole)
            arguments.Add("--no-console");
        return arguments;
    }

    private static (string Host, string Port) SplitEndpoint(string endpoint)
    {
        int separator = endpoint.LastIndexOf(':');
        if (separator <= 0 || separator == endpoint.Length - 1)
            throw new InvalidOperationException($"Invalid service endpoint: {endpoint}");
        return (endpoint[..separator], endpoint[(separator + 1)..]);
    }

    private static string HostExecutableName(string baseName) =>
        OperatingSystem.IsWindows() ? $"{baseName}.exe" : baseName;

    private static IReadOnlyList<string> LauncherServiceArguments(AetherXivOperatorConfig config) =>
    [
        "--bind",
        config.LauncherServices.Bind,
        "--db-host",
        config.Database.Host,
        "--db-port",
        config.Database.Port.ToString(),
        "--db-name",
        config.Database.Name,
        "--db-user",
        config.Database.User,
        "--db-password",
        config.Database.Password,
        "--allow-account-create",
        config.AllowLocalAccountCreation ? "true" : "false"
    ];
}
