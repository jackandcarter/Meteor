using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class BasePacketFrameCodecTests
{
    [Fact]
    public void CompressedBasePacketFramesRoundTripTraceObservedGameMessages()
    {
        BasePacketFrameCodec codec = new();
        SubPacket packet = SubPacket.Create(
            PacketOpcode.MoveActorToPosition,
            sourceActorId: 0xE0000001,
            payload: Enumerable.Range(0, 128).Select(static i => (byte)(i & 0xFF)).ToArray());

        byte[] encoded = codec.Encode(
            [new WireGameMessageSubPacket(packet, TargetActorId: 0xE0000002, GameTimestamp: 0x01020304)],
            isAuthenticated: true,
            timestamp: 0x1122334455667788,
            connectionType: 2,
            isCompressed: true);

        Assert.Equal(1, encoded[0]);
        Assert.Equal(1, encoded[1]);
        Assert.Equal(encoded.Length, PacketBinary.ReadUInt16LittleEndian(encoded.AsSpan(0x04)));
        Assert.Equal(1, PacketBinary.ReadUInt16LittleEndian(encoded.AsSpan(0x06)));

        BasePacketFrame decoded = codec.Decode(encoded);

        Assert.True(decoded.Header.IsAuthenticated);
        Assert.True(decoded.Header.IsCompressed);
        Assert.Equal((ushort)2, decoded.Header.ConnectionType);
        Assert.Equal(0x1122334455667788ul, decoded.Header.Timestamp);
        Assert.Single(decoded.SubPackets);
        Assert.Equal(PacketOpcode.MoveActorToPosition, decoded.SubPackets[0].Packet.Header.Opcode);
        Assert.Equal(0xE0000001u, decoded.SubPackets[0].Packet.Header.SourceActorId);
        Assert.Equal(0xE0000002u, decoded.SubPackets[0].TargetActorId);
        Assert.Equal(0x01020304u, decoded.SubPackets[0].GameTimestamp);
        Assert.Equal(packet.Payload.ToArray(), decoded.SubPackets[0].Packet.Payload.ToArray());
    }

    [Fact]
    public void ObservedProtocolKeyUsesTraceFacingIdentityBeforeFinalPacketNames()
    {
        SubPacket packet = SubPacket.Create(
            PacketOpcode.ClientUpdatePosition,
            sourceActorId: 0xE0000001,
            payload: new byte[32]);
        WireGameMessageSubPacket wire = new(packet, TargetActorId: 0xE0000002);

        ObservedProtocolKey key = ObservedProtocolKey.FromSubPacket(PacketDirection.ClientToServer, wire);

        Assert.Equal(PacketDirection.ClientToServer, key.Direction);
        Assert.Equal(0x0003, key.MessageType);
        Assert.Equal(0x0014, key.Category);
        Assert.Equal(0x00CA, key.Subcode);
        Assert.Equal(0x40, key.MessageLength);
        Assert.Equal("C2S:type=0x0003:cat=0x0014:sub=0x00CA:len=64", key.ToString());
    }
}
