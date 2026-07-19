using AetherXIV.Core;
using AetherXIV.Protocol;
using AetherXIV.Server.Hosting;
using System.Text.Json;

namespace AetherXIV.Server.Hosting.Tests;

public sealed class LegacyTransportTests
{
    [Fact]
    public void TraceObservedFrameConstantsRemainCanonical()
    {
        // Verified from the retail trace pass:
        // - outer app frame header is 16 bytes
        // - gameplay inner messages use type 3
        // - type-3 game header category/marker is 0x0014
        Assert.Equal(0x10, BasePacketFrameCodec.HeaderSize);
        Assert.Equal(0x03, BasePacketFrameCodec.GameMessageSubPacketType);
        Assert.Equal(0x14, BasePacketFrameCodec.GameMessageHeaderMarker);
    }

    [Fact]
    public async Task NoOpLegacyFrameConnectionRoundTripsAType3Category14Packet()
    {
        await using MemoryStream stream = new();
        LegacyFrameConnection connection = new(
            stream,
            LegacyNoOpStreamCipher.Instance,
            maxFrameSize: 4096);

        SubPacket packet = SubPacket.Create(
            PacketOpcode.ClientUpdatePosition,
            sourceActorId: 0xE0000001,
            payload: new byte[] { 1, 2, 3, 4 });

        await connection.WriteFrameAsync(
            new List<WireGameMessageSubPacket>
            {
                new(packet, TargetActorId: 0xE0000002, GameTimestamp: 1234)
            },
            isAuthenticated: true,
            connectionType: 2,
            timestamp: 42);

        stream.Position = 0;
        BasePacketFrame? decoded = await connection.ReadFrameAsync();

        Assert.NotNull(decoded);
        Assert.True(decoded.Value.Header.IsAuthenticated);
        Assert.False(decoded.Value.Header.IsCompressed);
        Assert.Equal((ushort)2, decoded.Value.Header.ConnectionType);
        Assert.Equal((ushort)1, decoded.Value.Header.SubPacketCount);
        Assert.Single(decoded.Value.SubPackets);
        Assert.Equal(PacketOpcode.ClientUpdatePosition, decoded.Value.SubPackets[0].Packet.Header.Opcode);
        Assert.Equal(0xE0000001u, decoded.Value.SubPackets[0].Packet.Header.SourceActorId);
        Assert.Equal(0xE0000002u, decoded.Value.SubPackets[0].TargetActorId);
        Assert.Equal(1234u, decoded.Value.SubPackets[0].GameTimestamp);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, decoded.Value.SubPackets[0].Packet.Payload.ToArray());
    }

    [Fact]
    public async Task ReadFrameRejectsImpossibleFrameSize()
    {
        byte[] invalidHeader = new byte[BasePacketFrameCodec.HeaderSize];
        invalidHeader[0x04] = 0x08;
        invalidHeader[0x05] = 0x00;

        await using MemoryStream stream = new(invalidHeader, writable: true);
        LegacyFrameConnection connection = new(stream, LegacyNoOpStreamCipher.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            _ = await connection.ReadFrameAsync();
        });
    }

    [Fact]
    public async Task ReadFrameRejectsFramesAboveConfiguredMaximum()
    {
        byte[] invalidHeader = new byte[BasePacketFrameCodec.HeaderSize];
        invalidHeader[0x04] = 0x00;
        invalidHeader[0x05] = 0x20;

        await using MemoryStream stream = new(invalidHeader, writable: true);
        LegacyFrameConnection connection = new(
            stream,
            LegacyNoOpStreamCipher.Instance,
            maxFrameSize: 1024);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            _ = await connection.ReadFrameAsync();
        });
    }

    [Fact]
    public async Task ReadLegacyFrameDecodesTraceConfirmedCompressedWorldFrame()
    {
        byte[] frameBytes = LoadWorldFixtureFramePayload(frameIndex: 4);
        await using MemoryStream stream = new(frameBytes, writable: true);
        LegacyFrameConnection connection = new(
            stream,
            LegacyNoOpStreamCipher.Instance,
            maxFrameSize: 4096);

        LegacyPacketFrame? decoded = await connection.ReadLegacyFrameAsync();

        Assert.NotNull(decoded);
        Assert.True(decoded.Value.Header.IsAuthenticated);
        Assert.True(decoded.Value.Header.IsCompressed);
        Assert.Equal((ushort)0, decoded.Value.Header.ConnectionType);
        Assert.Equal(frameBytes.Length, decoded.Value.Header.PacketSize);
        WireLegacySubPacket subPacket = Assert.Single(decoded.Value.SubPackets);
        Assert.True(subPacket.IsGameMessage);
        Assert.Equal(PacketOpcode.Pong, subPacket.Opcode);
        Assert.Equal(0xFED2E000u, subPacket.HeaderUnknown);
        Assert.Equal(0x50E0F2CBu, subPacket.GameTimestamp);
        Assert.Equal(32, subPacket.Payload.Length);
    }

    [Fact]
    public void WorldZoneChangeRouteRequestRoundTripsInternalPayload()
    {
        WorldZoneChangeRouteRequest request = new(
            SessionId: 42,
            DestinationZoneId: new ZoneId(175),
            PrivateAreaName: "PrivateAreaMasterPast",
            PrivateAreaLevel: 3,
            SpawnType: 15,
            X: -22.81f,
            Y: 196,
            Z: 87.82f,
            Rotation: 2.98f);

        byte[] payload = WorldMapRoutePackets.EncodeZoneChangeRequest(request);
        WorldZoneChangeRouteRequest decoded = WorldMapRoutePackets.DecodeZoneChangeRequest(payload);

        Assert.True(payload.Length > WorldMapRoutePackets.ZoneChangeRequestPayloadSize);
        Assert.Equal(request, decoded);
    }

    private static byte[] LoadWorldFixtureFramePayload(int frameIndex)
    {
        string fixturePath = FindFixturePath(
            Path.Combine("tests", "fixtures", "trace-evidence", "world-gridania-to-coerthas-observed.json"));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(fixturePath));
        JsonElement frame = document.RootElement
            .GetProperty("captures")
            .EnumerateArray()
            .Single()
            .GetProperty("frames")
            .EnumerateArray()
            .Single(item => item.GetProperty("frameIndex").GetInt32() == frameIndex);

        Assert.Equal("TraceConfirmed", frame.GetProperty("evidenceStatus").GetString());
        Assert.Equal("World", frame.GetProperty("service").GetString());
        return Convert.FromHexString(frame.GetProperty("payloadHex").GetString() ?? String.Empty);
    }

    private static string FindFixturePath(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find fixture {relativePath}.");
    }
}
