using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class LobbyPacketCodecTests
{
    [Fact]
    public void SessionAcknowledgementPacketDecodesLegacyOffsets()
    {
        LobbySessionAcknowledgementPacketCodec codec = new();
        LobbySessionAcknowledgementPacket packet = new(0x1122334455667788, "session-token", "2012.09.19.0000");

        SubPacket encoded = codec.Encode(0, packet);
        LobbySessionAcknowledgementPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.LobbySessionAcknowledgement, encoded.Header.Opcode);
        Assert.Equal(LobbySessionAcknowledgementPacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal((byte)'s', encoded.Payload.Span[0x10]);
        Assert.Equal((byte)'2', encoded.Payload.Span[0x50]);
        Assert.Equal(packet, decoded);
    }

    [Fact]
    public void GetCharactersPacketCarriesSequenceOnly()
    {
        LobbyGetCharactersPacketCodec codec = new();
        LobbyGetCharactersPacket packet = new(0x1020304050607080);

        SubPacket encoded = codec.Encode(0, packet);
        LobbyGetCharactersPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.LobbyGetCharacters, encoded.Header.Opcode);
        Assert.Equal(8, encoded.Payload.Length);
        Assert.Equal(packet, decoded);
    }

    [Fact]
    public void SelectCharacterPacketRoundTripsLegacyLayout()
    {
        LobbySelectCharacterPacketCodec codec = new();
        LobbySelectCharacterPacket packet = new(7, 42, 9, 0xAABBCCDDEEFF0011);

        SubPacket encoded = codec.Encode(0, packet);
        LobbySelectCharacterPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.LobbySelectCharacter, encoded.Header.Opcode);
        Assert.Equal(LobbySelectCharacterPacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(packet, decoded);
    }

    [Fact]
    public void ModifyCharacterPacketUsesLegacyReserveAndCreationPayloadOffsets()
    {
        LobbyModifyCharacterPacketCodec codec = new();
        LobbyModifyCharacterPacket packet = new(
            0x1020304050607080,
            42,
            1,
            3,
            LobbyCharacterModifyCommands.Reserve,
            7,
            "Ian One",
            "creation-payload");

        SubPacket encoded = codec.Encode(0, packet);
        LobbyModifyCharacterPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.LobbyModifyCharacter, encoded.Header.Opcode);
        Assert.Equal(LobbyModifyCharacterPacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(42u, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[0x08..]));
        Assert.Equal(3, encoded.Payload.Span[0x10]);
        Assert.Equal(LobbyCharacterModifyCommands.Reserve, encoded.Payload.Span[0x11]);
        Assert.Equal(7, PacketBinary.ReadUInt16LittleEndian(encoded.Payload.Span[0x12..]));
        Assert.Equal((byte)'I', encoded.Payload.Span[0x14]);
        Assert.Equal((byte)'c', encoded.Payload.Span[0x34]);
        Assert.Equal(packet, decoded);
    }

    [Fact]
    public void CharacterCreationResultPacketUsesLegacyCreatorResponseLayout()
    {
        LobbyCharacterCreationResultPacketCodec codec = new();
        LobbyCharacterCreationResultPacket packet = new(
            7,
            LobbyCharacterModifyCommands.Make,
            1,
            77,
            LobbyCharacterCreationResultPacketCodec.LegacyPlayerActorType,
            1,
            "Ian One",
            "Aether");

        SubPacket encoded = codec.Encode(0, packet);
        LobbyCharacterCreationResultPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.LobbyCharacterCreationResult, encoded.Header.Opcode);
        Assert.Equal(LobbyCharacterCreationResultPacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(1, encoded.Payload.Span[0x08]);
        Assert.Equal(1, encoded.Payload.Span[0x09]);
        Assert.Equal((ushort)LobbyCharacterModifyCommands.Make, PacketBinary.ReadUInt16LittleEndian(encoded.Payload.Span[0x0A..]));
        Assert.Equal(1u, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[0x10..]));
        Assert.Equal(77u, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[0x14..]));
        Assert.Equal(0x00400017u, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[0x18..]));
        Assert.Equal((byte)'I', encoded.Payload.Span[0x20]);
        Assert.Equal((byte)'A', encoded.Payload.Span[0x40]);
        Assert.Equal(packet, decoded);
    }

    [Fact]
    public void WorldListPacketUsesLegacyHeaderAndEntryOffsets()
    {
        LobbyWorldListPacketCodec codec = new();
        LobbyWorldListPacket packet = new(
            99,
            1,
            [new LobbyWorldListEntry(1, 0, 12, "Aether")]);

        SubPacket encoded = codec.Encode(0, packet);
        LobbyWorldListPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.LobbyWorldList, encoded.Header.Opcode);
        Assert.Equal(LobbyPacketConstants.ServerActorId, encoded.Header.SourceActorId);
        Assert.Equal(LobbyWorldListPacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(1, encoded.Payload.Span[0x08]);
        Assert.Equal(1u, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[0x09..]));
        Assert.Equal(1, PacketBinary.ReadUInt16LittleEndian(encoded.Payload.Span[0x10..]));
        Assert.Equal("Aether", decoded.Worlds.Single().Name);
    }

    [Fact]
    public void AccountListPacketUsesLegacyVariablePayloadSizes()
    {
        LobbyAccountListPacketCodec codec = new();
        LobbyAccountListPacket packet = new(
            1,
            1,
            [new LobbyAccountListEntry(1, "FINAL FANTASY XIV")]);

        SubPacket encoded = codec.Encode(0, packet);
        LobbyAccountListPacket decoded = codec.Decode(encoded);
        SubPacket empty = codec.Encode(0, new LobbyAccountListPacket(1, 1, []));

        Assert.Equal(PacketOpcode.LobbyAccountList, encoded.Header.Opcode);
        Assert.Equal(LobbyAccountListPacketCodec.NonEmptyPayloadSize, encoded.Payload.Length);
        Assert.Equal(LobbyAccountListPacketCodec.EmptyPayloadSize, empty.Payload.Length);
        Assert.Equal(1u, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[0x10..]));
        Assert.Equal((byte)'F', encoded.Payload.Span[0x18]);
        Assert.Equal(packet.Sequence, decoded.Sequence);
        Assert.Equal(packet.ListTracker, decoded.ListTracker);
        Assert.Equal(packet.Accounts.Single(), decoded.Accounts.Single());
    }

    [Fact]
    public void ImportListPacketUsesLegacyEntryOffsets()
    {
        LobbyImportListPacketCodec codec = new();
        LobbyImportListPacket packet = new(
            0,
            1,
            [new LobbyImportListEntry(0, 3, "Tester Last")]);

        SubPacket encoded = codec.Encode(0, packet);
        LobbyImportListPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.LobbyImportList, encoded.Header.Opcode);
        Assert.Equal(LobbyImportListPacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(3u, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[0x14..]));
        Assert.Equal((byte)'T', encoded.Payload.Span[0x18]);
        Assert.Equal(packet.Sequence, decoded.Sequence);
        Assert.Equal(packet.ListTracker, decoded.ListTracker);
        Assert.Equal(packet.Imports.Single(), decoded.Imports.Single());
    }

    [Fact]
    public void RetainerListPacketUsesLegacyHeaderAndEntryOffsets()
    {
        LobbyRetainerListPacketCodec codec = new();
        LobbyRetainerListPacket packet = new(
            0,
            1,
            [new LobbyRetainerListEntry(100, 42, 2, 0x04, "Helper")]);

        SubPacket encoded = codec.Encode(0, packet);
        LobbyRetainerListPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.LobbyRetainerList, encoded.Header.Opcode);
        Assert.Equal(LobbyRetainerListPacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(100u, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[0x1C..]));
        Assert.Equal(42u, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[0x20..]));
        Assert.Equal(2, PacketBinary.ReadUInt16LittleEndian(encoded.Payload.Span[0x24..]));
        Assert.Equal(0x04, PacketBinary.ReadUInt16LittleEndian(encoded.Payload.Span[0x26..]));
        Assert.Equal(packet.Sequence, decoded.Sequence);
        Assert.Equal(packet.ListTracker, decoded.ListTracker);
        Assert.Equal(packet.Retainers.Single(), decoded.Retainers.Single());
    }

    [Fact]
    public void CharacterListPacketUsesTraceConfirmedEntryOffsetsAndPreservesAppearancePayload()
    {
        byte[] appearance = Enumerable.Range(0, LobbyCharacterListPacketCodec.AppearanceSize)
            .Select(value => (byte)(value % 251))
            .ToArray();
        LobbyCharacterListPacketCodec codec = new();
        LobbyCharacterListPacket packet = new(
            99,
            1,
            [new LobbyCharacterListEntry(42, 3, 0x08, 209, "Tester", "Aether", appearance)]);

        SubPacket encoded = codec.Encode(0, packet);
        LobbyCharacterListPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.LobbyCharacterList, encoded.Header.Opcode);
        Assert.Equal(LobbyCharacterListPacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(42u, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[0x14..]));
        Assert.Equal(3, encoded.Payload.Span[0x18]);
        Assert.Equal(0x08, encoded.Payload.Span[0x19]);
        Assert.Equal(209u, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[0x1C..]));
        Assert.Equal((byte)'A', encoded.Payload.Span[0x40]);
        Assert.Equal(appearance[0], encoded.Payload.Span[0x50]);
        Assert.Equal("Tester", decoded.Characters.Single().Name);
        Assert.Equal("Aether", decoded.Characters.Single().WorldName);
        Assert.Equal(appearance, decoded.Characters.Single().Appearance.ToArray());
    }

    [Fact]
    public void CharacterListPacketTerminatesTruncatedWorldNameBeforeAppearancePayload()
    {
        byte[] appearance = Enumerable.Range(0, LobbyCharacterListPacketCodec.AppearanceSize)
            .Select(value => (byte)(0x80 + (value % 0x40)))
            .ToArray();
        LobbyCharacterListPacketCodec codec = new();
        LobbyCharacterListPacket packet = new(
            99,
            1,
            [new LobbyCharacterListEntry(42, 3, 0, 209, "Tester", "AetherXIV 2.0 Local", appearance)]);

        SubPacket encoded = codec.Encode(0, packet);
        LobbyCharacterListEntry decoded = codec.Decode(encoded).Characters.Single();

        Assert.Equal("AetherXIV 2.0 L", decoded.WorldName);
        Assert.Equal(0, encoded.Payload.Span[0x4F]);
        Assert.Equal(appearance[0], encoded.Payload.Span[0x50]);
        Assert.Equal(appearance, decoded.Appearance.ToArray());
    }

    [Fact]
    public void SelectCharacterConfirmPacketUsesLegacyConnectionFields()
    {
        LobbySelectCharacterConfirmPacketCodec codec = new();
        LobbySelectCharacterConfirmPacket packet = new(
            77,
            42,
            42,
            "session-token",
            1989,
            "127.0.0.1",
            123456);

        SubPacket encoded = codec.Encode(0, packet);
        LobbySelectCharacterConfirmPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.LobbySelectCharacterConfirm, encoded.Header.Opcode);
        Assert.Equal(1989, PacketBinary.ReadUInt16LittleEndian(encoded.Payload.Span[0x56..]));
        Assert.Equal((byte)'1', encoded.Payload.Span[0x58]);
        Assert.Equal(packet, decoded);
    }

    [Fact]
    public void ErrorPacketCarriesTextIdAndMessage()
    {
        LobbyErrorPacketCodec codec = new();
        LobbyErrorPacket packet = new(7, 0, 0, 13001, "Your session has expired.");

        SubPacket encoded = codec.Encode(0, packet);
        LobbyErrorPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.LobbyError, encoded.Header.Opcode);
        Assert.Equal(13001u, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[0x10..]));
        Assert.Equal(packet, decoded);
    }
}
