using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class ChatPacketCodecTests
{
    [Fact]
    public void ClientChatRoundTripsLegacyMapLayout()
    {
        ClientChatMessagePacketCodec codec = new();
        ClientChatMessagePacket expected = new(
            0x0102030405060708,
            1.25f,
            2.5f,
            3.75f,
            4.5f,
            ChatMessageType.Say,
            "Hello from the Twelveswood");

        SubPacket encoded = codec.Encode(0x029B2941, expected);

        Assert.Equal(PacketOpcode.ClientChatMessage, encoded.Header.Opcode);
        Assert.Equal(ClientChatMessagePacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(expected, codec.Decode(encoded));
    }

    [Fact]
    public void ServerChatRoundTripsLegacyMapLayout()
    {
        ServerChatMessagePacketCodec codec = new();
        ServerChatMessagePacket expected = new(
            "Ian Thirteen",
            ChatMessageType.Shout,
            "Testing local visibility");

        SubPacket encoded = codec.Encode(0x029B2941, expected);

        Assert.Equal(PacketOpcode.ClientChatMessage, encoded.Header.Opcode);
        Assert.Equal(ServerChatMessagePacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(expected, codec.Decode(encoded));
    }

    [Fact]
    public void ClientChatRejectsTruncatedPayload()
    {
        ClientChatMessagePacketCodec codec = new();
        SubPacket truncated = SubPacket.Create(
            PacketOpcode.ClientChatMessage,
            0x029B2941,
            new byte[ClientChatMessagePacketCodec.PayloadSize - 1]);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() => codec.Decode(truncated));

        Assert.Contains("requires at least", error.Message, StringComparison.Ordinal);
    }
}
