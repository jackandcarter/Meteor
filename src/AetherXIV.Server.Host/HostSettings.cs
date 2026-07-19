using AetherXIV.Core;
using AetherXIV.Data;
using AetherXIV.Lobby;
using AetherXIV.Map;
using AetherXIV.Server.Hosting;
using AetherXIV.World;

namespace AetherXIV.Server.Host;

public sealed record HostSettings(string ServiceName, ServerRuntimeOptions Runtime, MariaDbOptions Database, TimeSpan DbWait)
{
    public static HostSettings Resolve(string? serviceArg, Func<string, string?> env)
    {
        string? serviceValue = serviceArg ?? env("AETHERXIV_SERVICE");
        (string serviceName, ServerEndpoint defaultEndpoint) = ResolveService(serviceValue);

        string bindHost = env("AETHERXIV_BIND_HOST") is { Length: > 0 } host ? host : defaultEndpoint.Host;
        ushort bindPort = ParsePort(env("AETHERXIV_BIND_PORT"), defaultEndpoint.Port, "AETHERXIV_BIND_PORT");

        ServerRuntimeOptions runtime = new(serviceName, new ServerEndpoint(bindHost, bindPort));

        MariaDbOptions database = new(
            Host: env("AETHERXIV_DB_HOST") is { Length: > 0 } dbHost ? dbHost : "localhost",
            Port: ParsePort(env("AETHERXIV_DB_PORT"), 3306, "AETHERXIV_DB_PORT"),
            Database: env("AETHERXIV_DB_NAME") is { Length: > 0 } dbName ? dbName : AetherXivDatabase.DefaultDatabaseName,
            User: env("AETHERXIV_DB_USER") is { Length: > 0 } dbUser ? dbUser : "aetherxiv",
            Password: env("AETHERXIV_DB_PASSWORD") is { Length: > 0 } dbPassword ? dbPassword : "aether_dev");

        TimeSpan dbWait = TimeSpan.FromSeconds(ParseWaitSeconds(env("AETHERXIV_DB_WAIT_SECONDS")));

        return new HostSettings(serviceName, runtime, database, dbWait);
    }

    private static (string ServiceName, ServerEndpoint DefaultEndpoint) ResolveService(string? service)
    {
        return service?.Trim().ToLowerInvariant() switch
        {
            "lobby" => (LobbyFoundation.ServiceName, LobbyFoundation.DefaultEndpoint),
            "world" => (WorldFoundation.ServiceName, WorldFoundation.DefaultEndpoint),
            "map" => (MapFoundation.ServiceName, MapFoundation.DefaultEndpoint),
            _ => throw new ArgumentException(
                $"Unknown service '{service}'. Valid values: lobby, world, map.",
                nameof(service))
        };
    }

    private static ushort ParsePort(string? value, ushort fallback, string variableName)
    {
        if (string.IsNullOrEmpty(value))
            return fallback;

        if (!ushort.TryParse(value, out ushort parsed))
            throw new ArgumentException($"Invalid {variableName} value '{value}'.", nameof(value));

        return parsed;
    }

    private static int ParseWaitSeconds(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return 60;

        if (!int.TryParse(value, out int parsed) || parsed <= 0)
            throw new ArgumentException($"Invalid AETHERXIV_DB_WAIT_SECONDS value '{value}'.", nameof(value));

        return parsed;
    }
}
