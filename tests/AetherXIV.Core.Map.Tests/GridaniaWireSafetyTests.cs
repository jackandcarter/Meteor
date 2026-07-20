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
}
