using System.Text;

namespace AetherXIV.Protocol;

public static class LobbyPacketConstants
{
    public const uint ServerActorId = 0xE0006868;
}

public readonly record struct LobbySessionAcknowledgementPacket(
    ulong Sequence,
    string SessionToken,
    string ClientVersion);

public sealed class LobbySessionAcknowledgementPacketCodec : IPacketCodec<LobbySessionAcknowledgementPacket>
{
    public const int PayloadSize = 0x78;

    public PacketOpcode Opcode => PacketOpcode.LobbySessionAcknowledgement;

    public Type PacketType => typeof(LobbySessionAcknowledgementPacket);

    public LobbySessionAcknowledgementPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new LobbySessionAcknowledgementPacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            LobbyPacketStrings.ReadFixedAscii(payload[0x10..], 0x40),
            LobbyPacketStrings.ReadFixedAscii(payload[0x50..], 0x20));
    }

    public SubPacket Encode(uint sourceActorId, LobbySessionAcknowledgementPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Sequence);
        LobbyPacketStrings.WriteFixedAscii(payload.AsSpan(0x10), 0x40, packet.SessionToken);
        LobbyPacketStrings.WriteFixedAscii(payload.AsSpan(0x50), 0x20, packet.ClientVersion);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }

    private static ReadOnlySpan<byte> EnsurePayload(SubPacket packet, int requiredLength)
    {
        return LobbyPacketCodecHelpers.EnsurePayload(packet, requiredLength);
    }

    private static void EnsureOpcode(SubPacket packet, PacketOpcode opcode)
    {
        LobbyPacketCodecHelpers.EnsureOpcode(packet, opcode);
    }
}

public readonly record struct LobbyGetCharactersPacket(ulong Sequence);

public sealed class LobbyGetCharactersPacketCodec : IPacketCodec<LobbyGetCharactersPacket>
{
    public const int PayloadSize = 0x08;

    public PacketOpcode Opcode => PacketOpcode.LobbyGetCharacters;

    public Type PacketType => typeof(LobbyGetCharactersPacket);

    public LobbyGetCharactersPacket Decode(SubPacket packet)
    {
        LobbyPacketCodecHelpers.EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = LobbyPacketCodecHelpers.EnsurePayload(packet, PayloadSize);
        return new LobbyGetCharactersPacket(PacketBinary.ReadUInt64LittleEndian(payload));
    }

    public SubPacket Encode(uint sourceActorId, LobbyGetCharactersPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Sequence);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct LobbySelectCharacterPacket(
    ulong Sequence,
    uint CharacterId,
    uint UnknownId,
    ulong Ticket);

public sealed class LobbySelectCharacterPacketCodec : IPacketCodec<LobbySelectCharacterPacket>
{
    public const int PayloadSize = 0x18;

    public PacketOpcode Opcode => PacketOpcode.LobbySelectCharacter;

    public Type PacketType => typeof(LobbySelectCharacterPacket);

    public LobbySelectCharacterPacket Decode(SubPacket packet)
    {
        LobbyPacketCodecHelpers.EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = LobbyPacketCodecHelpers.EnsurePayload(packet, PayloadSize);
        return new LobbySelectCharacterPacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[0x08..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x0C..]),
            PacketBinary.ReadUInt64LittleEndian(payload[0x10..]));
    }

    public SubPacket Encode(uint sourceActorId, LobbySelectCharacterPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Sequence);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x08), packet.CharacterId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x0C), packet.UnknownId);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x10), packet.Ticket);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public static class LobbyCharacterModifyCommands
{
    public const byte Reserve = 0x01;
    public const byte Make = 0x02;
    public const byte Rename = 0x03;
    public const byte Delete = 0x04;
    public const byte RenameRetainer = 0x06;
}

public readonly record struct LobbyModifyCharacterPacket(
    ulong Sequence,
    uint CharacterId,
    uint PersonType,
    byte Slot,
    byte Command,
    ushort WorldId,
    string CharacterName,
    string CharacterInfoEncoded);

