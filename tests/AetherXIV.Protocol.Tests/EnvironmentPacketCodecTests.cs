using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class EnvironmentPacketCodecTests
{
    [Fact]
    public void SetMapCodecMatchesLegacyPayloadShape()
    {
        SetMapPacketCodec codec = new();

        SubPacket packet = codec.Encode(0x10001, new SetMapPacket(RegionId: 102, ZoneId: 209));

        Assert.Equal(PacketOpcode.MapSetMap, packet.Header.Opcode);
        Assert.Equal(0x10001u, packet.Header.SourceActorId);
        Assert.Equal(
            new byte[]
            {
                0x66, 0x00, 0x00, 0x00,
                0xD1, 0x00, 0x00, 0x00,
                0x28, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00
            },
            packet.Payload.ToArray());
        Assert.Equal(new SetMapPacket(102, 209), codec.Decode(packet));
    }

    [Fact]
    public void SetWeatherCodecMatchesLegacyPayloadShape()
    {
        SetWeatherPacketCodec codec = new();

        SubPacket packet = codec.Encode(0x1234, new SetWeatherPacket(WeatherId.Dalamud, 10));

        Assert.Equal(PacketOpcode.SetWeather, packet.Header.Opcode);
        Assert.Equal(0u, packet.Header.SourceActorId);
        Assert.Equal(new byte[] { 0x5E, 0x1F, 0x0A, 0x00, 0x00, 0x00, 0x00, 0x00 }, packet.Payload.ToArray());
    }

    [Fact]
    public void SetWeatherCodecRoundTripsWeatherAndTransition()
    {
        SetWeatherPacketCodec codec = new();
        SubPacket packet = codec.Encode(0, new SetWeatherPacket(WeatherId.Sandy, 30));

        SetWeatherPacket decoded = codec.Decode(packet);

        Assert.Equal(WeatherId.Sandy, decoded.Weather);
        Assert.Equal((ushort)30, decoded.TransitionTime);
    }

    [Fact]
    public void SetDalamudCodecMatchesLegacyPayloadShape()
    {
        SetDalamudPacketCodec codec = new();

        SubPacket packet = codec.Encode(0x5FF80001, new SetDalamudPacket(-1));

        Assert.Equal(PacketOpcode.SetDalamud, packet.Header.Opcode);
        Assert.Equal(0x5FF80001u, packet.Header.SourceActorId);
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00 }, packet.Payload.ToArray());
    }

    [Fact]
    public void PacketRegistryReturnsTypedCodec()
    {
        PacketRegistry registry = new();
        registry.Register(new SetWeatherPacketCodec());

        IPacketCodec<SetWeatherPacket> codec = registry.Get<SetWeatherPacket>(PacketOpcode.SetWeather);

        Assert.IsType<SetWeatherPacketCodec>(codec);
    }

    [Fact]
    public void PacketRegistryAllowsSameOpcodeToHaveDifferentDirectionMeanings()
    {
        PacketRegistry registry = new();
        registry.Register(PacketDirection.ClientToServer, new EventStartPacketCodec());
        registry.Register(PacketDirection.ServerToClient, new EndEventPacketCodec());

        IPacketCodec<EventStartPacket> clientCodec = registry.Get<EventStartPacket>(
            PacketDirection.ClientToServer,
            PacketOpcode.EventStart);
        IPacketCodec<EndEventPacket> serverCodec = registry.Get<EndEventPacket>(
            PacketDirection.ServerToClient,
            PacketOpcode.EndEvent);

        Assert.IsType<EventStartPacketCodec>(clientCodec);
        Assert.IsType<EndEventPacketCodec>(serverCodec);
    }
}
