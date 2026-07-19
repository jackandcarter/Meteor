using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using AetherXIV.Core;
using AetherXIV.Protocol;

namespace AetherXIV.Server.Hosting;

public sealed record ServerRuntimeOptions(string ServiceName, ServerEndpoint BindEndpoint, int Backlog = 100);

public sealed record AetherXivServiceOptions(
    string ServiceName,
    ServerEndpoint BindEndpoint,
    ServerEndpoint AdvertisedEndpoint,
    AetherXivDatabaseOptions Database,
    string? DiagnosticsDirectory,
    bool TraceEnabled,
    string DataRoot)
{
    public static AetherXivServiceOptions FromArgs(
        string serviceName,
        ServerEndpoint defaultBindEndpoint,
        ServerEndpoint defaultAdvertisedEndpoint,
        IReadOnlyList<string> args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentNullException.ThrowIfNull(args);

        Dictionary<string, string> values = ParseArgs(args);
        string prefix = serviceName.Replace(".", "_", StringComparison.Ordinal).ToUpperInvariant();

        ServerEndpoint bind = ReadEndpoint(values, "bind", $"{prefix}_BIND", defaultBindEndpoint);
        ServerEndpoint advertised = ReadEndpoint(values, "advertise", $"{prefix}_ADVERTISE", defaultAdvertisedEndpoint);
        string dataRoot = ReadString(values, "data-root", $"{prefix}_DATA_ROOT", Environment.CurrentDirectory);
        string? diagnosticsDirectory = ReadOptionalString(values, "diagnostics-dir", $"{prefix}_DIAGNOSTICS_DIR");
        bool traceEnabled = ReadBool(values, "trace", $"{prefix}_TRACE", diagnosticsDirectory is not null);

        AetherXivDatabaseOptions database = new(
            ReadString(values, "db-host", "AETHERXIV_DB_HOST", "localhost"),
            ReadUShort(values, "db-port", "AETHERXIV_DB_PORT", 3306),
            ReadString(values, "db-name", "AETHERXIV_DB_NAME", "ffxiv_server"),
            ReadString(values, "db-user", "AETHERXIV_DB_USER", "aetherxiv"),
            ReadString(values, "db-password", "AETHERXIV_DB_PASSWORD", "aether_dev"));

        return new AetherXivServiceOptions(
            serviceName,
            bind,
            advertised,
            database,
            diagnosticsDirectory,
            traceEnabled,
            dataRoot);
    }

    private static Dictionary<string, string> ParseArgs(IReadOnlyList<string> args)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < args.Count; index++)
        {
            string arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
                continue;

            string key = arg[2..];
            string value = "true";
            int equals = key.IndexOf('=', StringComparison.Ordinal);
            if (equals >= 0)
            {
                value = key[(equals + 1)..];
                key = key[..equals];
            }
            else if (index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++index];
            }

            values[key] = value;
        }

        return values;
    }

    private static ServerEndpoint ReadEndpoint(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentKey,
        ServerEndpoint fallback)
    {
        string? raw = ReadOptionalString(values, key, environmentKey);
        return raw is null ? fallback : ParseEndpoint(raw);
    }

    private static ServerEndpoint ParseEndpoint(string raw)
    {
        string[] parts = raw.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !UInt16.TryParse(parts[1], out ushort port) || String.IsNullOrWhiteSpace(parts[0]))
            throw new FormatException($"Endpoint '{raw}' must be in host:port form.");

        return new ServerEndpoint(parts[0], port);
    }

    private static string ReadString(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentKey,
        string fallback)
    {
        return ReadOptionalString(values, key, environmentKey) ?? fallback;
    }

    private static string? ReadOptionalString(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentKey)
    {
        if (values.TryGetValue(key, out string? value) && !String.IsNullOrWhiteSpace(value))
            return value;

        string? env = Environment.GetEnvironmentVariable(environmentKey);
        return String.IsNullOrWhiteSpace(env) ? null : env;
    }

    private static bool ReadBool(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentKey,
        bool fallback)
    {
        string? raw = ReadOptionalString(values, key, environmentKey);
        return raw is null ? fallback : Boolean.Parse(raw);
    }

    private static ushort ReadUShort(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentKey,
        ushort fallback)
    {
        string? raw = ReadOptionalString(values, key, environmentKey);
        return raw is null ? fallback : UInt16.Parse(raw);
    }
}

public sealed record AetherXivDatabaseOptions(
    string Host,
    ushort Port,
    string Database,
    string User,
    string Password);

public sealed record SessionKey(uint Value, string Channel);

public interface ISessionManager<TSession>
{
    ValueTask<TSession> AttachAsync(SessionKey key, CancellationToken cancellationToken = default);

    ValueTask DetachAsync(SessionKey key, string reason, CancellationToken cancellationToken = default);

    ValueTask<TSession?> FindAsync(SessionKey key, CancellationToken cancellationToken = default);
}

public interface IWorldRouteRegistry
{
    ValueTask<ServerEndpoint?> FindMapEndpointAsync(ZoneId zoneId, CancellationToken cancellationToken = default);
}

public interface IPacketDispatcher
{
    ValueTask DispatchAsync(SubPacket packet, CancellationToken cancellationToken = default);
}

public interface IPipelineConnection : IAsyncDisposable
{
    EndPoint? RemoteEndPoint { get; }

    PipeReader Input { get; }

    PipeWriter Output { get; }
}

public sealed class TcpPipelineConnection : IPipelineConnection
{
    private readonly TcpClient client;

    private TcpPipelineConnection(TcpClient client, PipeReader input, PipeWriter output)
    {
        this.client = client;
        Input = input;
        Output = output;
    }

    public EndPoint? RemoteEndPoint => client.Client.RemoteEndPoint;

    public PipeReader Input { get; }

    public PipeWriter Output { get; }

    public static TcpPipelineConnection Create(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        return new TcpPipelineConnection(client, PipeReader.Create(stream), PipeWriter.Create(stream));
    }

    public async ValueTask DisposeAsync()
    {
        await Input.CompleteAsync().ConfigureAwait(false);
        await Output.CompleteAsync().ConfigureAwait(false);
        client.Dispose();
    }
}

public sealed class InMemorySessionManager<TSession> : ISessionManager<TSession>
    where TSession : class, new()
{
    private readonly Dictionary<SessionKey, TSession> sessions = new();

    public ValueTask<TSession> AttachAsync(SessionKey key, CancellationToken cancellationToken = default)
    {
        TSession session = new();
        sessions[key] = session;
        return ValueTask.FromResult(session);
    }

    public ValueTask DetachAsync(SessionKey key, string reason, CancellationToken cancellationToken = default)
    {
        sessions.Remove(key);
        return ValueTask.CompletedTask;
    }

    public ValueTask<TSession?> FindAsync(SessionKey key, CancellationToken cancellationToken = default)
    {
        sessions.TryGetValue(key, out TSession? session);
        return ValueTask.FromResult(session);
    }
}