public sealed class LobbyModifyCharacterPacketCodec : IPacketCodec<LobbyModifyCharacterPacket>
{
    public const int PayloadSize = 0x1C4;
    public const int CharacterNameSize = 0x20;
    public const int CharacterInfoSize = 0x190;

    public PacketOpcode Opcode => PacketOpcode.LobbyModifyCharacter;

    public Type PacketType => typeof(LobbyModifyCharacterPacket);

    public LobbyModifyCharacterPacket Decode(SubPacket packet)
    {
        LobbyPacketCodecHelpers.EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = LobbyPacketCodecHelpers.EnsurePayload(packet, PayloadSize);
        return new LobbyModifyCharacterPacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[0x08..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x0C..]),
            payload[0x10],
            payload[0x11],
            PacketBinary.ReadUInt16LittleEndian(payload[0x12..]),
            LobbyPacketStrings.ReadFixedAscii(payload[0x14..], CharacterNameSize),
            LobbyPacketStrings.ReadFixedAscii(payload[0x34..], CharacterInfoSize));
    }

    public SubPacket Encode(uint sourceActorId, LobbyModifyCharacterPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Sequence);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x08), packet.CharacterId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x0C), packet.PersonType);
        payload[0x10] = packet.Slot;
        payload[0x11] = packet.Command;
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x12), packet.WorldId);
        LobbyPacketStrings.WriteFixedAscii(payload.AsSpan(0x14), CharacterNameSize, packet.CharacterName);
        LobbyPacketStrings.WriteFixedAscii(payload.AsSpan(0x34), CharacterInfoSize, packet.CharacterInfoEncoded);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct LobbyCharacterCreationResultPacket(
    ulong Sequence,
    ushort Command,
    uint PlayerId,
    uint CharacterId,
    uint ActorType,
    uint Ticket,
    string CharacterName,
    string WorldName);

public sealed class LobbyCharacterCreationResultPacketCodec : IPacketCodec<LobbyCharacterCreationResultPacket>
{
    public const int PayloadSize = 0x1F0;
    public const int CharacterNameSize = 0x20;
    public const int WorldNameSize = 0x20;
    public const uint LegacyPlayerActorType = 0x00400017;

    public PacketOpcode Opcode => PacketOpcode.LobbyCharacterCreationResult;

    public Type PacketType => typeof(LobbyCharacterCreationResultPacket);

    public LobbyCharacterCreationResultPacket Decode(SubPacket packet)
    {
        LobbyPacketCodecHelpers.EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = LobbyPacketCodecHelpers.EnsurePayload(packet, PayloadSize);
        return new LobbyCharacterCreationResultPacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            PacketBinary.ReadUInt16LittleEndian(payload[0x0A..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x10..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x14..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x18..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x1C..]),
            LobbyPacketStrings.ReadFixedAscii(payload[0x20..], CharacterNameSize),
            LobbyPacketStrings.ReadFixedAscii(payload[0x40..], WorldNameSize));
    }

    public SubPacket Encode(uint sourceActorId, LobbyCharacterCreationResultPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Sequence);
        payload[0x08] = 1;
        payload[0x09] = 1;
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x0A), packet.Command);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x0C), 0);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x10), packet.PlayerId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x14), packet.CharacterId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x18), packet.ActorType);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x1C), packet.Ticket);
        LobbyPacketStrings.WriteFixedAscii(payload.AsSpan(0x20), CharacterNameSize, packet.CharacterName);
        LobbyPacketStrings.WriteFixedAscii(payload.AsSpan(0x40), WorldNameSize, packet.WorldName);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct LobbyWorldListEntry(
    ushort WorldId,
    ushort ListPosition,
    uint Population,
    string Name);

public readonly record struct LobbyAccountListEntry(uint AccountId, string Name);

public readonly record struct LobbyAccountListPacket(
    ulong Sequence,
    byte ListTracker,
    IReadOnlyList<LobbyAccountListEntry> Accounts);

