using AetherXIV.Core;

namespace AetherXIV.Server.Hosting;

public static class ConsoleShutdownListener
{
    public const string ShutdownCommand = "shutdown";

    public static IDisposable Attach(
        CancellationTokenSource shutdown,
        IDiagnosticSink diagnostics,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(shutdown);
        ArgumentNullException.ThrowIfNull(diagnostics);

        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            RequestShutdown(shutdown, diagnostics, serviceName, "console-cancel");
        };
        EventHandler processExitHandler = (_, _) =>
            RequestShutdown(shutdown, diagnostics, serviceName, "process-exit");

        Console.CancelKeyPress += cancelHandler;
        AppDomain.CurrentDomain.ProcessExit += processExitHandler;

        _ = Task.Run(() => WatchStandardInputAsync(shutdown, diagnostics, serviceName));
        return new ShutdownListenerRegistration(cancelHandler, processExitHandler);
    }

    private static async Task WatchStandardInputAsync(
        CancellationTokenSource shutdown,
        IDiagnosticSink diagnostics,
        string serviceName)
    {
        try
        {
            while (!shutdown.IsCancellationRequested)
            {
                string? line = await Console.In.ReadLineAsync().ConfigureAwait(false);
                if (line is null)
                    return;

                if (String.Equals(line.Trim(), ShutdownCommand, StringComparison.OrdinalIgnoreCase))
                {
                    RequestShutdown(shutdown, diagnostics, serviceName, "stdin");
                    return;
                }
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException ex)
        {
            diagnostics.Trace("service.shutdown.stdinClosed", new Dictionary<string, object?>
            {
                ["service"] = serviceName,
                ["error"] = ex.Message
            });
        }
    }

    private static void RequestShutdown(
        CancellationTokenSource shutdown,
        IDiagnosticSink diagnostics,
        string serviceName,
        string reason)
    {
        try
        {
            if (shutdown.IsCancellationRequested)
                return;

            diagnostics.Trace("service.shutdown.requested", new Dictionary<string, object?>
            {
                ["service"] = serviceName,
                ["reason"] = reason
            });
            shutdown.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private sealed class ShutdownListenerRegistration(
        ConsoleCancelEventHandler cancelHandler,
        EventHandler processExitHandler) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            Console.CancelKeyPress -= cancelHandler;
            AppDomain.CurrentDomain.ProcessExit -= processExitHandler;
        }
    }
}
