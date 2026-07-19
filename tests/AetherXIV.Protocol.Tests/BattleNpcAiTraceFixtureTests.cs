using System.Text.Json;

namespace AetherXIV.Protocol.Tests;

public sealed class BattleNpcAiTraceFixtureTests
{
    [Fact]
    public void OfficialNpcEngagementUsesActiveStateAndMovementStateTwo()
    {
        IReadOnlyList<Observation> observations = LoadObservations();
        Observation stateObservation = observations.Single(item => item.FrameIndex == 22 && item.Opcode == 0x0134);
        Observation moveObservation = observations.Single(item => item.FrameIndex == 24);

        SetActorStatePacket state = new SetActorStatePacketCodec().Decode(stateObservation.ToSubPacket());
        MoveActorToPositionPacket move = new MoveActorToPositionPacketCodec().Decode(moveObservation.ToSubPacket());

        Assert.Equal(new SetActorStatePacket(2, 3), state);
        Assert.Equal((ushort)2, move.MoveState);
        Assert.Equal(0x44D035D0u, stateObservation.SourceActorId);
        Assert.Equal(0x44D035D0u, moveObservation.SourceActorId);
    }

    [Fact]
    public void OfficialNpcMeleeCadenceAndDamageAreVariable()
    {
        IReadOnlyList<Observation> observations = LoadObservations();
        Observation[] meleeObservations = observations
            .Where(item => item.FrameIndex is 31 or 46 or 60)
            .ToArray();
        CommandResultX01Packet[] results = meleeObservations
            .Select(item => new CommandResultX01PacketCodec().Decode(item.ToSubPacket()))
            .ToArray();

        Assert.Equal([0x59DB, 0x59DD, 0x59DB], results.Select(result => result.CommandId));
        Assert.Equal([28, 38, 30], results.Select(result => result.Action.Amount));
        Assert.Equal([30301, 30302, 30301], results.Select(result => result.Action.WorldMasterTextId));
        Assert.Equal(4702, meleeObservations[1].Timestamp - meleeObservations[0].Timestamp);
        Assert.Equal(4939, meleeObservations[2].Timestamp - meleeObservations[1].Timestamp);
        Assert.True(results.Select(result => result.Action.Amount).Distinct().Count() > 1);
    }

    [Fact]
    public void OfficialNpcEngagementPublishesClaimedHateState()
    {
        Observation observation = LoadObservations().Single(item => item.FrameIndex == 30);
        SetActorPropertyPacket packet = new SetActorPropertyPacketCodec().Decode(observation.ToSubPacket());

        Assert.Equal("npcWork/hate", packet.Target);
        ActorPropertyValue value = Assert.Single(packet.Values);
        Assert.Equal(ActorPropertyHash.LegacyMurmurHash2("npcWork.hateType"), value.PropertyId);
        Assert.Equal(ActorPropertyValueKind.Byte, value.Kind);
        Assert.Equal((byte)3, value.Value);
    }

    [Fact]
    public void OfficialNpcListedCastHasSeparateStartAndCompletionResults()
    {
        IReadOnlyList<Observation> observations = LoadObservations();
        CommandResultX01Packet start = new CommandResultX01PacketCodec().Decode(
            observations.Single(item => item.FrameIndex == 92).ToSubPacket());
        CommandResultX01Packet complete = new CommandResultX01PacketCodec().Decode(
            observations.Single(item => item.FrameIndex == 104).ToSubPacket());

        Assert.Equal((ushort)0x5A27, start.CommandId);
        Assert.Equal((ushort)0x5A27, complete.CommandId);
        Assert.Equal(0, start.Action.Amount);
        Assert.Equal(49, complete.Action.Amount);
        Assert.NotEqual(start.AnimationId, complete.AnimationId);
    }

    private static IReadOnlyList<Observation> LoadObservations()
    {
        string path = FindFixturePath(Path.Combine(
            "tests",
            "fixtures",
            "trace-evidence",
            "world-battle-npc-ai-observed.json"));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        JsonElement root = document.RootElement;
        Assert.Equal("aetherxiv.trace.fixture.v1", root.GetProperty("schema").GetString());
        Assert.Equal("World", root.GetProperty("service").GetString());
        Assert.Equal(54992, root.GetProperty("serverPort").GetInt32());
        JsonElement capture = root.GetProperty("captures").EnumerateArray().Single();
        Assert.Equal("combat_autoattack.pcapng", capture.GetProperty("capture").GetString());
        Assert.Empty(capture.GetProperty("accessIssues").EnumerateArray());

        return capture.GetProperty("observations")
            .EnumerateArray()
            .Select(item => new Observation(
                item.GetProperty("frameIndex").GetInt32(),
                item.GetProperty("timestamp").GetInt64(),
                Convert.ToUInt16(item.GetProperty("opcodeKey").GetString(), 16),
                item.GetProperty("sourceActorId").GetUInt32(),
                item.GetProperty("payloadHex").GetString() ?? String.Empty,
                item.GetProperty("payloadLength").GetInt32(),
                item.GetProperty("evidenceStatus").GetString() ?? String.Empty))
            .ToArray();
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

    private sealed record Observation(
        int FrameIndex,
        long Timestamp,
        ushort Opcode,
        uint SourceActorId,
        string PayloadHex,
        int PayloadLength,
        string EvidenceStatus)
    {
        public SubPacket ToSubPacket()
        {
            byte[] payload = Convert.FromHexString(PayloadHex);
            Assert.Equal(PayloadLength, payload.Length);
            Assert.Equal("TraceConfirmed", EvidenceStatus);
            return SubPacket.Create((PacketOpcode)Opcode, SourceActorId, payload);
        }
    }
}
