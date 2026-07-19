namespace AetherXIV.Protocol;

public readonly record struct ClientSocialStateRequestPacket(uint PageIndex, uint RequestToken);

public sealed class ClientSocialStateRequestPacketCodec : IPacketCodec<ClientSocialStateRequestPacket>
{
    public const int PayloadSize = 0x08;

    public ClientSocialStateRequestPacketCodec(PacketOpcode opcode)
    {
        Opcode = opcode;
    }

    public PacketOpcode Opcode { get; }

    public Type PacketType => typeof(ClientSocialStateRequestPacket);

    public ClientSocialStateRequestPacket Decode(SubPacket packet)
    {
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new ClientSocialStateRequestPacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[0x04..]));
    }

    public SubPacket Encode(uint sourceActorId, ClientSocialStateRequestPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.PageIndex);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x04), packet.RequestToken);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }

    private ReadOnlySpan<byte> EnsurePayload(SubPacket packet, int expectedLength)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected opcode 0x{(ushort)Opcode:X4} but received 0x{(ushort)packet.Header.Opcode:X4}.");
        if (packet.Payload.Length != expectedLength)
            throw new InvalidDataException($"Opcode 0x{(ushort)Opcode:X4} requires {expectedLength} payload bytes.");
        return packet.Payload.Span;
    }
}

public readonly record struct BlacklistStatePacket(uint PageIndex, IReadOnlyList<string> Names);

public sealed class BlacklistStatePacketCodec : IPacketCodec<BlacklistStatePacket>
{
    public const int PayloadSize = 0x288;
    public const int MaximumEntries = 20;
    public const int NameSize = 0x20;

    public PacketOpcode Opcode => PacketOpcode.BlacklistState;

    public Type PacketType => typeof(BlacklistStatePacket);

    public BlacklistStatePacket Decode(SubPacket packet)
    {
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        uint pageIndex = PacketBinary.ReadUInt32LittleEndian(payload);
        uint count = PacketBinary.ReadUInt32LittleEndian(payload[0x04..]);
        if (count > MaximumEntries)
            throw new InvalidDataException($"Blacklist state contains impossible entry count {count}.");

        List<string> names = [];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> name = payload.Slice(0x08 + (index * NameSize), NameSize);
            int end = name.IndexOf((byte)0);
            names.Add(System.Text.Encoding.ASCII.GetString(end < 0 ? name : name[..end]));
        }
        return new BlacklistStatePacket(pageIndex, names);
    }

    public SubPacket Encode(uint sourceActorId, BlacklistStatePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet.Names);
        if (packet.Names.Count > MaximumEntries)
            throw new ArgumentOutOfRangeException(nameof(packet), $"Blacklist state supports at most {MaximumEntries} entries.");

        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.PageIndex);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x04), checked((uint)packet.Names.Count));
        for (int index = 0; index < packet.Names.Count; index++)
            WriteFixedAscii(payload.AsSpan(0x08 + (index * NameSize), NameSize), packet.Names[index]);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }

    internal static ReadOnlySpan<byte> EnsurePayload(SubPacket packet, int expectedLength)
    {
        if (packet.Payload.Length != expectedLength)
            throw new InvalidDataException($"Opcode 0x{(ushort)packet.Header.Opcode:X4} requires {expectedLength} payload bytes.");
        return packet.Payload.Span;
    }

    internal static void WriteFixedAscii(Span<byte> destination, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        int length = System.Text.Encoding.ASCII.GetByteCount(value);
        if (length > destination.Length)
            throw new ArgumentOutOfRangeException(nameof(value), $"ASCII value exceeds {destination.Length} bytes.");
        System.Text.Encoding.ASCII.GetBytes(value, destination);
    }
}

public readonly record struct FriendListEntry(ulong CharacterId, string Name);

public readonly record struct FriendListStatePacket(uint PageIndex, IReadOnlyList<FriendListEntry> Entries);

public sealed class FriendListStatePacketCodec : IPacketCodec<FriendListStatePacket>
{
    public const int PayloadSize = 0x328;
    public const int MaximumEntries = 20;
    public const int EntrySize = 0x28;
    public const int NameSize = 0x20;

    public PacketOpcode Opcode => PacketOpcode.FriendListState;

    public Type PacketType => typeof(FriendListStatePacket);

