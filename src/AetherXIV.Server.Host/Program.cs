using System.Net.Sockets;
using System.Runtime.InteropServices;
using AetherXIV.Core;
using AetherXIV.Data;
using AetherXIV.Server.Hosting;
using MySqlConnector;

namespace AetherXIV.Server.Host;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        HostSettings settings;
        try
        {
            string? serviceArg = args.Length > 0 ? args[0] : null;
            settings = HostSettings.Resolve(serviceArg, Environment.GetEnvironmentVariable);
        }
        catch (ArgumentException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            await Console.Error.WriteLineAsync("Usage: AetherXIV.Server.Host <lobby|world|map>");
            await Console.Error.WriteLineAsync(
                "Env vars: AETHERXIV_SERVICE, AETHERXIV_BIND_HOST, AETHERXIV_BIND_PORT, "
                + "AETHERXIV_DB_HOST, AETHERXIV_DB_PORT, AETHERXIV_DB_NAME, AETHERXIV_DB_USER, "
                + "AETHERXIV_DB_PASSWORD, AETHERXIV_DB_WAIT_SECONDS");
            return 64;
        }

        ConsoleDiagnosticSink diagnostics = new();
        using CancellationTokenSource cts = new();

        using PosixSignalRegistration sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
        {
            context.Cancel = true;
            cts.Cancel();
        });
        using PosixSignalRegistration sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            cts.Cancel();
        });

        try
        {
            if (!await WaitForDatabaseAsync(settings, diagnostics, cts.Token).ConfigureAwait(false))
                return 1;

            return await RunServiceAsync(settings, diagnostics, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception ex)
        {
            diagnostics.Trace("server.fatal", new Dictionary<string, object?>
            {
                ["service"] = settings.ServiceName,
                ["error"] = ex.Message,
                ["exceptionType"] = ex.GetType().FullName
            });

            return 70;
        }
    }

    private static async Task<bool> WaitForDatabaseAsync(HostSettings settings, IDiagnosticSink diagnostics, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + settings.DbWait;
        MariaDbConnectionFactory factory = new(settings.Database);

        while (true)
        {
            try
            {
                await using MySqlConnection connection = await factory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
                await using MySqlCommand command = connection.CreateCommand();
                command.CommandText = "SELECT migration_id FROM schema_migrations ORDER BY migration_id";

                List<string> migrationIds = [];
                await using (MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        migrationIds.Add(reader.GetValue(0).ToString() ?? string.Empty);
                }

                diagnostics.Trace("db.ready", new Dictionary<string, object?>
                {
                    ["service"] = settings.ServiceName,
                    ["migrations"] = migrationIds.Count,
                    ["ids"] = string.Join(",", migrationIds)
                });

                return true;
            }
            catch (Exception ex) when (ex is MySqlException or SocketException)
            {
                diagnostics.Trace("db.wait", new Dictionary<string, object?>
                {
                    ["service"] = settings.ServiceName,
                    ["error"] = ex.Message
                });

                if (DateTimeOffset.UtcNow >= deadline)
                {
                    diagnostics.Trace("db.unavailable", new Dictionary<string, object?>
                    {
                        ["service"] = settings.ServiceName
                    });

                    return false;
                }

                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<int> RunServiceAsync(HostSettings settings, IDiagnosticSink diagnostics, CancellationToken cancellationToken)
    {
        int activeConnections = 0;

        TcpServerListener listener = new(
            settings.Runtime,
            async (connection, ct) =>
            {
                string remote = connection.RemoteEndPoint?.ToString() ?? "unknown";
                Interlocked.Increment(ref activeConnections);
                try
                {
                    long bytes = 0;
                    while (true)
                    {
                        System.IO.Pipelines.ReadResult result = await connection.Input.ReadAsync(ct).ConfigureAwait(false);
                        bytes += result.Buffer.Length;
                        connection.Input.AdvanceTo(result.Buffer.End);

                        if (result.IsCompleted)
                            break;
                    }

                    // Zero-byte connections are compose TCP healthcheck probes;
                    // keep them out of the log.
                    if (bytes > 0)
                    {
                        diagnostics.Trace("connection.served", new Dictionary<string, object?>
                        {
                            ["service"] = settings.ServiceName,
                            ["remote"] = remote,
                            ["bytes"] = bytes
                        });
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref activeConnections);
                }
            },
            diagnostics);

        FixedIntervalServerLoop heartbeat = new(
            $"{settings.ServiceName}.heartbeat",
            new PeriodicIntervalTickSource(TimeSpan.FromSeconds(60)),
            _ =>
            {
                diagnostics.Trace("server.heartbeat", new Dictionary<string, object?>
                {
                    ["service"] = settings.ServiceName,
                    ["activeConnections"] = activeConnections
                });

                return ValueTask.CompletedTask;
            },
            diagnostics);

        Task listenerTask = listener.RunAsync(cancellationToken).AsTask();
        Task heartbeatTask = heartbeat.RunAsync(cancellationToken).AsTask();

        await Task.WhenAll(listenerTask, heartbeatTask).ConfigureAwait(false);
        return 0;
    }
}
