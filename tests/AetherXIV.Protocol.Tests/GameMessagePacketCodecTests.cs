using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class GameMessagePacketCodecTests
{
    [Fact]
    public void MessageWithSourceActorUsesWorkingServerX01Layout()
    {
        GameMessageWithActorPacketCodec codec = new();
        GameMessageWithActorPacket expected = new(0x02BE9B30, 0x5FF80001, 34109, 0x20, []);

        SubPacket encoded = codec.Encode(0x5FF80001, expected);
        GameMessageWithActorPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.GameMessageWithActorX01, encoded.Header.Opcode);
        Assert.Equal(0x10, encoded.Payload.Length);
        Assert.Equal(expected, decoded);
    }

    [Fact]
    public void MessageWithSourceActorParametersSelectsBoundedLayoutAndRoundTrips()
    {
        GameMessageWithActorPacketCodec codec = new();
        GameMessageWithActorPacket expected = new(
            0x02BE9B30,
            0x5FF80001,
            40301,
            0x20,
            [
                new LuaParameter(LuaParameterType.ActorId, 0x02BE9B30u),
                new LuaParameter(LuaParameterType.Int32, 2)
            ]);

        SubPacket encoded = codec.Encode(0x5FF80001, expected);
        GameMessageWithActorPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.GameMessageWithActorX03, encoded.Header.Opcode);
        Assert.Equal(0x20, encoded.Payload.Length);
        Assert.Equal(expected.MessageActorId, decoded.MessageActorId);
        Assert.Equal(expected.TextOwnerActorId, decoded.TextOwnerActorId);
        Assert.Equal(expected.TextId, decoded.TextId);
        Assert.Equal(expected.LogType, decoded.LogType);
        Assert.Equal(expected.Parameters, decoded.Parameters);
    }

    [Fact]
    public void MessageWithoutParametersUsesWorkingServerX01Layout()
    {
        GameMessageWithoutActorPacketCodec codec = new();
        GameMessageWithoutActorPacket expected = new(0xA0F00001, 353, 0x20, []);

        SubPacket encoded = codec.Encode(0x5FF80001, expected);
        GameMessageWithoutActorPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.GameMessageWithoutActorX01, encoded.Header.Opcode);
        Assert.Equal(8, encoded.Payload.Length);
        Assert.Equal(expected.TextOwnerActorId, decoded.TextOwnerActorId);
        Assert.Equal(expected.TextId, decoded.TextId);
        Assert.Equal(expected.LogType, decoded.LogType);
        Assert.Equal(expected.Parameters, decoded.Parameters);
    }

    [Fact]
    public void MessageWithParametersSelectsBoundedLayoutAndRoundTripsLuaValues()
    {
        GameMessageWithoutActorPacketCodec codec = new();
        GameMessageWithoutActorPacket expected = new(
            0x5FF80001,
            30603,
            0x20,
            [
                new LuaParameter(LuaParameterType.Int32, 0),
                new LuaParameter(LuaParameterType.UInt32, 27150u)
            ]);

        SubPacket encoded = codec.Encode(0x5FF80001, expected);
        GameMessageWithoutActorPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.GameMessageWithoutActorX03, encoded.Header.Opcode);
        Assert.Equal(0x18, encoded.Payload.Length);
        Assert.Equal(expected.TextOwnerActorId, decoded.TextOwnerActorId);
        Assert.Equal(expected.TextId, decoded.TextId);
        Assert.Equal(expected.LogType, decoded.LogType);
        Assert.Equal(expected.Parameters, decoded.Parameters);
    }
}
