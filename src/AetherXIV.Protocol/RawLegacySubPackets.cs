namespace AetherXIV.Protocol;

public readonly record struct WireLegacySubPacket(
    ushort Type,
    uint SourceActorId,
    uint TargetActorId,
    uint HeaderUnknown,
    PacketOpcode? Opcode,
    uint GameUnknown5,
    uint GameTimestamp,
    uint GameUnknown6,
    ReadOnlyMemory<byte> Payload)
{
    public bool IsGameMessage => Type == RawLegacySubPacketCodec.GameMessageSubPacketType;

    public static WireLegacySubPacket FromGame(
        SubPacket packet,
        uint targetActorId,
        uint gameTimestamp = 0,
        uint headerUnknown = 0,
        uint gameUnknown5 = 0,
        uint gameUnknown6 = 0)
    {
        return new WireLegacySubPacket(
            RawLegacySubPacketCodec.GameMessageSubPacketType,
            packet.Header.SourceActorId,
            targetActorId,
            headerUnknown,
            packet.Header.Opcode,
            gameUnknown5,
            gameTimestamp,
            gameUnknown6,
            packet.Payload);
    }

    public static WireLegacySubPacket FromControl(
        ushort type,
        uint sourceActorId,
        ReadOnlyMemory<byte> payload,
        uint targetActorId = 0,
        uint headerUnknown = 0)
    {
        if (type == RawLegacySubPacketCodec.GameMessageSubPacketType)
            throw new ArgumentException("Use FromGame for legacy type-3 game-message subpackets.", nameof(type));

        return new WireLegacySubPacket(
            type,
            sourceActorId,
            targetActorId,
            headerUnknown,
            null,
            0,
            0,
            0,
            payload);
    }

    public SubPacket ToSubPacket()
    {
        if (!IsGameMessage || Opcode is null)
            throw new InvalidOperationException($"Legacy subpacket type 0x{Type:X4} is not a game-message subpacket.");

        return SubPacket.Create(Opcode.Value, SourceActorId, Payload);
    }
}

public sealed class RawLegacySubPacketCodec
{
    public const int HeaderSize = 0x10;
    public const int GameMessageHeaderSize = 0x10;
    public const ushort GameMessageSubPacketType = 0x0003;
    public const ushort GameMessageHeaderMarker = 0x0014;

    public byte[] Encode(WireLegacySubPacket packet)
    {
        int size = HeaderSize + packet.Payload.Length + (packet.IsGameMessage ? GameMessageHeaderSize : 0);
        if (size > UInt16.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(packet), "Legacy subpacket exceeds UInt16 packet size.");

        byte[] buffer = new byte[size];
        PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(0x00), checked((ushort)size));
        PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(0x02), packet.Type);
        PacketBinary.WriteUInt32LittleEndian(buffer.AsSpan(0x04), packet.SourceActorId);
        PacketBinary.WriteUInt32LittleEndian(buffer.AsSpan(0x08), packet.TargetActorId);
        PacketBinary.WriteUInt32LittleEndian(buffer.AsSpan(0x0C), packet.HeaderUnknown);

        int payloadOffset = HeaderSize;
        if (packet.IsGameMessage)
        {
            if (packet.Opcode is null)
                throw new InvalidDataException("Legacy game-message subpacket requires an opcode.");

            PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(0x10), GameMessageHeaderMarker);
            PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(0x12), (ushort)packet.Opcode.Value);
            PacketBinary.WriteUInt32LittleEndian(buffer.AsSpan(0x14), packet.GameUnknown5);
            PacketBinary.WriteUInt32LittleEndian(buffer.AsSpan(0x18), packet.GameTimestamp);
            PacketBinary.WriteUInt32LittleEndian(buffer.AsSpan(0x1C), packet.GameUnknown6);
            payloadOffset += GameMessageHeaderSize;
        }

        packet.Payload.Span.CopyTo(buffer.AsSpan(payloadOffset));
        return buffer;
    }

    public WireLegacySubPacket Decode(ReadOnlySpan<byte> buffer)
    {
        if (!TryDecode(buffer, out WireLegacySubPacket packet, out int consumed))
            throw new InvalidDataException("Legacy subpacket buffer did not contain one complete subpacket.");

        if (consumed != buffer.Length)
            throw new InvalidDataException($"Legacy subpacket decode consumed {consumed} bytes from a {buffer.Length}-byte buffer.");

        return packet;
    }

    public bool TryDecode(ReadOnlySpan<byte> buffer, out WireLegacySubPacket packet, out int consumed)
    {
        packet = default;
        consumed = 0;

        if (buffer.Length < HeaderSize)
            return false;

        ushort size = PacketBinary.ReadUInt16LittleEndian(buffer);
        if (size < HeaderSize)
            throw new InvalidDataException($"Legacy subpacket declared impossible size {size}.");

        if (buffer.Length < size)
            return false;

        ushort type = PacketBinary.ReadUInt16LittleEndian(buffer[0x02..]);
        uint sourceActorId = PacketBinary.ReadUInt32LittleEndian(buffer[0x04..]);
        uint targetActorId = PacketBinary.ReadUInt32LittleEndian(buffer[0x08..]);
        uint headerUnknown = PacketBinary.ReadUInt32LittleEndian(buffer[0x0C..]);
        int payloadOffset = HeaderSize;
        PacketOpcode? opcode = null;
        uint gameUnknown5 = 0;
        uint gameTimestamp = 0;
        uint gameUnknown6 = 0;

        if (type == GameMessageSubPacketType)
        {
            if (size < HeaderSize + GameMessageHeaderSize)
                throw new InvalidDataException("Legacy game-message subpacket ended before its game header.");

            ushort marker = PacketBinary.ReadUInt16LittleEndian(buffer[0x10..]);
            if (marker != GameMessageHeaderMarker)
                throw new InvalidDataException($"Unexpected legacy game-message marker 0x{marker:X4}.");

            opcode = (PacketOpcode)PacketBinary.ReadUInt16LittleEndian(buffer[0x12..]);
            gameUnknown5 = PacketBinary.ReadUInt32LittleEndian(buffer[0x14..]);
            gameTimestamp = PacketBinary.ReadUInt32LittleEndian(buffer[0x18..]);
            gameUnknown6 = PacketBinary.ReadUInt32LittleEndian(buffer[0x1C..]);
            payloadOffset += GameMessageHeaderSize;
        }

        byte[] payload = buffer[payloadOffset..size].ToArray();
        packet = new WireLegacySubPacket(
            type,
            sourceActorId,
            targetActorId,
            headerUnknown,
            opcode,
            gameUnknown5,
            gameTimestamp,
            gameUnknown6,
            payload);
        consumed = size;
        return true;
    }
}