    public FriendListStatePacket Decode(SubPacket packet)
    {
        ReadOnlySpan<byte> payload = BlacklistStatePacketCodec.EnsurePayload(packet, PayloadSize);
        uint pageIndex = PacketBinary.ReadUInt32LittleEndian(payload);
        uint count = PacketBinary.ReadUInt32LittleEndian(payload[0x04..]);
        if (count > MaximumEntries)
            throw new InvalidDataException($"Friend-list state contains impossible entry count {count}.");

        List<FriendListEntry> entries = [];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> entry = payload.Slice(0x08 + (index * EntrySize), EntrySize);
            ReadOnlySpan<byte> name = entry[..NameSize];
            int end = name.IndexOf((byte)0);
            entries.Add(new FriendListEntry(
                PacketBinary.ReadUInt64LittleEndian(entry[NameSize..]),
                System.Text.Encoding.ASCII.GetString(end < 0 ? name : name[..end])));
        }
        return new FriendListStatePacket(pageIndex, entries);
    }

    public SubPacket Encode(uint sourceActorId, FriendListStatePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet.Entries);
        if (packet.Entries.Count > MaximumEntries)
            throw new ArgumentOutOfRangeException(nameof(packet), $"Friend-list state supports at most {MaximumEntries} entries.");

        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.PageIndex);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x04), checked((uint)packet.Entries.Count));
        for (int index = 0; index < packet.Entries.Count; index++)
        {
            Span<byte> entry = payload.AsSpan(0x08 + (index * EntrySize), EntrySize);
            BlacklistStatePacketCodec.WriteFixedAscii(entry[..NameSize], packet.Entries[index].Name);
            PacketBinary.WriteUInt64LittleEndian(entry[NameSize..], packet.Entries[index].CharacterId);
        }
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct FriendStatusEntry(ulong CharacterId, bool IsOnline);

public readonly record struct FriendStatusPacket(uint PageIndex, IReadOnlyList<FriendStatusEntry> Entries);

public sealed class FriendStatusPacketCodec : IPacketCodec<FriendStatusPacket>
{
    public const int PayloadSize = 0x648;
    public const int MaximumEntries = 100;
    public const int EntrySize = 0x10;

    public PacketOpcode Opcode => PacketOpcode.FriendStatus;

    public Type PacketType => typeof(FriendStatusPacket);

    public FriendStatusPacket Decode(SubPacket packet)
    {
        ReadOnlySpan<byte> payload = BlacklistStatePacketCodec.EnsurePayload(packet, PayloadSize);
        uint pageIndex = PacketBinary.ReadUInt32LittleEndian(payload);
        uint count = PacketBinary.ReadUInt32LittleEndian(payload[0x04..]);
        if (count > MaximumEntries)
            throw new InvalidDataException($"Friend status contains impossible entry count {count}.");

        List<FriendStatusEntry> entries = [];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> entry = payload.Slice(0x08 + (index * EntrySize), EntrySize);
            entries.Add(new FriendStatusEntry(
                PacketBinary.ReadUInt64LittleEndian(entry),
                PacketBinary.ReadUInt64LittleEndian(entry[0x08..]) != 0));
        }

        return new FriendStatusPacket(pageIndex, entries);
    }

    public SubPacket Encode(uint sourceActorId, FriendStatusPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet.Entries);
        if (packet.Entries.Count > MaximumEntries)
            throw new ArgumentOutOfRangeException(nameof(packet), $"Friend status supports at most {MaximumEntries} entries.");

        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.PageIndex);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x04), checked((uint)packet.Entries.Count));
        for (int index = 0; index < packet.Entries.Count; index++)
        {
            Span<byte> entry = payload.AsSpan(0x08 + (index * EntrySize), EntrySize);
            PacketBinary.WriteUInt64LittleEndian(entry, packet.Entries[index].CharacterId);
            PacketBinary.WriteUInt64LittleEndian(entry[0x08..], packet.Entries[index].IsOnline ? 1ul : 0ul);
        }

        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct GmTicketStatePacket(bool HasOpenTicket);

public sealed class GmTicketStatePacketCodec : IPacketCodec<GmTicketStatePacket>
{
    public const int PayloadSize = 0x08;

    public PacketOpcode Opcode => PacketOpcode.GmTicketState;

    public Type PacketType => typeof(GmTicketStatePacket);

    public GmTicketStatePacket Decode(SubPacket packet)
    {
        ReadOnlySpan<byte> payload = BlacklistStatePacketCodec.EnsurePayload(packet, PayloadSize);
        return new GmTicketStatePacket(payload[0] != 0);
    }

    public SubPacket Encode(uint sourceActorId, GmTicketStatePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        payload[0] = packet.HasOpenTicket ? (byte)1 : (byte)0;
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}