public sealed class LobbyAccountListPacketCodec : IPacketCodec<LobbyAccountListPacket>
{
    public const int EmptyPayloadSize = 0x210;
    public const int NonEmptyPayloadSize = 0x280;
    public const int MaxEntries = 8;
    public const int HeaderSize = 0x10;
    public const int EntrySize = 0x48;

    public PacketOpcode Opcode => PacketOpcode.LobbyAccountList;

    public Type PacketType => typeof(LobbyAccountListPacket);

    public LobbyAccountListPacket Decode(SubPacket packet)
    {
        LobbyPacketCodecHelpers.EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = LobbyPacketCodecHelpers.EnsurePayload(packet, HeaderSize);
        uint count = PacketBinary.ReadUInt32LittleEndian(payload[0x09..]);
        if (count > MaxEntries)
            throw new InvalidDataException($"Lobby account list count {count} exceeds {MaxEntries}.");

        LobbyPacketCodecHelpers.EnsurePayload(packet, HeaderSize + ((int)count * EntrySize));
        LobbyAccountListEntry[] entries = new LobbyAccountListEntry[count];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> entry = payload.Slice(HeaderSize + (EntrySize * index), EntrySize);
            entries[index] = new LobbyAccountListEntry(
                PacketBinary.ReadUInt32LittleEndian(entry),
                LobbyPacketStrings.ReadFixedAscii(entry[0x08..], 0x40));
        }

        return new LobbyAccountListPacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            payload[0x08],
            entries);
    }

    public SubPacket Encode(uint sourceActorId, LobbyAccountListPacket packet)
    {
        if (packet.Accounts.Count > MaxEntries)
            throw new ArgumentOutOfRangeException(nameof(packet), $"Lobby account list packets support at most {MaxEntries} entries.");

        byte[] payload = new byte[packet.Accounts.Count == 0 ? EmptyPayloadSize : NonEmptyPayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Sequence);
        payload[0x08] = packet.ListTracker;
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x09), (uint)packet.Accounts.Count);

        for (int index = 0; index < packet.Accounts.Count; index++)
        {
            LobbyAccountListEntry account = packet.Accounts[index];
            Span<byte> entry = payload.AsSpan(HeaderSize + (EntrySize * index), EntrySize);
            PacketBinary.WriteUInt32LittleEndian(entry, account.AccountId);
            PacketBinary.WriteUInt32LittleEndian(entry[0x04..], 0);
            LobbyPacketStrings.WriteFixedAscii(entry[0x08..], 0x40, account.Name);
        }

        return SubPacket.Create(Opcode, LobbyPacketConstants.ServerActorId, payload);
    }
}

public readonly record struct LobbyWorldListPacket(
    ulong Sequence,
    byte ListTracker,
    IReadOnlyList<LobbyWorldListEntry> Worlds);

public sealed class LobbyWorldListPacketCodec : IPacketCodec<LobbyWorldListPacket>
{
    public const int PayloadSize = 0x210;
    public const int MaxEntries = 6;
    public const int HeaderSize = 0x10;
    public const int EntrySize = 0x50;

    public PacketOpcode Opcode => PacketOpcode.LobbyWorldList;

    public Type PacketType => typeof(LobbyWorldListPacket);

    public LobbyWorldListPacket Decode(SubPacket packet)
    {
        LobbyPacketCodecHelpers.EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = LobbyPacketCodecHelpers.EnsurePayload(packet, PayloadSize);
        uint count = PacketBinary.ReadUInt32LittleEndian(payload[0x09..]);
        if (count > MaxEntries)
            throw new InvalidDataException($"Lobby world list count {count} exceeds {MaxEntries}.");

        LobbyWorldListEntry[] entries = new LobbyWorldListEntry[count];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> entry = payload.Slice(HeaderSize + (EntrySize * index), EntrySize);
            entries[index] = new LobbyWorldListEntry(
                PacketBinary.ReadUInt16LittleEndian(entry),
                PacketBinary.ReadUInt16LittleEndian(entry[0x02..]),
                PacketBinary.ReadUInt32LittleEndian(entry[0x04..]),
                LobbyPacketStrings.ReadFixedAscii(entry[0x10..], 0x40));
        }

