using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using AetherXIV.Core;
using AetherXIV.Protocol;

namespace AetherXIV.Server.Hosting;

public sealed class RawLegacySubPacketConnection
{
    private readonly Stream stream;
    private readonly RawLegacySubPacketCodec codec;
    private readonly int maxSubPacketSize;
    private readonly IDiagnosticSink diagnostics;
    private readonly string diagnosticPrefix;
    private readonly SemaphoreSlim writeGate = new(1, 1);

    public RawLegacySubPacketConnection(
        Stream stream,
        RawLegacySubPacketCodec? codec = null,
        int maxSubPacketSize = UInt16.MaxValue,
        IDiagnosticSink? diagnostics = null,
        string diagnosticPrefix = "legacy.rawSubPacket")
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanWrite)
            throw new ArgumentException("Raw legacy subpacket connection requires a readable and writable stream.", nameof(stream));

        if (maxSubPacketSize < RawLegacySubPacketCodec.HeaderSize)
            throw new ArgumentOutOfRangeException(nameof(maxSubPacketSize), maxSubPacketSize, "Max subpacket size must allow the legacy subpacket header.");

        this.stream = stream;
        this.codec = codec ?? new RawLegacySubPacketCodec();
        this.maxSubPacketSize = maxSubPacketSize;
        this.diagnostics = diagnostics ?? NullDiagnosticSink.Instance;
        this.diagnosticPrefix = diagnosticPrefix;
    }

    public async ValueTask<WireLegacySubPacket?> ReadSubPacketAsync(CancellationToken cancellationToken = default)
    {
        byte[] header = new byte[RawLegacySubPacketCodec.HeaderSize];
        bool hasHeader = await ReadExactlyOrEndAsync(header, cancellationToken).ConfigureAwait(false);
        if (!hasHeader)
            return null;

        ushort subPacketSize = PacketBinary.ReadUInt16LittleEndian(header);
        if (subPacketSize < RawLegacySubPacketCodec.HeaderSize)
            throw new InvalidDataException($"Raw legacy subpacket declared impossible size {subPacketSize}.");

        if (subPacketSize > maxSubPacketSize)
            throw new InvalidDataException($"Raw legacy subpacket declared size {subPacketSize}, above configured max {maxSubPacketSize}.");

        byte[] bytes = new byte[subPacketSize];
        header.CopyTo(bytes.AsSpan());
        int bodyLength = subPacketSize - RawLegacySubPacketCodec.HeaderSize;
        if (bodyLength > 0)
            await ReadExactlyAsync(bytes.AsMemory(RawLegacySubPacketCodec.HeaderSize, bodyLength), cancellationToken).ConfigureAwait(false);

        WireLegacySubPacket decoded = codec.Decode(bytes);
        TracePacket("read", decoded, bytes);
        return decoded;
    }

    public async ValueTask WriteSubPacketAsync(
        WireLegacySubPacket subPacket,
        CancellationToken cancellationToken = default)
    {
        byte[] bytes = codec.Encode(subPacket);
        if (bytes.Length > maxSubPacketSize)
            throw new InvalidDataException($"Encoded raw legacy subpacket size {bytes.Length} exceeds configured max {maxSubPacketSize}.");

        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            TracePacket("write", subPacket, bytes);
        }
        finally
        {
            writeGate.Release();
        }
    }

    private void TracePacket(string direction, WireLegacySubPacket packet, byte[] encoded)
    {
        diagnostics.Trace($"{diagnosticPrefix}.{direction}", new Dictionary<string, object?>
        {
            ["direction"] = direction == "read" ? "client-to-server" : "server-to-client",
            ["opcode"] = packet.Opcode.HasValue ? $"0x{(ushort)packet.Opcode.Value:X4}" : null,
            ["sourceActorId"] = packet.SourceActorId,
            ["targetActorId"] = packet.TargetActorId,
            ["subPacketType"] = packet.Type,
            ["encodedLength"] = encoded.Length,
            ["payloadLength"] = packet.Payload.Length,
            ["encodedSha256"] = Convert.ToHexString(SHA256.HashData(encoded)).ToLowerInvariant()
        });
    }

    private async ValueTask<bool> ReadExactlyOrEndAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        int read = 0;
        while (read < destination.Length)
        {
            int current = await stream.ReadAsync(destination[read..], cancellationToken).ConfigureAwait(false);
            if (current == 0)
            {
                if (read == 0)
                    return false;

                throw new EndOfStreamException($"Stream ended after {read} of {destination.Length} bytes.");
            }

            read += current;
        }

        return true;
    }

    private async ValueTask ReadExactlyAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        bool success = await ReadExactlyOrEndAsync(destination, cancellationToken).ConfigureAwait(false);
        if (!success)
            throw new EndOfStreamException("Stream ended before the raw legacy subpacket body was read.");
    }
}

public sealed record RawLegacyTcpSubPacketServerOptions(
    string ServiceName,
    ServerEndpoint BindEndpoint,
    int Backlog = 100,
    int MaxSubPacketSize = UInt16.MaxValue,
    bool TracePackets = false);

