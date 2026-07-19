using System.Text.Json;
using AetherXIV.Core;
using AetherXIV.Protocol;

namespace AetherXIV.Server.Hosting.Tests;

public sealed class DiagnosticTraceArtifactTests
{
    [Fact]
    public void FileSinkCreatesDiscoverableRunManifestAndPreservesTypedFields()
    {
        string root = Path.Combine(Path.GetTempPath(), $"aetherxiv-trace-{Guid.NewGuid():N}");
        string? priorRunId = Environment.GetEnvironmentVariable("AETHERXIV_TRACE_RUN_ID");
        try
        {
            Environment.SetEnvironmentVariable("AETHERXIV_TRACE_RUN_ID", "live-test-run");
            FileDiagnosticSink sink = new("AetherXIV.Launcher.Host", root);

            sink.Trace("battle.exp.calculated", new Dictionary<string, object?>
            {
                ["characterId"] = 42u,
                ["baseExperience"] = 150,
                ["committed"] = true
            });

            Assert.Equal(Path.Combine(root, "runs", "live-test-run"), sink.RunDirectory);
            Assert.Equal(sink.RunDirectory, File.ReadAllText(Path.Combine(root, "latest.txt")).Trim());
            Assert.True(File.Exists(Path.Combine(sink.RunDirectory, "AetherXIV.Launcher.Host.manifest.json")));
            using JsonDocument eventDocument = JsonDocument.Parse(File.ReadAllText(sink.EventsFilePath).Trim());
            JsonElement traceEvent = eventDocument.RootElement;
            Assert.Equal("aetherxiv.trace.event.v1", traceEvent.GetProperty("schema").GetString());
            Assert.Equal("live-test-run", traceEvent.GetProperty("runId").GetString());
            Assert.Equal("battle", traceEvent.GetProperty("category").GetString());
            Assert.Equal(42u, traceEvent.GetProperty("characterId").GetUInt32());
            Assert.Equal(150, traceEvent.GetProperty("baseExperience").GetInt32());
            Assert.True(traceEvent.GetProperty("committed").GetBoolean());
            Assert.Equal(1, traceEvent.GetProperty("sequence").GetInt64());
            Assert.False(String.IsNullOrWhiteSpace(traceEvent.GetProperty("eventId").GetString()));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AETHERXIV_TRACE_RUN_ID", priorRunId);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RawWorldRouteTraceIncludesComparableFingerprintWithoutPayloadBytes()
    {
        RecordingSink diagnostics = new();
        await using MemoryStream stream = new();
        RawLegacySubPacketConnection writer = new(
            stream,
            diagnostics: diagnostics,
            diagnosticPrefix: "test.route");
        WireLegacySubPacket packet = WireLegacySubPacket.FromGame(
            SubPacket.Create(PacketOpcode.Ping, 0x10001, new byte[] { 1, 2, 3, 4 }),
            targetActorId: 0x20002);

        await writer.WriteSubPacketAsync(packet);

        (string Name, IReadOnlyDictionary<string, object?> Fields) trace = Assert.Single(diagnostics.Events);
        Assert.Equal("test.route.write", trace.Name);
        Assert.Equal("server-to-client", trace.Fields["direction"]);
        Assert.Equal("0x0001", trace.Fields["opcode"]);
        Assert.Equal(0x10001u, trace.Fields["sourceActorId"]);
        Assert.Equal(0x20002u, trace.Fields["targetActorId"]);
        Assert.Equal(4, trace.Fields["payloadLength"]);
        Assert.Matches("^[0-9a-f]{64}$", Assert.IsType<string>(trace.Fields["encodedSha256"]));
        Assert.DoesNotContain(trace.Fields.Keys, key => key.Contains("payloadHex", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class RecordingSink : IDiagnosticSink
    {
        public List<(string Name, IReadOnlyDictionary<string, object?> Fields)> Events { get; } = [];

        public void Trace(string eventName, IReadOnlyDictionary<string, object?> fields) =>
            Events.Add((eventName, fields));
    }
}
