using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.Actors.Chara;
using AetherXIV.Core.Map.actors.group;
using AetherXIV.Core.Map.packets.send.actor;
using AetherXIV.Core.Map.packets.send.actor.battle;
using AetherXIV.Core.Map.packets.send.group;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Interop;

namespace AetherXIV.Core.Map.Tests;

public sealed class GridaniaWireSafetyTests
{
    [Fact]
    public void QuestRuntimeExposesSequenceAndFourPersistentCounters()
    {
        Quest quest = new(0xA0F00000 | 110006u, "Man0g1");
        QuestData data = quest.GetData();

        Assert.Equal(0u, quest.GetSequence());
        Assert.Equal(quest.GetSequence(), quest.getSequence());
        data.SetCounter(0, 5);
        data.SetCounter(3, ushort.MaxValue);
        Assert.Equal(6u, data.IncCounter(0));
        Assert.Equal(ushort.MaxValue - 1u, data.DecCounter(3));
        Assert.Equal(0u, data.GetCounter(4));
    }

    [Fact]
    public void QuestRuntimeMethodsBindWithTheLuaSignaturesUsedByStoryScripts()
    {
        UserData.RegistrationPolicy = InteropRegistrationPolicy.Automatic;
        Quest quest = new(0xA0F00000 | 110006u, "Man0g1");
        Script script = new();
        script.Globals["quest"] = DynValue.FromObject(script, quest);

        script.DoString("quest:SetENpc(1000230, 2); quest:GetData():SetCounter(1, 15)");

        Assert.Equal(15u, quest.GetData().GetCounter(1));
    }

