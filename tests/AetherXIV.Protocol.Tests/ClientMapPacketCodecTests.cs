using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class ClientMapPacketCodecTests
{
    [Theory]
    [InlineData("00000000636F6D306731303500000000000000000000000000000000000000000000000001010004", 0u, "com0g105", 0x04000101u)]
    [InlineData("02000000636F6D3067313035000000000000000000000000000000000000000000000000D0EB1800", 2u, "com0g105", 0x0018EBD0u)]
    public void ClientCutsceneStateDecodesOfficialRetailPayload(
        string payloadHex,
        uint state,
        string cutsceneName,
        uint detail)
    {
        SubPacket packet = SubPacket.Create(
            PacketOpcode.SetActorPosition,
            43723073,
            Convert.FromHexString(payloadHex));

        ClientCutsceneStatePacket decoded = new ClientCutsceneStatePacketCodec().Decode(packet);

        Assert.Equal(state, decoded.State);
        Assert.Equal(cutsceneName, decoded.CutsceneName);
        Assert.Equal(detail, decoded.Detail);
    }

    [Fact]
    public void ItemPackageUpdateRequestRoundTripsWorkingServerLayout()
    {
        ClientUpdateItemPackagePacketCodec codec = new();
        ClientUpdateItemPackagePacket expected = new(0x029B2941, 99);

        SubPacket encoded = codec.Encode(0x029B2941, expected);

        Assert.Equal(PacketOpcode.ClientUpdateItemPackage, encoded.Header.Opcode);
        Assert.Equal(8, encoded.Payload.Length);
        Assert.Equal(expected, codec.Decode(encoded));
    }

    [Fact]
    public void ClientPositionPacketUsesTraceConfirmedUpdateOpcodeAndLegacyLayout()
    {
        ClientPlayerPositionPacket packet = new(0x0102030405060708, 10.5f, 20.5f, 30.5f, 1.25f, 2);
        ClientPlayerPositionPacketCodec codec = new();

        SubPacket encoded = codec.Encode(0x10001, packet);
        ClientPlayerPositionPacket decoded = codec.Decode(encoded);

        Assert.Equal((ushort)0x00CA, (ushort)encoded.Header.Opcode);
        Assert.Equal(0x0102030405060708ul, PacketBinary.ReadUInt64LittleEndian(encoded.Payload.Span));
        Assert.Equal(10.5f, PacketBinary.ReadSingleLittleEndian(encoded.Payload.Span[8..]));
        Assert.Equal(2, PacketBinary.ReadUInt16LittleEndian(encoded.Payload.Span[24..]));
        Assert.Equal(packet, decoded);
        Assert.Equal((ushort)PacketOpcode.AddActor, (ushort)PacketOpcode.ClientUpdatePosition);
    }

    [Fact]
    public void ClientTargetPacketDistinguishesClearAndAutoAttackRequests()
    {
        ClientSetTargetPacketCodec codec = new();
        SubPacket selected = codec.Encode(
            0x10001,
            new ClientSetTargetPacket(0x40001234, 0x40001234));
        SubPacket cleared = codec.Encode(
            0x10001,
            new ClientSetTargetPacket(ClientSetTargetPacket.InvalidActorId, ClientSetTargetPacket.InvalidActorId));

        ClientSetTargetPacket selectedDecoded = codec.Decode(selected);
        ClientSetTargetPacket clearedDecoded = codec.Decode(cleared);

        Assert.Equal((ushort)0x00CD, (ushort)selected.Header.Opcode);
        Assert.False(selectedDecoded.IsClear);
        Assert.True(selectedDecoded.RequestsAutoAttack);
        Assert.True(clearedDecoded.IsClear);
        Assert.False(clearedDecoded.RequestsAutoAttack);
    }

    [Fact]
    public void ClientActorInstantiateAcknowledgeUsesTraceObservedShortDirectionSpecificLayout()
    {
        ClientActorInstantiateAcknowledgePacketCodec codec = new();
        ClientActorInstantiateAcknowledgePacket packet = new(0x46700082, 0);

        SubPacket encoded = codec.Encode(0x10001, packet);
        ClientActorInstantiateAcknowledgePacket decoded = codec.Decode(encoded);

        Assert.Equal((ushort)0x00CC, (ushort)encoded.Header.Opcode);
        Assert.Equal((ushort)PacketOpcode.ActorInstantiate, (ushort)encoded.Header.Opcode);
        Assert.Equal(8, encoded.Payload.Length);
        Assert.Equal(packet, decoded);
    }

    [Fact]
    public void ClientLockTargetPreservesCombatContextAndClearSentinel()
    {
        ClientLockTargetPacketCodec codec = new();
        ClientLockTargetPacket locked = new(0x44D035D0, 0x0466E6A4);
        ClientLockTargetPacket clear = new(ClientLockTargetPacket.ClearActorId, 0x00CF97AA);

        Assert.Equal(locked, codec.Decode(codec.Encode(1, locked)));
        Assert.False(locked.IsClear);
        Assert.Equal(clear, codec.Decode(codec.Encode(1, clear)));
        Assert.True(clear.IsClear);
    }

    [Fact]
    public void ClientParameterDataRequestSharesOpcodeWithServerKickEventByDirection()
    {
        ClientParameterDataRequestPacketCodec codec = new();
        SubPacket encoded = codec.Encode(
            0x10001,
            new ClientParameterDataRequestPacket(0x10001, "charaWork/exp"));

        ClientParameterDataRequestPacket decoded = codec.Decode(encoded);

        Assert.Equal((ushort)0x012F, (ushort)encoded.Header.Opcode);
        Assert.Equal((ushort)PacketOpcode.KickEvent, (ushort)PacketOpcode.ClientParameterDataRequest);
        Assert.Equal(0x10001u, decoded.ActorId);
        Assert.Equal("charaWork/exp", decoded.ParameterName);
    }

    [Fact]
    public void BasicLoginSidebandPacketsUseLegacyLayouts()
    {
        SubPacket ping = new MapPingPacketCodec().Encode(0x10001, new MapPingPacket(1234));
        SubPacket pong = new MapPongPacketCodec().Encode(0x10001, new MapPongPacket(1234));
        SubPacket handshake = new MapLoginHandshakeResponsePacketCodec().Encode(
            0x10001,
            new MapLoginHandshakeResponsePacket(0x10001));
        SubPacket language = new ClientLanguageCodePacketCodec().Encode(0x10001, new ClientLanguageCodePacket(1));
        SubPacket zoneIn = new ClientZoneInCompletePacketCodec().Encode(0x10001, new ClientZoneInCompletePacket(55, -1));
        SubPacket group = new ClientGroupCreatedPacketCodec().Encode(0x10001, new ClientGroupCreatedPacket(99, "playerWork"));

        Assert.Equal(new MapPingPacket(1234), new MapPingPacketCodec().Decode(ping));
        Assert.Equal(new MapPongPacket(1234), new MapPongPacketCodec().Decode(pong));
        Assert.Equal(MapPingPacketCodec.PayloadSize, ping.Payload.Length);
        Assert.Equal(MapPongPacketCodec.PayloadSize, pong.Payload.Length);
        Assert.Equal(
            new MapLoginHandshakeResponsePacket(0x10001),
            new MapLoginHandshakeResponsePacketCodec().Decode(handshake));
        Assert.Equal(0x10, handshake.Payload.Length);
        Assert.Equal(0x10001u, PacketBinary.ReadUInt32LittleEndian(handshake.Payload.Span[0x08..]));
        Assert.Equal(new ClientLanguageCodePacket(1), new ClientLanguageCodePacketCodec().Decode(language));
        Assert.Equal(new ClientZoneInCompletePacket(55, -1), new ClientZoneInCompletePacketCodec().Decode(zoneIn));
        Assert.Equal(new ClientGroupCreatedPacket(99, "playerWork"), new ClientGroupCreatedPacketCodec().Decode(group));
    }
}
