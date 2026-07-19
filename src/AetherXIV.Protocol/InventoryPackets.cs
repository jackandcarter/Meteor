using static AetherXIV.Protocol.InventoryPacketValidation;

namespace AetherXIV.Protocol;

public readonly record struct InventoryBeginChangePacket(bool ClearItemPackage);

public sealed class InventoryBeginChangePacketCodec : IPacketCodec<InventoryBeginChangePacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.InventoryBeginChange;

    public Type PacketType => typeof(InventoryBeginChangePacket);

    public InventoryBeginChangePacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet);
        EnsurePayload(packet, PayloadSize);
        return new InventoryBeginChangePacket(packet.Payload.Span[0] == 2);
    }

    public SubPacket Encode(uint sourceActorId, InventoryBeginChangePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        payload[0] = packet.ClearItemPackage ? (byte)2 : (byte)0;
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }

    private void EnsureOpcode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));
    }
}

public readonly record struct InventoryEndChangePacket;

public sealed class InventoryEndChangePacketCodec : IPacketCodec<InventoryEndChangePacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.InventoryEndChange;

    public Type PacketType => typeof(InventoryEndChangePacket);

    public InventoryEndChangePacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        EnsurePayload(packet, PayloadSize);
        return new InventoryEndChangePacket();
    }

    public SubPacket Encode(uint sourceActorId, InventoryEndChangePacket packet)
    {
        return SubPacket.Create(Opcode, sourceActorId, new byte[PayloadSize]);
    }
}

public readonly record struct InventorySetBeginPacket(ushort Capacity, ushort PackageCode);

public sealed class InventorySetBeginPacketCodec : IPacketCodec<InventorySetBeginPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.InventorySetBegin;

    public Type PacketType => typeof(InventorySetBeginPacket);

    public InventorySetBeginPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new InventorySetBeginPacket(
            PacketBinary.ReadUInt16LittleEndian(payload[4..]),
            PacketBinary.ReadUInt16LittleEndian(payload[6..]));
    }

    public SubPacket Encode(uint sourceActorId, InventorySetBeginPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, sourceActorId);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(4), packet.Capacity);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(6), packet.PackageCode);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct InventorySetEndPacket;

public sealed class InventorySetEndPacketCodec : IPacketCodec<InventorySetEndPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.InventorySetEnd;

    public Type PacketType => typeof(InventorySetEndPacket);

    public InventorySetEndPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        EnsurePayload(packet, PayloadSize);
        return new InventorySetEndPacket();
    }

    public SubPacket Encode(uint sourceActorId, InventorySetEndPacket packet)
    {
        return SubPacket.Create(Opcode, sourceActorId, new byte[PayloadSize]);
    }
}

public sealed record InventoryItemEntry(
    ulong UniqueId,
    int Quantity,
    uint ItemId,
    ushort Slot,
    byte Quality = 1);

public sealed record InventoryListPacket(IReadOnlyList<InventoryItemEntry> Items);

public abstract class InventoryListPacketCodec : IPacketCodec<InventoryListPacket>
{
    public const int ItemEntrySize = 0x70;

    private readonly PacketOpcode opcode;
    private readonly int itemCapacity;
    private readonly int payloadSize;
    private readonly int? countOffset;

    protected InventoryListPacketCodec(PacketOpcode opcode, int itemCapacity, int payloadSize, int? countOffset = null)
    {
        this.opcode = opcode;
        this.itemCapacity = itemCapacity;
        this.payloadSize = payloadSize;
        this.countOffset = countOffset;
    }

    public PacketOpcode Opcode => opcode;

    public Type PacketType => typeof(InventoryListPacket);

    public InventoryListPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = EnsurePayload(packet, payloadSize);
        int count = countOffset.HasValue
            ? checked((int)PacketBinary.ReadUInt32LittleEndian(payload[countOffset.Value..]))
            : itemCapacity;
        count = Math.Clamp(count, 0, itemCapacity);

