using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.packets.send.group;

namespace AetherXIV.Core.Map.Tests;

public sealed class GridaniaWireSafetyTests
{
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
            GroupMember.ForActor(0x45000001, uint.MaxValue, "Test Player", true),
            GroupMember.ForActor(0x45000006, 2300120, "", false),
            GroupMember.ForActor(0x45000007, 1400004, "", false)
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
}
