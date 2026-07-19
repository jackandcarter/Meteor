using AetherXIV.Core;

namespace AetherXIV.Server.Hosting;

public interface IAsyncServerLoop
{
    ValueTask RunAsync(CancellationToken cancellationToken = default);
}

public interface IIntervalTickSource : IAsyncDisposable
{
    ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default);
}

public sealed class PeriodicIntervalTickSource : IIntervalTickSource
{
    private readonly PeriodicTimer timer;

    public PeriodicIntervalTickSource(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "Tick interval must be greater than zero.");

        timer = new PeriodicTimer(interval);
    }

    public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
    {
        return timer.WaitForNextTickAsync(cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        timer.Dispose();
        return ValueTask.CompletedTask;
    }
}

public sealed class FixedIntervalServerLoop : IAsyncServerLoop
{
    private readonly string loopName;
    private readonly IIntervalTickSource tickSource;
    private readonly Func<CancellationToken, ValueTask> onTick;
    private readonly IDiagnosticSink diagnostics;
    private readonly bool traceSuccessfulTicks;

    public FixedIntervalServerLoop(
        string loopName,
        IIntervalTickSource tickSource,
        Func<CancellationToken, ValueTask> onTick,
        IDiagnosticSink? diagnostics = null,
        bool traceSuccessfulTicks = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loopName);

        this.loopName = loopName;
        this.tickSource = tickSource;
        this.onTick = onTick;
        this.diagnostics = diagnostics ?? NullDiagnosticSink.Instance;
        this.traceSuccessfulTicks = traceSuccessfulTicks;
    }

    public async ValueTask RunAsync(CancellationToken cancellationToken = default)
    {
        diagnostics.Trace("server.loop.start", new Dictionary<string, object?>
        {
            ["loop"] = loopName
        });

        try
        {
            while (await tickSource.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    await onTick(cancellationToken).ConfigureAwait(false);
                    if (traceSuccessfulTicks)
                    {
                        diagnostics.Trace("server.loop.tick", new Dictionary<string, object?>
                        {
                            ["loop"] = loopName
                        });
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    diagnostics.Trace("server.loop.tick.error", new Dictionary<string, object?>
                    {
                        ["loop"] = loopName,
                        ["error"] = ex.Message,
                        ["exceptionType"] = ex.GetType().FullName
                    });
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            diagnostics.Trace("server.loop.stop", new Dictionary<string, object?>
            {
                ["loop"] = loopName,
                ["cancelled"] = cancellationToken.IsCancellationRequested
            });
        }
    }
}