        return new LobbyWorldListPacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            payload[0x08],
            entries);
    }

    public SubPacket Encode(uint sourceActorId, LobbyWorldListPacket packet)
    {
        if (packet.Worlds.Count > MaxEntries)
            throw new ArgumentOutOfRangeException(nameof(packet), $"Lobby world list packets support at most {MaxEntries} entries.");

        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Sequence);
        payload[0x08] = packet.ListTracker;
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x09), (uint)packet.Worlds.Count);

        for (int index = 0; index < packet.Worlds.Count; index++)
        {
            LobbyWorldListEntry world = packet.Worlds[index];
            Span<byte> entry = payload.AsSpan(HeaderSize + (EntrySize * index), EntrySize);
            PacketBinary.WriteUInt16LittleEndian(entry, world.WorldId);
            PacketBinary.WriteUInt16LittleEndian(entry[0x02..], world.ListPosition);
            PacketBinary.WriteUInt32LittleEndian(entry[0x04..], world.Population);
            LobbyPacketStrings.WriteFixedAscii(entry[0x10..], 0x40, world.Name);
        }

        return SubPacket.Create(Opcode, LobbyPacketConstants.ServerActorId, payload);
    }
}

public readonly record struct LobbyImportListEntry(uint Unknown, uint ListPosition, string Name);

public readonly record struct LobbyImportListPacket(
    ulong Sequence,
    byte ListTracker,
    IReadOnlyList<LobbyImportListEntry> Imports);

public sealed class LobbyImportListPacketCodec : IPacketCodec<LobbyImportListPacket>
{
    public const int PayloadSize = 0x210;
    public const int MaxEntries = 12;
    public const int HeaderSize = 0x10;
    public const int EntrySize = 0x28;

    public PacketOpcode Opcode => PacketOpcode.LobbyImportList;

    public Type PacketType => typeof(LobbyImportListPacket);

    public LobbyImportListPacket Decode(SubPacket packet)
    {
        LobbyPacketCodecHelpers.EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = LobbyPacketCodecHelpers.EnsurePayload(packet, PayloadSize);
        uint count = PacketBinary.ReadUInt32LittleEndian(payload[0x09..]);
        if (count > MaxEntries)
            throw new InvalidDataException($"Lobby import list count {count} exceeds {MaxEntries}.");

        LobbyImportListEntry[] entries = new LobbyImportListEntry[count];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> entry = payload.Slice(HeaderSize + (EntrySize * index), EntrySize);
            entries[index] = new LobbyImportListEntry(
                PacketBinary.ReadUInt32LittleEndian(entry),
                PacketBinary.ReadUInt32LittleEndian(entry[0x04..]),
                LobbyPacketStrings.ReadFixedAscii(entry[0x08..], 0x20));
        }

        return new LobbyImportListPacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            payload[0x08],
            entries);
    }

    public SubPacket Encode(uint sourceActorId, LobbyImportListPacket packet)
    {
        if (packet.Imports.Count > MaxEntries)
            throw new ArgumentOutOfRangeException(nameof(packet), $"Lobby import list packets support at most {MaxEntries} entries.");

        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Sequence);
        payload[0x08] = packet.ListTracker;
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x09), (uint)packet.Imports.Count);

        for (int index = 0; index < packet.Imports.Count; index++)
        {
            LobbyImportListEntry import = packet.Imports[index];
            Span<byte> entry = payload.AsSpan(HeaderSize + (EntrySize * index), EntrySize);
            PacketBinary.WriteUInt32LittleEndian(entry, import.Unknown);
            PacketBinary.WriteUInt32LittleEndian(entry[0x04..], import.ListPosition);
            LobbyPacketStrings.WriteFixedAscii(entry[0x08..], 0x20, import.Name);
        }

        return SubPacket.Create(Opcode, LobbyPacketConstants.ServerActorId, payload);
    }
}