        List<InventoryItemEntry> items = [];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> entry = payload.Slice(index * ItemEntrySize, ItemEntrySize);
            ulong uniqueId = PacketBinary.ReadUInt64LittleEndian(entry);
            if (uniqueId == 0)
                continue;

            items.Add(new InventoryItemEntry(
                uniqueId,
                PacketBinary.ReadInt32LittleEndian(entry[8..]),
                PacketBinary.ReadUInt32LittleEndian(entry[12..]),
                PacketBinary.ReadUInt16LittleEndian(entry[16..]),
                entry[40]));
        }

        return new InventoryListPacket(items);
    }

    public SubPacket Encode(uint sourceActorId, InventoryListPacket packet)
    {
        if (packet.Items.Count > itemCapacity)
            throw new InvalidDataException($"{Opcode} can carry {itemCapacity} inventory entries; received {packet.Items.Count}.");

        byte[] payload = new byte[payloadSize];
        for (int index = 0; index < packet.Items.Count; index++)
            WriteEntry(payload.AsSpan(index * ItemEntrySize, ItemEntrySize), packet.Items[index]);

        if (countOffset.HasValue)
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(countOffset.Value), checked((uint)packet.Items.Count));

        return SubPacket.Create(Opcode, sourceActorId, payload);
    }

    private static void WriteEntry(Span<byte> payload, InventoryItemEntry item)
    {
        PacketBinary.WriteUInt64LittleEndian(payload, item.UniqueId);
        PacketBinary.WriteInt32LittleEndian(payload[8..], item.Quantity);
        PacketBinary.WriteUInt32LittleEndian(payload[12..], item.ItemId);
        PacketBinary.WriteUInt16LittleEndian(payload[16..], item.Slot);
        payload[40] = item.Quality;
    }
}

public sealed class InventoryListX01PacketCodec : InventoryListPacketCodec
{
    public InventoryListX01PacketCodec()
        : base(PacketOpcode.InventoryListX01, 1, ItemEntrySize)
    {
    }
}

public sealed class InventoryListX08PacketCodec : InventoryListPacketCodec
{
    public InventoryListX08PacketCodec()
        : base(PacketOpcode.InventoryListX08, 8, 0x3A8 - 0x20, countOffset: 0x380)
    {
    }
}

public sealed class InventoryListX16PacketCodec : InventoryListPacketCodec
{
    public InventoryListX16PacketCodec()
        : base(PacketOpcode.InventoryListX16, 16, 0x720 - 0x20)
    {
    }
}

public sealed class InventoryListX32PacketCodec : InventoryListPacketCodec
{
    public InventoryListX32PacketCodec()
        : base(PacketOpcode.InventoryListX32, 32, 0xE20 - 0x20)
    {
    }
}

public sealed class InventoryListX64PacketCodec : InventoryListPacketCodec
{
    public InventoryListX64PacketCodec()
        : base(PacketOpcode.InventoryListX64, 64, 0x1C20 - 0x20)
    {
    }
}

public sealed record LinkedItemEntry(
    ushort LinkedSlot,
    ushort ItemSlot,
    ushort ItemPackageCode);

public sealed record LinkedItemListPacket(IReadOnlyList<LinkedItemEntry> Items);

public abstract class LinkedItemListPacketCodec : IPacketCodec<LinkedItemListPacket>
{
    public const int LinkedItemEntrySize = 6;

    private readonly PacketOpcode opcode;
    private readonly int itemCapacity;
    private readonly int payloadSize;
    private readonly int? countOffset;

    protected LinkedItemListPacketCodec(PacketOpcode opcode, int itemCapacity, int payloadSize, int? countOffset = null)
    {
        this.opcode = opcode;
        this.itemCapacity = itemCapacity;
        this.payloadSize = payloadSize;
        this.countOffset = countOffset;
    }

    public PacketOpcode Opcode => opcode;

    public Type PacketType => typeof(LinkedItemListPacket);

