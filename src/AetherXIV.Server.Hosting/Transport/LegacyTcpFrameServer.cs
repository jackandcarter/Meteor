using System.Net;
using System.Net.Sockets;
using AetherXIV.Core;

namespace AetherXIV.Server.Hosting;

public sealed record LegacyTcpFrameServerOptions(
    string ServiceName,
    ServerEndpoint BindEndpoint,
    int Backlog = 100,
    int MaxFrameSize = 1048576)
{
    public const int DefaultMaxFrameSize = 1024 * 1024;
}

public sealed class LegacyClientConnection : IAsyncDisposable
{
    private readonly TcpClient tcpClient;

    internal LegacyClientConnection(
        TcpClient tcpClient,
        LegacyConnectionContext context,
        LegacyFrameConnection frames)
    {
        this.tcpClient = tcpClient;
        Context = context;
        Frames = frames;
    }

    public LegacyConnectionContext Context { get; }

    public LegacyFrameConnection Frames { get; }

    public EndPoint? RemoteEndPoint => Context.RemoteEndPoint;

    public ValueTask DisposeAsync()
    {
        tcpClient.Dispose();
        return ValueTask.CompletedTask;
    }
}

public interface ILegacyFrameSessionHandler
{
    ValueTask HandleConnectionAsync(LegacyClientConnection connection, CancellationToken cancellationToken = default);
}

public sealed class LegacyTcpFrameServer : IAsyncServerLoop
{
    private readonly LegacyTcpFrameServerOptions options;
    private readonly ILegacyFrameSessionHandler handler;
    private readonly ILegacyStreamCipherFactory cipherFactory;
    private readonly IDiagnosticSink diagnostics;

    public LegacyTcpFrameServer(
        LegacyTcpFrameServerOptions options,
        ILegacyFrameSessionHandler handler,
        ILegacyStreamCipherFactory? cipherFactory = null,
        IDiagnosticSink? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(handler);

        this.options = options;
        this.handler = handler;
        this.cipherFactory = cipherFactory ?? LegacyNoOpStreamCipherFactory.Instance;
        this.diagnostics = diagnostics ?? NullDiagnosticSink.Instance;
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        IPAddress bindAddress = ResolveBindAddress(options.BindEndpoint.Host);
        TcpListener listener = new(bindAddress, options.BindEndpoint.Port);
        listener.Start(options.Backlog);

        diagnostics.Trace("legacy.tcp.listen", new Dictionary<string, object?>
        {
            ["service"] = options.ServiceName,
            ["bind"] = options.BindEndpoint.ToString(),
            ["backlog"] = options.Backlog,
            ["maxFrameSize"] = options.MaxFrameSize
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
            diagnostics.Trace("legacy.tcp.stop", new Dictionary<string, object?>
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

        ILegacyStreamCipher cipher = cipherFactory.CreateCipher(context);
        await using LegacyClientConnection connection = new(
            tcpClient,
            context,
            new LegacyFrameConnection(
                tcpClient.GetStream(),
                cipher,
                diagnostics: diagnostics,
                maxFrameSize: options.MaxFrameSize,
                diagnosticPrefix: $"legacy.frame.{options.ServiceName}"));

        diagnostics.Trace("legacy.tcp.accept", new Dictionary<string, object?>
        {
            ["service"] = options.ServiceName,
            ["connectionId"] = context.ConnectionId,
            ["remote"] = context.RemoteEndPoint,
            ["cipher"] = cipher.Name
        });

        try
        {
            await handler.HandleConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
            diagnostics.Trace("legacy.tcp.disconnect", new Dictionary<string, object?>
            {
                ["service"] = options.ServiceName,
                ["connectionId"] = context.ConnectionId,
                ["remote"] = context.RemoteEndPoint,
                ["reason"] = "clean"
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            diagnostics.Trace("legacy.tcp.disconnect", new Dictionary<string, object?>
            {
                ["service"] = options.ServiceName,
                ["connectionId"] = context.ConnectionId,
                ["remote"] = context.RemoteEndPoint,
                ["reason"] = "cancelled"
            });
        }
        catch (Exception ex)
        {
            diagnostics.Trace("legacy.tcp.connection.error", new Dictionary<string, object?>
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