public sealed class RawLegacyClientConnection : IAsyncDisposable
{
    private readonly TcpClient tcpClient;

    internal RawLegacyClientConnection(
        TcpClient tcpClient,
        LegacyConnectionContext context,
        RawLegacySubPacketConnection subPackets)
    {
        this.tcpClient = tcpClient;
        Context = context;
        SubPackets = subPackets;
    }

    public LegacyConnectionContext Context { get; }

    public RawLegacySubPacketConnection SubPackets { get; }

    public EndPoint? RemoteEndPoint => Context.RemoteEndPoint;

    public ValueTask DisposeAsync()
    {
        tcpClient.Dispose();
        return ValueTask.CompletedTask;
    }
}

public interface IRawLegacySubPacketSessionHandler
{
    ValueTask HandleConnectionAsync(RawLegacyClientConnection connection, CancellationToken cancellationToken = default);
}

public sealed class RawLegacyTcpSubPacketServer : IAsyncServerLoop
{
    private readonly RawLegacyTcpSubPacketServerOptions options;
    private readonly IRawLegacySubPacketSessionHandler handler;
    private readonly IDiagnosticSink diagnostics;

    public RawLegacyTcpSubPacketServer(
        RawLegacyTcpSubPacketServerOptions options,
        IRawLegacySubPacketSessionHandler handler,
        IDiagnosticSink? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);

        this.options = options;
        this.handler = handler;
        this.diagnostics = diagnostics ?? NullDiagnosticSink.Instance;
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        IPAddress bindAddress = ResolveBindAddress(options.BindEndpoint.Host);
        TcpListener listener = new(bindAddress, options.BindEndpoint.Port);
        listener.Start(options.Backlog);

        diagnostics.Trace("legacy.rawTcp.listen", new Dictionary<string, object?>
        {
            ["service"] = options.ServiceName,
            ["bind"] = options.BindEndpoint.ToString(),
            ["backlog"] = options.Backlog,
            ["maxSubPacketSize"] = options.MaxSubPacketSize
        });

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient tcpClient;
                try
                {
                    tcpClient = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _ = Task.Run(
                    () => HandleClientAsync(tcpClient, cancellationToken).AsTask(),
                    CancellationToken.None);
            }
        }
        finally
        {
            listener.Stop();
            diagnostics.Trace("legacy.rawTcp.stop", new Dictionary<string, object?>
            {
                ["service"] = options.ServiceName,
                ["bind"] = options.BindEndpoint.ToString()
            });
        }
    }

    private async ValueTask HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
    {
        LegacyConnectionContext context = new(
            Guid.NewGuid(),
            options.ServiceName,
            tcpClient.Client.RemoteEndPoint,
            DateTimeOffset.UtcNow);

        await using RawLegacyClientConnection connection = new(
            tcpClient,
            context,
            new RawLegacySubPacketConnection(
                tcpClient.GetStream(),
                maxSubPacketSize: options.MaxSubPacketSize,
                diagnostics: options.TracePackets ? diagnostics : NullDiagnosticSink.Instance,
                diagnosticPrefix: $"legacy.rawSubPacket.{options.ServiceName}"));

        diagnostics.Trace("legacy.rawTcp.accept", new Dictionary<string, object?>
        {
            ["service"] = options.ServiceName,
            ["connectionId"] = context.ConnectionId,
            ["remote"] = context.RemoteEndPoint
        });

        try
        {
            await handler.HandleConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
            diagnostics.Trace("legacy.rawTcp.disconnect", new Dictionary<string, object?>
            {
                ["service"] = options.ServiceName,
                ["connectionId"] = context.ConnectionId,
                ["remote"] = context.RemoteEndPoint,
                ["reason"] = "clean"
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            diagnostics.Trace("legacy.rawTcp.disconnect", new Dictionary<string, object?>
            {
                ["service"] = options.ServiceName,
                ["connectionId"] = context.ConnectionId,
                ["remote"] = context.RemoteEndPoint,
                ["reason"] = "cancelled"
            });
        }
        catch (Exception ex)
        {
            diagnostics.Trace("legacy.rawTcp.connection.error", new Dictionary<string, object?>
            {
                ["service"] = options.ServiceName,
                ["connectionId"] = context.ConnectionId,
                ["remote"] = context.RemoteEndPoint,
                ["error"] = ex.Message,
                ["exceptionType"] = ex.GetType().FullName
            });
        }
    }

    private static IPAddress ResolveBindAddress(string host)
    {
        if (IPAddress.TryParse(host, out IPAddress? address))
            return address;

        if (StringComparer.OrdinalIgnoreCase.Equals(host, "localhost"))
            return IPAddress.Loopback;

        IPAddress[] addresses = Dns.GetHostAddresses(host);
        return addresses.FirstOrDefault(static candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault()
            ?? IPAddress.Any;
    }
}
