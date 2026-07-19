using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class ZoneTransitionPacketCodecTests
{
    [Fact]
    public void DeleteAllActorsUsesServiceScopedOpcodeAndEightBytePayload()
    {
        DeleteAllActorsPacketCodec codec = new();

        SubPacket packet = codec.Encode(0x029B2941, new DeleteAllActorsPacket());

        Assert.Equal(PacketOpcode.DeleteAllActors, packet.Header.Opcode);
        Assert.Equal(DeleteAllActorsPacketCodec.PayloadSize, packet.Payload.Length);
        Assert.All(packet.Payload.ToArray(), value => Assert.Equal(0, value));
        codec.Decode(packet);
    }

    [Fact]
    public void ZoneTransitionStateRoundTripsLegacyResetValue()
    {
        ZoneTransitionStatePacketCodec codec = new();

        SubPacket packet = codec.Encode(0x029B2941, new ZoneTransitionStatePacket(0x02));

        Assert.Equal(PacketOpcode.ZoneTransitionState, packet.Header.Opcode);
        Assert.Equal(0x02, codec.Decode(packet).State);
        Assert.All(packet.Payload.Span[1..].ToArray(), value => Assert.Equal(0, value));
    }
}
