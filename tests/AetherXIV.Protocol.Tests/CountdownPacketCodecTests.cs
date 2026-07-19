using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class CountdownPacketCodecTests
{
    [Fact]
    public void ClientCountdownRequestRoundTripsWorkingServerLayout()
    {
        ClientCountdownRequestPacketCodec codec = new();
        ClientCountdownRequestPacket expected = new(10, 0x0102030405060708);

        SubPacket encoded = codec.Encode(0x029B2941, expected);

        Assert.Equal((ushort)0x00CF, (ushort)encoded.Header.Opcode);
        Assert.Equal(ClientCountdownRequestPacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal((byte)10, encoded.Payload.Span[0]);
        Assert.Equal(0x0102030405060708ul, PacketBinary.ReadUInt64LittleEndian(encoded.Payload.Span[0x08..]));
        Assert.Equal(expected, codec.Decode(encoded));
    }

    [Fact]
    public void StartCountdownRoundTripsWorkingServerLayout()
    {
        StartCountdownPacketCodec codec = new();
        StartCountdownPacket expected = new(5, 0x1122334455667788, "Go!");

        SubPacket encoded = codec.Encode(0x029B2941, expected);

        Assert.Equal((ushort)0x00E5, (ushort)encoded.Header.Opcode);
        Assert.Equal(StartCountdownPacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(expected, codec.Decode(encoded));
    }

    [Fact]
    public void CountdownCodecsRejectTruncatedPayloads()
    {
        SubPacket client = SubPacket.Create(
            PacketOpcode.ClientCountdownRequest,
            1,
            new byte[ClientCountdownRequestPacketCodec.PayloadSize - 1]);
        SubPacket server = SubPacket.Create(
            PacketOpcode.StartCountdown,
            1,
            new byte[StartCountdownPacketCodec.PayloadSize - 1]);

        Assert.Throws<InvalidDataException>(() => new ClientCountdownRequestPacketCodec().Decode(client));
        Assert.Throws<InvalidDataException>(() => new StartCountdownPacketCodec().Decode(server));
    }
}
