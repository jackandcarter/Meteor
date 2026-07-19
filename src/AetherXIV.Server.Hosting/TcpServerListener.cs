using System.Net;
using System.Net.Sockets;
using AetherXIV.Core;

namespace AetherXIV.Server.Hosting;

public sealed class TcpServerListener : IAsyncServerLoop
{
    private readonly ServerRuntimeOptions options;
    private readonly Func<IPipelineConnection, CancellationToken, ValueTask> onConnection;
    private readonly IDiagnosticSink diagnostics;
    private readonly TaskCompletionSource<IPEndPoint> started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TcpServerListener(
        ServerRuntimeOptions options,
        Func<IPipelineConnection, CancellationToken, ValueTask> onConnection,
        IDiagnosticSink? diagnostics = null)
    {
        this.options = options;
        this.onConnection = onConnection;
        this.diagnostics = diagnostics ?? NullDiagnosticSink.Instance;
    }

    public Task<IPEndPoint> Started => started.Task;

    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        IPAddress address = await ResolveAddressAsync(options.BindEndpoint.Host, cancellationToken).ConfigureAwait(false);
        TcpListener listener = new(address, options.BindEndpoint.Port);

        try
        {
            listener.Start(options.Backlog);
        }
        catch (Exception ex)
        {
            started.TrySetException(ex);
            throw;
        }

        IPEndPoint boundEndpoint = (IPEndPoint)listener.LocalEndpoint;
        started.TrySetResult(boundEndpoint);

        diagnostics.Trace("server.listener.start", new Dictionary<string, object?>
        {
            ["service"] = options.ServiceName,
            ["host"] = boundEndpoint.Address.ToString(),
            ["port"] = boundEndpoint.Port
        });

        try
        {
            while (true)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                _ = HandleConnectionAsync(client, cancellationToken);
            }
        }
        finally
        {
            listener.Stop();
            diagnostics.Trace("server.listener.stop", new Dictionary<string, object?>
            {
                ["service"] = options.ServiceName
            });
        }
    }

    // Per-connection reporting is the onConnection callback's job; the listener
    // traces only lifecycle and faults so bare TCP probes (healthchecks) stay
    // out of the log.
    private async Task HandleConnectionAsync(TcpClient client, CancellationToken cancellationToken)
    {
        IPipelineConnection connection = TcpPipelineConnection.Create(client);
        string remote = connection.RemoteEndPoint?.ToString() ?? "unknown";

        try
        {
            await onConnection(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            diagnostics.Trace("connection.error", new Dictionary<string, object?>
            {
                ["service"] = options.ServiceName,
                ["remote"] = remote,
                ["error"] = ex.Message
            });
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async ValueTask<IPAddress> ResolveAddressAsync(string host, CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out IPAddress? parsed))
            return parsed;

        IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        return addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
            ?? addresses.FirstOrDefault()
            ?? throw new SocketException((int)SocketError.HostNotFound);
    }
}