public readonly record struct LobbyRetainerListEntry(
    uint RetainerId,
    uint CharacterId,
    ushort Slot,
    ushort Options,
    string Name);

public readonly record struct LobbyRetainerListPacket(
    ulong Sequence,
    byte ListTracker,
    IReadOnlyList<LobbyRetainerListEntry> Retainers);

public sealed class LobbyRetainerListPacketCodec : IPacketCodec<LobbyRetainerListPacket>
{
    public const int PayloadSize = 0x210;
    public const int MaxEntries = 9;
    public const int HeaderSize = 0x1C;
    public const int EntrySize = 0x30;

    public PacketOpcode Opcode => PacketOpcode.LobbyRetainerList;

    public Type PacketType => typeof(LobbyRetainerListPacket);

    public LobbyRetainerListPacket Decode(SubPacket packet)
    {
        LobbyPacketCodecHelpers.EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = LobbyPacketCodecHelpers.EnsurePayload(packet, PayloadSize);
        uint count = PacketBinary.ReadUInt32LittleEndian(payload[0x09..]);
        if (count > MaxEntries)
            throw new InvalidDataException($"Lobby retainer list count {count} exceeds {MaxEntries}.");

        LobbyRetainerListEntry[] entries = new LobbyRetainerListEntry[count];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> entry = payload.Slice(HeaderSize + (EntrySize * index), EntrySize);
            entries[index] = new LobbyRetainerListEntry(
                PacketBinary.ReadUInt32LittleEndian(entry),
                PacketBinary.ReadUInt32LittleEndian(entry[0x04..]),
                PacketBinary.ReadUInt16LittleEndian(entry[0x08..]),
                PacketBinary.ReadUInt16LittleEndian(entry[0x0A..]),
                LobbyPacketStrings.ReadFixedAscii(entry[0x10..], 0x20));
        }

        return new LobbyRetainerListPacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            payload[0x08],
            entries);
    }

    public SubPacket Encode(uint sourceActorId, LobbyRetainerListPacket packet)
    {
        if (packet.Retainers.Count > MaxEntries)
            throw new ArgumentOutOfRangeException(nameof(packet), $"Lobby retainer list packets support at most {MaxEntries} entries.");

        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Sequence);
        payload[0x08] = packet.ListTracker;
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x09), (uint)packet.Retainers.Count);
        if (packet.Retainers.Count > 0)
        {
            PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x10), 0);
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x18), 0);
        }

        for (int index = 0; index < packet.Retainers.Count; index++)
        {
            LobbyRetainerListEntry retainer = packet.Retainers[index];
            Span<byte> entry = payload.AsSpan(HeaderSize + (EntrySize * index), EntrySize);
            PacketBinary.WriteUInt32LittleEndian(entry, retainer.RetainerId);
            PacketBinary.WriteUInt32LittleEndian(entry[0x04..], retainer.CharacterId);
            PacketBinary.WriteUInt16LittleEndian(entry[0x08..], retainer.Slot);
            PacketBinary.WriteUInt16LittleEndian(entry[0x0A..], retainer.Options);
            PacketBinary.WriteUInt32LittleEndian(entry[0x0C..], 0);
            LobbyPacketStrings.WriteFixedAscii(entry[0x10..], 0x20, retainer.Name);
        }

        return SubPacket.Create(Opcode, LobbyPacketConstants.ServerActorId, payload);
    }
}

public readonly record struct LobbyCharacterListEntry(
    uint CharacterId,
    byte Slot,
    byte Options,
    uint CurrentZoneId,
    string Name,
    string WorldName,
    ReadOnlyMemory<byte> Appearance);

public readonly record struct LobbyCharacterListPacket(
    ulong Sequence,
    byte ListTracker,
    IReadOnlyList<LobbyCharacterListEntry> Characters);