    [Fact]
    public void GridaniaPostWarpDirectorRunsTutorialAndAlwaysClosesTheNotice()
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "Data", "scripts", "directors", "AfterQuestWarpDirector.lua");
        string source = File.ReadAllText(path)
            .Replace("require(\"global\")", "", StringComparison.Ordinal);

        Script script = new();
        DynValue result = script.DoString(source + """

            local calls = { ended = 0, run = nil }
            local quest = {
                GetSequence = function(self)
                    return 5
                end
            }
            local player = {
                HasQuest = function(self, questId)
                    return questId == 110006
                end,
                GetQuest = function(self, questId)
                    return quest
                end,
                RunEventFunction = function(self, functionName, eventPlayer, eventQuest, eventName)
                    calls.run = { functionName, eventPlayer, eventQuest, eventName }
                end,
                EndEvent = function(self)
                    calls.ended = calls.ended + 1
                end
            }

            onEventStarted(player, {}, "noticeEvent", true)
            return calls.ended,
                calls.run[1],
                calls.run[2] == player,
                calls.run[3] == quest,
                calls.run[4]
            """);

        Assert.Equal(DataType.Tuple, result.Type);
        Assert.Equal(1d, result.Tuple[0].Number);
        Assert.Equal("delegateEvent", result.Tuple[1].String);
        Assert.True(result.Tuple[2].Boolean);
        Assert.True(result.Tuple[3].Boolean);
        Assert.Equal("processEventTu_001", result.Tuple[4].String);
    }

    [Fact]
    public void PlayerExposesTheGilRewardContractUsedByQuestLua()
    {
        System.Reflection.MethodInfo? method = typeof(Player).GetMethod(
            "AddGil",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
            binder: null,
            types: [typeof(int)],
            modifiers: null);

        Assert.NotNull(method);
        Assert.Equal(typeof(int), method.ReturnType);
    }

    [Fact]
    public void LuaStoryScriptsEnumerateClrActorListsInsteadOfIndexingThemAsLuaArrays()
    {
        UserData.RegistrationPolicy = InteropRegistrationPolicy.Automatic;
        Script script = new();
        script.Globals["actors"] = DynValue.FromObject(script, new List<int> { 3, 5 });

        DynValue sum = script.DoString("local total = 0; for actor in actors do total = total + actor end; return total");

        Assert.Equal(8d, sum.Number);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            script.DoString("return actors[#actors]"));
    }

    [Fact]
    public void ContentRosterX08MatchesRetailPacketSizeAndOffset()
    {
        List<GroupMember> members = Enumerable.Range(1, 10)
            .Select(id => new GroupMember((uint)id, -1, 0, false, true, ""))
            .ToList();
        int offset = 8;

        AetherXIV.Core.Common.SubPacket packet = ContentMembersX08Packet.buildPacket(
            0x45000001,
            166,
            1234,
            members,
            ref offset);

        Assert.Equal(0x78, packet.data.Length);
        Assert.Equal(0x98, packet.header.subpacketSize);
        Assert.Equal(10, offset);
        Assert.Equal(9u, BitConverter.ToUInt32(packet.data, 0x10));
        Assert.Equal(10u, BitConverter.ToUInt32(packet.data, 0x1C));
        Assert.Equal(2, BitConverter.ToInt32(packet.data, 0x70));
    }

    [Fact]
    public void NpcPropertiesAlwaysClearPlayerOwnedBitTwo()
    {
        Assert.Equal(0b1011u, NpcPropertyPolicy.Sanitize(0b1111u));
        Assert.Equal(0u, NpcPropertyPolicy.Sanitize(1u << 2));
    }

    [Fact]
    public void TutorialPartyRowsUseLocalizedNpcNamesAndRetailFlags()
    {
        List<GroupMember> members =
        [
            GroupMember.ForActor(0x45000001, uint.MaxValue, "Test Player", true, true),
            GroupMember.ForActor(0x45000006, 2300120, "yda", false, false),
            GroupMember.ForActor(0x45000007, 1400004, "papalymo", false, false)
        ];
        int offset = 0;

        AetherXIV.Core.Common.SubPacket packet = GroupMembersX08Packet.buildPacket(
            0x45000001,
            166,
            1234,
            members,
            ref offset);

        Assert.Equal(3, offset);
        Assert.Equal(-1, BitConverter.ToInt32(packet.data, 0x14));
        Assert.Equal("Test Player", System.Text.Encoding.ASCII.GetString(packet.data, 0x1E, 0x20).TrimEnd('\0'));
        Assert.Equal(2300120, BitConverter.ToInt32(packet.data, 0x44));
        Assert.Equal(1, packet.data[0x4C]);
        Assert.All(packet.data[0x4E..0x6E], value => Assert.Equal(0, value));
        Assert.Equal(1400004, BitConverter.ToInt32(packet.data, 0x74));
        Assert.Equal(1, packet.data[0x7C]);
        Assert.Equal(3, BitConverter.ToInt32(packet.data, 0x190));
    }

    [Fact]
    public void GroupHeadersUseRetailPartyAndContentEnvelopes()
    {
        TestGroup party = new(0x8000000000000042, Group.PlayerPartyGroup, 3);
        TestGroup content = new(0x3000000000000042, Group.ContentGroup_SimpleContentGroup24B, 7);

        AetherXIV.Core.Common.SubPacket partyPacket = GroupHeaderPacket.buildPacket(1, 166, 1234, party);
        AetherXIV.Core.Common.SubPacket contentPacket = GroupHeaderPacket.buildPacket(1, 166, 1234, content);

        Assert.Equal(0ul, BitConverter.ToUInt64(partyPacket.data, 0x10));
        Assert.Equal(0ul, BitConverter.ToUInt64(partyPacket.data, 0x18));
        Assert.Equal(0x3F3Eu, BitConverter.ToUInt32(partyPacket.data, 0x64));
        Assert.Equal(3ul, BitConverter.ToUInt64(contentPacket.data, 0x10));
        Assert.Equal(content.groupIndex, BitConverter.ToUInt64(contentPacket.data, 0x18));
        Assert.Equal(0u, BitConverter.ToUInt32(contentPacket.data, 0x64));
        Assert.Equal(0u, BitConverter.ToUInt32(contentPacket.data, 0x70));
    }

    [Fact]
    public void TutorialPartyCompositionGetsFreshClientGroupIndex()
    {
        const ulong solo = 0x8000000000000042;

        ulong first = Party.BuildClientGroupIndex(0x42, 3, 1, solo);
        ulong second = Party.BuildClientGroupIndex(0x42, 2, 2, solo);

        Assert.Equal(0x8000000000420001ul, first);
        Assert.Equal(0x8000000000420002ul, second);
        Assert.NotEqual(first, second);
        Assert.Equal(solo, Party.BuildClientGroupIndex(0x42, 1, 3, solo));
    }

    [Fact]
    public void ZoneInstanceSnapshotPacketsMatchRetailWireShapes()
    {
        const uint playerId = 0x45000001;
        uint[] actors =
        [
            playerId,
            0x44880002,
            0x5FF80002,
            0x5FF80001,
            0x44880044,
            0x44880001,
            0x448800EC,
            0x44880090
        ];

        AetherXIV.Core.Common.SubPacket begin = ServerZoneInstanceBeginPacket.BuildPacket(playerId);
        AetherXIV.Core.Common.SubPacket body = ServerZoneInstanceActorsPacket.BuildPacket(playerId, actors);
        AetherXIV.Core.Common.SubPacket end = ServerZoneInstanceEndPacket.BuildPacket(playerId);

        Assert.Equal((ushort)0x0003, begin.header.type);
        Assert.Equal((ushort)0x0006, begin.gameMessage.opcode);
        Assert.Equal(0x28, begin.header.subpacketSize);
        Assert.Equal((ushort)0x0003, body.header.type);
        Assert.Equal((ushort)0x0008, body.gameMessage.opcode);
        Assert.Equal(0x50, body.header.subpacketSize);
        Assert.Equal(8u, BitConverter.ToUInt32(body.data, 0));
        Assert.Equal(actors, Enumerable.Range(0, actors.Length)
            .Select(index => BitConverter.ToUInt32(body.data, 4 + index * sizeof(uint)))
            .ToArray());
        Assert.Equal((ushort)0x0003, end.header.type);
        Assert.Equal((ushort)0x0007, end.gameMessage.opcode);
        Assert.Equal(0x28, end.header.subpacketSize);
    }

    [Fact]
    public void ZoneInstanceActorPacketRejectsInvalidChunks()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ServerZoneInstanceActorsPacket.BuildPacket(1, Array.Empty<uint>()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ServerZoneInstanceActorsPacket.BuildPacket(1, Enumerable.Range(1, 9).Select(id => (uint)id).ToArray()));
    }

    [Fact]
    public void BattleNpcNameplatesStartInRetailPassiveState()
    {
        Assert.Equal(1, NpcWork.HATE_TYPE_PASSIVE);
        Assert.NotEqual(NpcWork.HATE_TYPE_NONE, NpcWork.HATE_TYPE_PASSIVE);
    }

    [Fact]
    public void EnmityIndicatorMatchesRetailCombatTraceShape()
    {
        AetherXIV.Core.Common.SubPacket packet = SetEnmityIndicatorPacket.BuildPacket(
            0x44D035D5,
            0x029B2941,
            100);

        Assert.Equal((ushort)0x0195, packet.gameMessage.opcode);
        Assert.Equal(0x28, packet.header.subpacketSize);
        Assert.Equal(0x029B2941u, BitConverter.ToUInt32(packet.data, 0));
        Assert.Equal((ushort)100, BitConverter.ToUInt16(packet.data, 4));
        Assert.Equal((ushort)0, BitConverter.ToUInt16(packet.data, 6));
    }

    private sealed class TestGroup(ulong groupIndex, uint typeId, int memberCount) : Group(groupIndex)
    {
        public override uint GetTypeId() => typeId;
        public override int GetMemberCount() => memberCount;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AetherXIV.sln")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Data", "scripts")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the AetherXIV repository root.");
    }
}
