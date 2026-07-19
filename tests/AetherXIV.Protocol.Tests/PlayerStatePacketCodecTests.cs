using System.Text.Json;

namespace AetherXIV.Protocol.Tests;

public sealed class PlayerStatePacketCodecTests
{
    [Fact]
    public void PlayerStateCodecsRoundTripOfficialWorldStreamLayouts()
    {
        using JsonDocument fixture = LoadFixture();
        JsonElement root = fixture.RootElement;
        Assert.Equal("aetherxiv.trace.player-state.v1", root.GetProperty("schema").GetString());
        Assert.Equal("TraceConfirmed", root.GetProperty("evidenceStatus").GetString());

        AssertRoundTrip(
            Message(root, "0x0194"),
            PacketOpcode.GrandCompanyState,
            packet => new GrandCompanyStatePacketCodec().Encode(1, new GrandCompanyStatePacketCodec().Decode(packet)));
        AssertRoundTrip(
            Message(root, "0x019D"),
            PacketOpcode.PlayerTitleState,
            packet => new PlayerTitleStatePacketCodec().Encode(1, new PlayerTitleStatePacketCodec().Decode(packet)));
        AssertRoundTrip(
            Message(root, "0x01A4"),
            PacketOpcode.CurrentJobState,
            packet => new CurrentJobStatePacketCodec().Encode(1, new CurrentJobStatePacketCodec().Decode(packet)));
        AssertRoundTrip(
            Message(root, "0x0196"),
            PacketOpcode.SpecialEventWorkState,
            packet => new SpecialEventWorkStatePacketCodec().Encode(1, new SpecialEventWorkStatePacketCodec().Decode(packet)));
        AssertRoundTrip(
            Message(root, "0x019A"),
            PacketOpcode.CompletedAchievementsState,
            packet => new CompletedAchievementsStatePacketCodec().Encode(1, new CompletedAchievementsStatePacketCodec().Decode(packet)));
        AssertRoundTrip(
            Message(root, "0x019B"),
            PacketOpcode.LatestAchievementsState,
            packet => new LatestAchievementsStatePacketCodec().Encode(1, new LatestAchievementsStatePacketCodec().Decode(packet)));
        AssertRoundTrip(
            Message(root, "0x019C"),
            PacketOpcode.AchievementPointsState,
            packet => new AchievementPointsStatePacketCodec().Encode(1, new AchievementPointsStatePacketCodec().Decode(packet)));

        LatestAchievementsStatePacket latest = new LatestAchievementsStatePacketCodec().Decode(
            EvidenceSubPacket(Message(root, "0x019B"), PacketOpcode.LatestAchievementsState));
        Assert.Equal([212u, 208u, 216u, 207u, 211u, 206u, 234u, 233u], latest.AchievementIds);
        AchievementPointsStatePacket points = new AchievementPointsStatePacketCodec().Decode(
            EvidenceSubPacket(Message(root, "0x019C"), PacketOpcode.AchievementPointsState));
        Assert.Equal(175ul, points.Points);
    }

    private static void AssertRoundTrip(
        JsonElement evidence,
        PacketOpcode opcode,
        Func<SubPacket, SubPacket> roundTrip)
    {
        SubPacket decoded = EvidenceSubPacket(evidence, opcode);
        SubPacket encoded = roundTrip(decoded);
        Assert.Equal(decoded.Payload.ToArray(), encoded.Payload.ToArray());
        WireLegacySubPacket wire = WireLegacySubPacket.FromGame(encoded, 1, gameTimestamp: 1);
        Assert.Equal(evidence.GetProperty("subPacketLength").GetInt32(), new RawLegacySubPacketCodec().Encode(wire).Length);
    }

    private static SubPacket EvidenceSubPacket(JsonElement evidence, PacketOpcode opcode) =>
        SubPacket.Create(opcode, 1, Convert.FromHexString(evidence.GetProperty("payloadHex").GetString()!));

    private static JsonElement Message(JsonElement root, string opcode) =>
        root.GetProperty("messages")
            .EnumerateArray()
            .Single(message => message.GetProperty("opcodeKey").GetString() == opcode);

    private static JsonDocument LoadFixture()
    {
        string relativePath = Path.Combine(
            "tests",
            "fixtures",
            "trace-evidence",
            "world-login-player-state-observed.json");
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
                return JsonDocument.Parse(File.ReadAllBytes(candidate));
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find fixture {relativePath}.");
    }
}