public sealed class LobbyCharacterListPacketCodec : IPacketCodec<LobbyCharacterListPacket>
{
    public const int PayloadSize = 0x3B0;
    public const int MaxEntries = 2;
    public const int HeaderSize = 0x10;
    public const int EntrySize = 0x1D0;
    public const int WorldNameSize = 0x10;
    public const int AppearanceOffset = 0x40;
    public const int AppearanceSize = EntrySize - AppearanceOffset;

    public PacketOpcode Opcode => PacketOpcode.LobbyCharacterList;

    public Type PacketType => typeof(LobbyCharacterListPacket);

    public LobbyCharacterListPacket Decode(SubPacket packet)
    {
        LobbyPacketCodecHelpers.EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = LobbyPacketCodecHelpers.EnsurePayload(packet, PayloadSize);
        uint count = PacketBinary.ReadUInt32LittleEndian(payload[0x09..]);
        if (count > MaxEntries)
            throw new InvalidDataException($"Lobby character list count {count} exceeds {MaxEntries}.");

        LobbyCharacterListEntry[] entries = new LobbyCharacterListEntry[count];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> entry = payload.Slice(HeaderSize + (EntrySize * index), EntrySize);
            entries[index] = new LobbyCharacterListEntry(
                PacketBinary.ReadUInt32LittleEndian(entry[0x04..]),
                entry[0x08],
                entry[0x09],
                PacketBinary.ReadUInt32LittleEndian(entry[0x0C..]),
                LobbyPacketStrings.ReadFixedAscii(entry[0x10..], 0x20),
                LobbyPacketStrings.ReadFixedAscii(entry[0x30..], WorldNameSize),
                entry[AppearanceOffset..].ToArray());
        }

        return new LobbyCharacterListPacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            payload[0x08],
            entries);
    }

    public SubPacket Encode(uint sourceActorId, LobbyCharacterListPacket packet)
    {
        if (packet.Characters.Count > MaxEntries)
            throw new ArgumentOutOfRangeException(nameof(packet), $"Lobby character list packets support at most {MaxEntries} entries.");

        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Sequence);
        payload[0x08] = packet.ListTracker;
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x09), (uint)packet.Characters.Count);

        for (int index = 0; index < packet.Characters.Count; index++)
        {
            LobbyCharacterListEntry character = packet.Characters[index];
            if (character.Appearance.Length > AppearanceSize)
                throw new ArgumentOutOfRangeException(nameof(packet), $"Lobby character appearance payload exceeds {AppearanceSize} bytes.");

            Span<byte> entry = payload.AsSpan(HeaderSize + (EntrySize * index), EntrySize);
            PacketBinary.WriteUInt32LittleEndian(entry, 0);
            PacketBinary.WriteUInt32LittleEndian(entry[0x04..], character.CharacterId);
            entry[0x08] = character.Slot;
            entry[0x09] = character.Options;
            PacketBinary.WriteUInt16LittleEndian(entry[0x0A..], 0);
            PacketBinary.WriteUInt32LittleEndian(entry[0x0C..], character.CurrentZoneId);
            LobbyPacketStrings.WriteFixedAscii(entry[0x10..], 0x20, character.Name);
            LobbyPacketStrings.WriteFixedAscii(entry[0x30..], WorldNameSize, character.WorldName);
            character.Appearance.Span.CopyTo(entry[AppearanceOffset..]);
        }

        return SubPacket.Create(Opcode, LobbyPacketConstants.ServerActorId, payload);
    }
}

public readonly record struct LobbySelectCharacterConfirmPacket(
    ulong Sequence,
    uint ActorId,
    uint CharacterId,
    string SessionToken,
    ushort WorldPort,
    string WorldHost,
    ulong Ticket);

public sealed class LobbySelectCharacterConfirmPacketCodec : IPacketCodec<LobbySelectCharacterConfirmPacket>
{
    public const int PayloadSize = 0x98;

    public PacketOpcode Opcode => PacketOpcode.LobbySelectCharacterConfirm;

    public Type PacketType => typeof(LobbySelectCharacterConfirmPacket);

