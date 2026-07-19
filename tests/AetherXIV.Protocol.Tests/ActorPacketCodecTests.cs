using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class ActorPacketCodecTests
{
    [Fact]
    public void BasicActorSpawnPacketsUseLegacyOpcodesAndLayouts()
    {
        uint actorId = 0x46800001;
        SubPacket add = new AddActorPacketCodec().Encode(actorId, new AddActorPacket(8));
        SubPacket commandCategory = new PlayerCommandCategoryPacketCodec().Encode(
            actorId,
            new PlayerCommandCategoryPacket(3, "commandContent"));
        SubPacket remove = new RemoveActorPacketCodec().Encode(actorId, new RemoveActorPacket(actorId));
        SubPacket speed = new SetActorSpeedPacketCodec().Encode(actorId, SetActorSpeedPacket.LegacyDefault);
        SubPacket position = new SetActorPositionPacketCodec().Encode(
            actorId,
            new SetActorPositionPacket(actorId, 10, 20, 30, 1.5f, 0, IsZoningPlayer: false));
        SubPacket move = new MoveActorToPositionPacketCodec().Encode(
            actorId,
            new MoveActorToPositionPacket(11, 21, 31, 1.75f, 2));
        SubPacket targetAnimated = new SetActorTargetAnimatedPacketCodec().Encode(
            actorId,
            new SetActorTargetAnimatedPacket(0x40001234));
        SubPacket name = new SetActorNamePacketCodec().Encode(actorId, new SetActorNamePacket(0, "Gogofu"));
        SubPacket state = new SetActorStatePacketCodec().Encode(actorId, new SetActorStatePacket(7, 0));
        SubPacket zoning = new SetActorIsZoningPacketCodec().Encode(actorId, new SetActorIsZoningPacket(false));

        Assert.Equal((ushort)0x00CA, (ushort)add.Header.Opcode);
        Assert.Equal(8, add.Payload.Span[0]);

        Assert.Equal((ushort)0x0132, (ushort)commandCategory.Header.Opcode);
        Assert.Equal(3, PacketBinary.ReadUInt16LittleEndian(commandCategory.Payload.Span));
        Assert.Equal("commandContent", new PlayerCommandCategoryPacketCodec().Decode(commandCategory).FunctionName);

        Assert.Equal((ushort)0x00CB, (ushort)remove.Header.Opcode);
        Assert.Equal(actorId, new RemoveActorPacketCodec().Decode(remove).ActorId);
        Assert.Equal(actorId, remove.Header.SourceActorId);
        Assert.All(remove.Payload.ToArray(), value => Assert.Equal(0, value));

        Assert.Equal((ushort)0x00D0, (ushort)speed.Header.Opcode);
        Assert.Equal(0.0f, PacketBinary.ReadSingleLittleEndian(speed.Payload.Span));
        Assert.Equal(2.0f, PacketBinary.ReadSingleLittleEndian(speed.Payload.Span[8..]));
        Assert.Equal(5.0f, PacketBinary.ReadSingleLittleEndian(speed.Payload.Span[16..]));
        Assert.Equal(5.0f, PacketBinary.ReadSingleLittleEndian(speed.Payload.Span[24..]));
        Assert.Equal(4u, PacketBinary.ReadUInt32LittleEndian(speed.Payload.Span[0x80..]));

        Assert.Equal((ushort)0x00CE, (ushort)position.Header.Opcode);
        Assert.Equal(actorId, PacketBinary.ReadUInt32LittleEndian(position.Payload.Span[4..]));
        Assert.Equal(10.0f, PacketBinary.ReadSingleLittleEndian(position.Payload.Span[8..]));
        Assert.Equal(0, PacketBinary.ReadUInt16LittleEndian(position.Payload.Span[0x24..]));

        Assert.Equal((ushort)0x00CF, (ushort)move.Header.Opcode);
        Assert.Equal(11.0f, PacketBinary.ReadSingleLittleEndian(move.Payload.Span[8..]));
        Assert.Equal(2, new MoveActorToPositionPacketCodec().Decode(move).MoveState);

        Assert.Equal((ushort)0x00D3, (ushort)targetAnimated.Header.Opcode);
        Assert.Equal(0x40001234u, new SetActorTargetAnimatedPacketCodec().Decode(targetAnimated).TargetActorId);

        Assert.Equal((ushort)0x013D, (ushort)name.Header.Opcode);
        Assert.Equal(0u, PacketBinary.ReadUInt32LittleEndian(name.Payload.Span));
        Assert.Equal("Gogofu", new SetActorNamePacketCodec().Decode(name).CustomName);

        Assert.Equal((ushort)0x0134, (ushort)state.Header.Opcode);
        Assert.Equal(7u, new SetActorStatePacketCodec().Decode(state).MainState);

        Assert.Equal((ushort)0x017B, (ushort)zoning.Header.Opcode);
        Assert.Equal(0, zoning.Payload.Span[0]);
    }

    [Fact]
    public void VisualActorSpawnPacketsUseLegacyOpcodesAndLayouts()
    {
        uint actorId = 0x46800001;
        uint[] appearanceIds = Enumerable.Range(0, SetActorAppearancePacketCodec.AppearanceValueCount)
            .Select(index => 0x1000u + (uint)index)
            .ToArray();

        SubPacket appearance = new SetActorAppearancePacketCodec().Encode(
            actorId,
            new SetActorAppearancePacket(0x2000001, appearanceIds));
        SubPacket bgProperties = new SetActorBGPropertiesPacketCodec().Encode(
            actorId,
            new SetActorBGPropertiesPacket(0xABCDEF01, 0x12345678));
        SubPacket subState = new SetActorSubStatePacketCodec().Encode(
            actorId,
            new SetActorSubStatePacket(1, 2, 0xFF, 4, 5, 0x3456));
        SubPacket statusAll = new SetActorStatusAllPacketCodec().Encode(
            actorId,
            new SetActorStatusAllPacket([11, 22, 33]));
        SubPacket icon = new SetActorIconPacketCodec().Encode(
            actorId,
            new SetActorIconPacket(SetActorIconPacket.IsAfk));
        SubPacket questGraphic = new SetActorQuestGraphicPacketCodec().Encode(
            actorId,
            new SetActorQuestGraphicPacket(2));

        Assert.Equal((ushort)0x00D6, (ushort)appearance.Header.Opcode);
        Assert.Equal(0x2000001u, PacketBinary.ReadUInt32LittleEndian(appearance.Payload.Span));
        Assert.Equal(0u, PacketBinary.ReadUInt32LittleEndian(appearance.Payload.Span[4..]));
        Assert.Equal(0x1000u, PacketBinary.ReadUInt32LittleEndian(appearance.Payload.Span[8..]));
        Assert.Equal(27u, PacketBinary.ReadUInt32LittleEndian(appearance.Payload.Span[220..]));
        Assert.Equal(0x101Bu, PacketBinary.ReadUInt32LittleEndian(appearance.Payload.Span[224..]));
        Assert.Equal(28, PacketBinary.ReadInt32LittleEndian(appearance.Payload.Span[0x100..]));
        Assert.Equal(appearanceIds, new SetActorAppearancePacketCodec().Decode(appearance).AppearanceIds);

        Assert.Equal((ushort)0x00D8, (ushort)bgProperties.Header.Opcode);
        Assert.Equal(0xABCDEF01u, PacketBinary.ReadUInt32LittleEndian(bgProperties.Payload.Span));
        Assert.Equal(0x12345678u, PacketBinary.ReadUInt32LittleEndian(bgProperties.Payload.Span[4..]));

        Assert.Equal((ushort)0x0144, (ushort)subState.Header.Opcode);
        Assert.Equal([1, 2, 0x0F, 4, 5, 0], subState.Payload.Span[..6].ToArray());
        Assert.Equal(0x3456, PacketBinary.ReadUInt16LittleEndian(subState.Payload.Span[6..]));

        Assert.Equal((ushort)0x0179, (ushort)statusAll.Header.Opcode);
        Assert.Equal(11, PacketBinary.ReadUInt16LittleEndian(statusAll.Payload.Span));
        Assert.Equal(22, PacketBinary.ReadUInt16LittleEndian(statusAll.Payload.Span[2..]));
        Assert.Equal(33, PacketBinary.ReadUInt16LittleEndian(statusAll.Payload.Span[4..]));
        Assert.Equal(0, PacketBinary.ReadUInt16LittleEndian(statusAll.Payload.Span[6..]));

        Assert.Equal((ushort)0x0145, (ushort)icon.Header.Opcode);
        Assert.Equal(SetActorIconPacket.IsAfk, PacketBinary.ReadUInt32LittleEndian(icon.Payload.Span));

        Assert.Equal((ushort)0x00E3, (ushort)questGraphic.Header.Opcode);
        Assert.Equal(actorId, questGraphic.Header.SourceActorId);
        Assert.Equal(8, questGraphic.Payload.Length);
        Assert.Equal(2u, new SetActorQuestGraphicPacketCodec().Decode(questGraphic).GraphicId);
        Assert.All(questGraphic.Payload.Span[4..].ToArray(), value => Assert.Equal(0, value));
    }

    [Fact]
    public void ActorPropertyPacketUsesLegacyTargetAndValueLayout()
    {
        uint hpHash = ActorPropertyHash.LegacyMurmurHash2("charaWork.parameterSave.hp[0]");
        uint pushHash = ActorPropertyHash.LegacyMurmurHash2("npcWork.pushCommand");
        SetActorPropertyPacket packet = new(
            "/_init",
            [
                ActorPropertyValue.UInt16(hpHash, 80),
                ActorPropertyValue.UInt32(pushHash, 0x0102)
            ],
            HasMore: true);

        SetActorPropertyPacketCodec codec = new();
        SubPacket encoded = codec.Encode(0x46800001, packet);
        SetActorPropertyPacket decoded = codec.Decode(encoded);

        Assert.Equal((ushort)0x0137, (ushort)encoded.Header.Opcode);
        Assert.Equal(0x17, encoded.Payload.Span[0]);
        Assert.Equal(2, encoded.Payload.Span[1]);
        Assert.Equal(hpHash, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[2..]));
        Assert.Equal(80, PacketBinary.ReadUInt16LittleEndian(encoded.Payload.Span[6..]));
        Assert.Equal(4, encoded.Payload.Span[8]);
        Assert.Equal(pushHash, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[9..]));
        Assert.Equal(0x0102u, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[13..]));
        Assert.Equal(0x66, encoded.Payload.Span[17]);
        Assert.Equal("/_init", decoded.Target);
        Assert.True(decoded.HasMore);
        Assert.False(decoded.IsArrayMode);
        Assert.Equal([hpHash, pushHash], decoded.Values.Select(value => value.PropertyId).ToArray());
    }

    [Fact]
    public void ActorPropertyPacketDecodesLongFinalTargetWithoutMistakingItForArrayMode()
    {
        const string target = "charaWork/commandDetailForSelf";
        uint propertyId = ActorPropertyHash.LegacyMurmurHash2(
            "charaWork.parameterSave.commandSlot_compatibility[0]");
        SetActorPropertyPacketCodec codec = new();

        SubPacket encoded = codec.Encode(
            0x46800001,
            new SetActorPropertyPacket(target, [ActorPropertyValue.Byte(propertyId, 1)]));
        SetActorPropertyPacket decoded = codec.Decode(encoded);

        Assert.Equal(0xA0, encoded.Payload.Span[7]);
        Assert.Equal(target, decoded.Target);
        Assert.False(decoded.IsArrayMode);
        Assert.False(decoded.HasMore);
        Assert.Equal(propertyId, Assert.Single(decoded.Values).PropertyId);
    }

    [Fact]
    public void ActorInstantiatePacketUsesLegacyOffsetsAndLuaParameterBlock()
    {
        ActorInstantiatePacket packet = new(
            "pplStd_wil0Twn01a_01@0D100",
            "PopulaceStandard",
            [
                new LuaParameter(LuaParameterType.String, "/chara/npc/populace/PopulaceStandard"),
                new LuaParameter(LuaParameterType.BooleanFalse, null),
                new LuaParameter(LuaParameterType.Int32, 1000001)
            ]);

        ActorInstantiatePacketCodec codec = new();
        SubPacket encoded = codec.Encode(0x46800001, packet);
        ActorInstantiatePacket decoded = codec.Decode(encoded);

        Assert.Equal((ushort)0x00CC, (ushort)encoded.Header.Opcode);
        Assert.Equal(0, PacketBinary.ReadUInt16LittleEndian(encoded.Payload.Span));
        Assert.Equal(0x3040, PacketBinary.ReadUInt16LittleEndian(encoded.Payload.Span[2..]));
        Assert.Equal("pplStd_wil0Twn01a_01@0D100", decoded.ObjectName);
        Assert.Equal("PopulaceStandard", decoded.ClassName);
        Assert.Equal("/chara/npc/populace/PopulaceStandard", decoded.InitParameters[0].Value);
        Assert.Equal(false, decoded.InitParameters[1].Value);
        Assert.Equal(1000001, decoded.InitParameters[2].Value);
    }

    [Fact]
    public void MapPlayerSpawnUnknownPacketPreservesLegacyZeroPayloadShape()
    {
        MapPlayerSpawnUnknownPacketCodec codec = new();

        SubPacket packet = codec.Encode(0x10001, new MapPlayerSpawnUnknownPacket());

        Assert.Equal(PacketOpcode.MapPlayerSpawnUnknown0x000F, packet.Header.Opcode);
        Assert.Equal(0x10001u, packet.Header.SourceActorId);
        Assert.Equal(MapPlayerSpawnUnknownPacketCodec.PayloadSize, packet.Payload.Length);
        Assert.All(packet.Payload.ToArray(), value => Assert.Equal(0, value));
        codec.Decode(packet);
    }

    [Fact]
    public void PlayAnimationPacketPreservesFixedLegacyLayout()
    {
        PlayAnimationOnActorPacketCodec codec = new();

        SubPacket packet = codec.Encode(0x10001, new PlayAnimationOnActorPacket(0x04000FFA));

        Assert.Equal(PacketOpcode.PlayAnimationOnActor, packet.Header.Opcode);
        Assert.Equal(PlayAnimationOnActorPacketCodec.PayloadSize, packet.Payload.Length);
        Assert.Equal(0x04000FFAu, PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span));
        Assert.Equal(0u, PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span[4..]));
        Assert.Equal(0x04000FFAu, codec.Decode(packet).AnimationId);
    }

    [Theory]
    [InlineData(PacketOpcode.PlayerLogout, 0x000E)]
    [InlineData(PacketOpcode.PlayerQuit, 0x0011)]
    public void PlayerSessionTransitionPacketsPreserveFixedZeroLayout(PacketOpcode opcode, ushort expectedOpcode)
    {
        PlayerSessionTransitionPacketCodec codec = new(opcode);

        SubPacket packet = codec.Encode(0x10001, new PlayerSessionTransitionPacket());

        Assert.Equal(expectedOpcode, (ushort)packet.Header.Opcode);
        Assert.Equal(PlayerSessionTransitionPacketCodec.PayloadSize, packet.Payload.Length);
        Assert.All(packet.Payload.ToArray(), value => Assert.Equal(0, value));
        codec.Decode(packet);
    }
}
