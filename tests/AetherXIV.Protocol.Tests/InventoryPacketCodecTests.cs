using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class InventoryPacketCodecTests
{
    [Fact]
    public void InventoryBeginAndEndChangePacketsMatchLegacyShape()
    {
        InventoryBeginChangePacketCodec beginCodec = new();
        InventoryEndChangePacketCodec endCodec = new();

        SubPacket begin = beginCodec.Encode(0x10001, new InventoryBeginChangePacket(ClearItemPackage: true));
        SubPacket end = endCodec.Encode(0x10001, new InventoryEndChangePacket());

        Assert.Equal(PacketOpcode.InventoryBeginChange, begin.Header.Opcode);
        Assert.Equal(0x10001u, begin.Header.SourceActorId);
        Assert.Equal([2, 0, 0, 0, 0, 0, 0, 0], begin.Payload.ToArray());
        Assert.True(beginCodec.Decode(begin).ClearItemPackage);
        Assert.Equal(PacketOpcode.InventoryEndChange, end.Header.Opcode);
        Assert.Equal(new byte[8], end.Payload.ToArray());
    }

    [Fact]
    public void InventorySetBeginPacketMatchesLegacyPackageHeaderShape()
    {
        InventorySetBeginPacketCodec codec = new();

        SubPacket packet = codec.Encode(0x10001, new InventorySetBeginPacket(Capacity: 200, PackageCode: 0));

        Assert.Equal(PacketOpcode.InventorySetBegin, packet.Header.Opcode);
        Assert.Equal(new byte[] { 0x01, 0x00, 0x01, 0x00, 0xC8, 0x00, 0x00, 0x00 }, packet.Payload.ToArray());
        Assert.Equal(new InventorySetBeginPacket(200, 0), codec.Decode(packet));
    }

    [Fact]
    public void QuestGilRewardUsesTheObservedRetailCurrencyTransaction()
    {
        const uint playerActorId = 0x029B2941;
        SubPacket[] packets =
        [
            new InventoryBeginChangePacketCodec().Encode(
                playerActorId,
                new InventoryBeginChangePacket(ClearItemPackage: false)),
            new InventorySetBeginPacketCodec().Encode(
                playerActorId,
                new InventorySetBeginPacket(Capacity: 320, PackageCode: 99)),
            new InventoryListX01PacketCodec().Encode(
                playerActorId,
                new InventoryListPacket(
                    [new InventoryItemEntry(1, 3000, 1000001, Slot: 0, Quality: 1)])),
            new InventorySetEndPacketCodec().Encode(playerActorId, new InventorySetEndPacket()),
            new InventoryEndChangePacketCodec().Encode(playerActorId, new InventoryEndChangePacket())
        ];

        Assert.Equal(
            [
                PacketOpcode.InventoryBeginChange,
                PacketOpcode.InventorySetBegin,
                PacketOpcode.InventoryListX01,
                PacketOpcode.InventorySetEnd,
                PacketOpcode.InventoryEndChange
            ],
            packets.Select(packet => packet.Header.Opcode).ToArray());
        Assert.Equal(
            new InventorySetBeginPacket(Capacity: 320, PackageCode: 99),
            new InventorySetBeginPacketCodec().Decode(packets[1]));
        InventoryItemEntry gil = Assert.Single(
            new InventoryListX01PacketCodec().Decode(packets[2]).Items);
        Assert.Equal(1000001u, gil.ItemId);
        Assert.Equal(3000, gil.Quantity);
    }

    [Fact]
    public void InventoryListX01PacketWritesLegacyItemEntry()
    {
        InventoryListX01PacketCodec codec = new();

        SubPacket packet = codec.Encode(
            0x10001,
            new InventoryListPacket([new InventoryItemEntry(1002, 3, 7000001, Slot: 5, Quality: 1)]));

        Assert.Equal(PacketOpcode.InventoryListX01, packet.Header.Opcode);
        Assert.Equal(InventoryListPacketCodec.ItemEntrySize, packet.Payload.Length);
        Assert.Equal(1002ul, PacketBinary.ReadUInt64LittleEndian(packet.Payload.Span));
        Assert.Equal(3, PacketBinary.ReadInt32LittleEndian(packet.Payload.Span[8..]));
        Assert.Equal(7000001u, PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span[12..]));
        Assert.Equal(5, PacketBinary.ReadUInt16LittleEndian(packet.Payload.Span[16..]));
        Assert.Equal(1, packet.Payload.Span[40]);
        InventoryItemEntry item = Assert.Single(codec.Decode(packet).Items);
        Assert.Equal(new InventoryItemEntry(1002, 3, 7000001, 5, 1), item);
    }

    [Fact]
    public void InventoryListX08PacketWritesLegacyCountFooter()
    {
        InventoryListX08PacketCodec codec = new();
        InventoryItemEntry[] items = Enumerable.Range(0, 3)
            .Select(index => new InventoryItemEntry((ulong)(1000 + index), 1, (uint)(7000000 + index), (ushort)index))
            .ToArray();

        SubPacket packet = codec.Encode(0x10001, new InventoryListPacket(items));

        Assert.Equal(PacketOpcode.InventoryListX08, packet.Header.Opcode);
        Assert.Equal(3u, PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span[0x380..]));
        Assert.Equal(items, codec.Decode(packet).Items);
    }

    [Fact]
    public void LinkedItemListX01PacketWritesLegacyEquipmentReference()
    {
        LinkedItemListX01PacketCodec codec = new();

        SubPacket packet = codec.Encode(
            0x10001,
            new LinkedItemListPacket([new LinkedItemEntry(0, 5, 0)]));

        Assert.Equal(PacketOpcode.LinkedItemListX01, packet.Header.Opcode);
        Assert.Equal(0x28 - 0x20, packet.Payload.Length);
        Assert.Equal(new byte[] { 0, 0, 5, 0, 0, 0, 0, 0 }, packet.Payload.ToArray());
        Assert.Equal([new LinkedItemEntry(0, 5, 0)], codec.Decode(packet).Items);
    }

    [Fact]
    public void LinkedItemListX08PacketWritesLegacyCountFooter()
    {
        LinkedItemListX08PacketCodec codec = new();
        LinkedItemEntry[] items =
        [
            new(0, 5, 0),
            new(3, 7, 0),
            new(5, 9, 7)
        ];

        SubPacket packet = codec.Encode(0x10001, new LinkedItemListPacket(items));

        Assert.Equal(PacketOpcode.LinkedItemListX08, packet.Header.Opcode);
        Assert.Equal(0x58 - 0x20, packet.Payload.Length);
        Assert.Equal(3u, PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span[0x30..]));
        Assert.Equal(items, codec.Decode(packet).Items);
    }
}