    public LobbySelectCharacterConfirmPacket Decode(SubPacket packet)
    {
        LobbyPacketCodecHelpers.EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = LobbyPacketCodecHelpers.EnsurePayload(packet, PayloadSize);
        return new LobbySelectCharacterConfirmPacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[0x08..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x0C..]),
            LobbyPacketStrings.ReadFixedAscii(payload[0x14..], 0x42),
            PacketBinary.ReadUInt16LittleEndian(payload[0x56..]),
            LobbyPacketStrings.ReadFixedAscii(payload[0x58..], 0x38),
            PacketBinary.ReadUInt64LittleEndian(payload[0x90..]));
    }

    public SubPacket Encode(uint sourceActorId, LobbySelectCharacterConfirmPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Sequence);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x08), packet.ActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x0C), packet.CharacterId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x10), 0);
        LobbyPacketStrings.WriteFixedAscii(payload.AsSpan(0x14), 0x42, packet.SessionToken);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x56), packet.WorldPort);
        LobbyPacketStrings.WriteFixedAscii(payload.AsSpan(0x58), 0x38, packet.WorldHost);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x90), packet.Ticket);
        return SubPacket.Create(Opcode, LobbyPacketConstants.ServerActorId, payload);
    }
}

public readonly record struct LobbyErrorPacket(
    ulong Sequence,
    uint ErrorCode,
    uint StatusCode,
    uint TextId,
    string Message);

public sealed class LobbyErrorPacketCodec : IPacketCodec<LobbyErrorPacket>
{
    public const int PayloadSize = 0x210;

    public PacketOpcode Opcode => PacketOpcode.LobbyError;

    public Type PacketType => typeof(LobbyErrorPacket);

    public LobbyErrorPacket Decode(SubPacket packet)
    {
        LobbyPacketCodecHelpers.EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = LobbyPacketCodecHelpers.EnsurePayload(packet, PayloadSize);
        return new LobbyErrorPacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[0x08..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x0C..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x10..]),
            LobbyPacketStrings.ReadFixedAscii(payload[0x14..], PayloadSize - 0x14));
    }

    public SubPacket Encode(uint sourceActorId, LobbyErrorPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Sequence);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x08), packet.ErrorCode);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x0C), packet.StatusCode);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x10), packet.TextId);
        LobbyPacketStrings.WriteFixedAscii(payload.AsSpan(0x14), PayloadSize - 0x14, packet.Message);
        return SubPacket.Create(Opcode, LobbyPacketConstants.ServerActorId, payload);
    }
}

internal static class LobbyPacketCodecHelpers
{
    public static void EnsureOpcode(SubPacket packet, PacketOpcode opcode)
    {
        if (packet.Header.Opcode != opcode)
            throw new ArgumentException($"Expected opcode {opcode} but received {packet.Header.Opcode}.", nameof(packet));
    }

    public static ReadOnlySpan<byte> EnsurePayload(SubPacket packet, int requiredLength)
    {
        if (packet.Payload.Length < requiredLength)
            throw new InvalidDataException($"Lobby packet payload ended before {requiredLength} bytes.");

        return packet.Payload.Span;
    }
}

internal static class LobbyPacketStrings
{
    public static string ReadFixedAscii(ReadOnlySpan<byte> payload, int length)
    {
        if (payload.Length < length)
            throw new InvalidDataException("Lobby fixed string payload ended unexpectedly.");

        ReadOnlySpan<byte> value = payload[..length];
        int terminator = value.IndexOf((byte)0);
        if (terminator >= 0)
            value = value[..terminator];

        return Encoding.ASCII.GetString(value).TrimEnd();
    }

    public static void WriteFixedAscii(Span<byte> payload, int length, string value)
    {
        if (payload.Length < length)
            throw new InvalidDataException("Lobby fixed string target ended unexpectedly.");

        payload[..length].Clear();
        byte[] bytes = Encoding.ASCII.GetBytes(value);
        int copyLength = Math.Min(bytes.Length, Math.Max(0, length - 1));
        bytes.AsSpan(0, copyLength).CopyTo(payload[..length]);
    }
}
