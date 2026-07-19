using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class PlayerPacketCodecTests
{
    [Fact]
    public void SetMusicCodecMatchesLegacyPayloadShape()
    {
        SetMusicPacketCodec codec = new();

        SubPacket packet = codec.Encode(0x10001, new SetMusicPacket(7, SetMusicPacketCodec.EffectImmediate));

        Assert.Equal(PacketOpcode.SetMusic, packet.Header.Opcode);
        Assert.Equal(0u, packet.Header.SourceActorId);
        Assert.Equal(new byte[] { 0x07, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 }, packet.Payload.ToArray());
        Assert.Equal(new SetMusicPacket(7, SetMusicPacketCodec.EffectImmediate), codec.Decode(packet));
    }

    [Fact]
    public void GenericDataCodecMatchesLegacyPayloadSizeAndLuaParameterShape()
    {
        GenericDataPacketCodec codec = new();
        GenericDataPacket data = new(
            [
                new LuaParameter(LuaParameterType.String, "attention"),
                new LuaParameter(LuaParameterType.ActorId, 0xE0000000u),
                new LuaParameter(LuaParameterType.String, ""),
                new LuaParameter(LuaParameterType.Int32, 51073),
                new LuaParameter(LuaParameterType.Int32, 3)
            ]);

        SubPacket packet = codec.Encode(0x10001, data);

        Assert.Equal(PacketOpcode.GenericData, packet.Header.Opcode);
        Assert.Equal(0x10001u, packet.Header.SourceActorId);
        Assert.Equal(GenericDataPacketCodec.PayloadSize, packet.Payload.Length);
        Assert.Equal(0x0F, packet.Payload.Span[28]);
        Assert.All(packet.Payload.ToArray().Skip(29), value => Assert.Equal(0, value));
        Assert.Equal(data.Parameters, codec.Decode(packet).Parameters);
    }

    [Fact]
    public void GenericDataCodecRejectsOversizedLuaPayloads()
    {
        GenericDataPacketCodec codec = new();
        string oversized = new('x', GenericDataPacketCodec.PayloadSize);

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            codec.Encode(0x10001, new GenericDataPacket([new LuaParameter(LuaParameterType.String, oversized)])));

        Assert.Contains("Generic data Lua parameters require", error.Message);
    }
}
