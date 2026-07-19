using AetherXIV.Server.Hosting;

namespace AetherXIV.Server.Hosting.Tests;

public sealed class HostingFoundationTests
{
    [Fact]
    public async Task InMemorySessionManagerCanAttachFindAndDetach()
    {
        InMemorySessionManager<TestSession> manager = new();
        SessionKey key = new(100, "zone");

        TestSession attached = await manager.AttachAsync(key);
        TestSession? found = await manager.FindAsync(key);
        await manager.DetachAsync(key, "test complete");

        Assert.Same(attached, found);
        Assert.Null(await manager.FindAsync(key));
    }

    [Fact]
    public async Task FixedIntervalServerLoopRunsOncePerTickAndStopsWhenSourceStops()
    {
        FakeTickSource tickSource = new(3);
        TestDiagnosticSink diagnostics = new();
        int ticks = 0;
        FixedIntervalServerLoop loop = new(
            "test.loop",
            tickSource,
            _ =>
            {
                ticks++;
                return ValueTask.CompletedTask;
            },
            diagnostics);

        await loop.RunAsync();

        Assert.Equal(3, ticks);
        Assert.Contains(diagnostics.Events, item => item.EventName == "server.loop.start");
        Assert.Equal(3, diagnostics.Events.Count(item => item.EventName == "server.loop.tick"));
        Assert.Contains(diagnostics.Events, item => item.EventName == "server.loop.stop");
    }

    [Fact]
    public async Task FixedIntervalServerLoopReportsTickErrorsAndContinues()
    {
        FakeTickSource tickSource = new(2);
        TestDiagnosticSink diagnostics = new();
        int attempts = 0;
        FixedIntervalServerLoop loop = new(
            "test.loop",
            tickSource,
            _ =>
            {
                attempts++;
                if (attempts == 1)
                    throw new InvalidOperationException("first tick failed");

                return ValueTask.CompletedTask;
            },
            diagnostics);

        await loop.RunAsync();

        Assert.Equal(2, attempts);
        Assert.Contains(diagnostics.Events, item => item.EventName == "server.loop.tick.error");
        Assert.Single(diagnostics.Events, item => item.EventName == "server.loop.tick");
    }

    [Fact]
    public async Task FixedIntervalServerLoopCanSuppressSuccessfulTickTrace()
    {
        FakeTickSource tickSource = new(2);
        TestDiagnosticSink diagnostics = new();
        int ticks = 0;
        FixedIntervalServerLoop loop = new(
            "quiet.loop",
            tickSource,
            _ =>
            {
                ticks++;
                return ValueTask.CompletedTask;
            },
            diagnostics,
            traceSuccessfulTicks: false);

        await loop.RunAsync();

        Assert.Equal(2, ticks);
        Assert.Contains(diagnostics.Events, item => item.EventName == "server.loop.start");
        Assert.DoesNotContain(diagnostics.Events, item => item.EventName == "server.loop.tick");
        Assert.Contains(diagnostics.Events, item => item.EventName == "server.loop.stop");
    }

    [Fact]
    public void ServiceOptionsParseBindAdvertiseDatabaseAndTraceArgs()
    {
        AetherXivServiceOptions options = AetherXivServiceOptions.FromArgs(
            "AetherXIV.Launcher.Host",
            new AetherXIV.Core.ServerEndpoint("127.0.0.1", 54993),
            new AetherXIV.Core.ServerEndpoint("127.0.0.1", 54993),
            [
                "--bind", "0.0.0.0:54993",
                "--advertise=game.example.test:54993",
                "--db-host", "db.local",
                "--db-port", "3307",
                "--db-name", "ffxiv_server_live",
                "--db-user", "aether",
                "--db-password", "secret",
                "--diagnostics-dir", "/tmp/aetherxiv-traces",
                "--trace", "true",
                "--data-root", "/srv/aetherxiv"
            ]);

        Assert.Equal("AetherXIV.Launcher.Host", options.ServiceName);
        Assert.Equal(new AetherXIV.Core.ServerEndpoint("0.0.0.0", 54993), options.BindEndpoint);
        Assert.Equal(new AetherXIV.Core.ServerEndpoint("game.example.test", 54993), options.AdvertisedEndpoint);
        Assert.Equal("db.local", options.Database.Host);
        Assert.Equal((ushort)3307, options.Database.Port);
        Assert.Equal("ffxiv_server_live", options.Database.Database);
        Assert.Equal("aether", options.Database.User);
        Assert.Equal("secret", options.Database.Password);
        Assert.True(options.TraceEnabled);
        Assert.Equal("/tmp/aetherxiv-traces", options.DiagnosticsDirectory);
        Assert.Equal("/srv/aetherxiv", options.DataRoot);
    }

    private sealed class TestSession
    {
    }

    private sealed class FakeTickSource : IIntervalTickSource
    {
        private int remainingTicks;

        public FakeTickSource(int ticks)
        {
            remainingTicks = ticks;
        }

        public ValueTask<bool> WaitForNextTickAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return ValueTask.FromCanceled<bool>(cancellationToken);

            if (remainingTicks <= 0)
                return ValueTask.FromResult(false);

            remainingTicks--;
            return ValueTask.FromResult(true);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestDiagnosticSink : AetherXIV.Core.IDiagnosticSink
    {
        public List<(string EventName, IReadOnlyDictionary<string, object?> Fields)> Events { get; } = [];

        public void Trace(string eventName, IReadOnlyDictionary<string, object?> fields)
        {
            Events.Add((eventName, fields));
        }
    }
}