    public LinkedItemListPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = EnsurePayload(packet, payloadSize);
        int count = countOffset.HasValue
            ? checked((int)PacketBinary.ReadUInt32LittleEndian(payload[countOffset.Value..]))
            : itemCapacity;
        count = Math.Clamp(count, 0, itemCapacity);

        List<LinkedItemEntry> items = [];
        for (int index = 0; index < count; index++)
        {
            ReadOnlySpan<byte> entry = payload.Slice(index * LinkedItemEntrySize, LinkedItemEntrySize);
            items.Add(new LinkedItemEntry(
                PacketBinary.ReadUInt16LittleEndian(entry),
                PacketBinary.ReadUInt16LittleEndian(entry[2..]),
                PacketBinary.ReadUInt16LittleEndian(entry[4..])));
        }

        return new LinkedItemListPacket(items);
    }

    public SubPacket Encode(uint sourceActorId, LinkedItemListPacket packet)
    {
        if (packet.Items.Count > itemCapacity)
            throw new InvalidDataException($"{Opcode} can carry {itemCapacity} linked inventory entries; received {packet.Items.Count}.");

        if (!countOffset.HasValue && packet.Items.Count != itemCapacity)
            throw new InvalidDataException($"{Opcode} requires exactly {itemCapacity} linked inventory entries; received {packet.Items.Count}.");

        byte[] payload = new byte[payloadSize];
        for (int index = 0; index < packet.Items.Count; index++)
            WriteEntry(payload.AsSpan(index * LinkedItemEntrySize, LinkedItemEntrySize), packet.Items[index]);

        if (countOffset.HasValue)
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(countOffset.Value), checked((uint)packet.Items.Count));

        return SubPacket.Create(Opcode, sourceActorId, payload);
    }

    private static void WriteEntry(Span<byte> payload, LinkedItemEntry item)
    {
        PacketBinary.WriteUInt16LittleEndian(payload, item.LinkedSlot);
        PacketBinary.WriteUInt16LittleEndian(payload[2..], item.ItemSlot);
        PacketBinary.WriteUInt16LittleEndian(payload[4..], item.ItemPackageCode);
    }
}

public sealed class LinkedItemListX01PacketCodec : LinkedItemListPacketCodec
{
    public const int ItemCapacity = 1;

    public LinkedItemListX01PacketCodec()
        : base(PacketOpcode.LinkedItemListX01, ItemCapacity, 0x28 - 0x20)
    {
    }
}

public sealed class LinkedItemListX08PacketCodec : LinkedItemListPacketCodec
{
    public const int ItemCapacity = 8;

    public LinkedItemListX08PacketCodec()
        : base(PacketOpcode.LinkedItemListX08, ItemCapacity, 0x58 - 0x20, countOffset: 0x30)
    {
    }
}

public sealed class LinkedItemListX16PacketCodec : LinkedItemListPacketCodec
{
    public const int ItemCapacity = 16;

    public LinkedItemListX16PacketCodec()
        : base(PacketOpcode.LinkedItemListX16, ItemCapacity, 0x80 - 0x20)
    {
    }
}

public sealed class LinkedItemListX32PacketCodec : LinkedItemListPacketCodec
{
    public const int ItemCapacity = 32;

    public LinkedItemListX32PacketCodec()
        : base(PacketOpcode.LinkedItemListX32, ItemCapacity, 0xE0 - 0x20)
    {
    }
}

public sealed class LinkedItemListX64PacketCodec : LinkedItemListPacketCodec
{
    public const int ItemCapacity = (0x194 - 0x20) / LinkedItemEntrySize;

    public LinkedItemListX64PacketCodec()
        : base(PacketOpcode.LinkedItemListX64, ItemCapacity, 0x194 - 0x20)
    {
    }
}

internal static class InventoryPacketValidation
{
    public static ReadOnlySpan<byte> EnsurePayload(SubPacket packet, int expected)
    {
        if (packet.Payload.Length < expected)
            throw new InvalidDataException($"Inventory payload requires {expected} bytes; received {packet.Payload.Length}.");

        return packet.Payload.Span[..expected];
    }
}
