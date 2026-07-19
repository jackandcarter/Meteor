using System.IO.Compression;

namespace AetherXIV.Protocol;

public readonly record struct BasePacketFrameHeader(
    bool IsAuthenticated,
    bool IsCompressed,
    ushort ConnectionType,
    ushort PacketSize,
    ushort SubPacketCount,
    ulong Timestamp);

public readonly record struct WireGameMessageSubPacket(
    SubPacket Packet,
    uint TargetActorId,
    uint GameTimestamp = 0,
    uint SubPacketUnknown = 0,
    uint GameUnknown5 = 0,
    uint GameUnknown6 = 0);

public readonly record struct BasePacketFrame(
    BasePacketFrameHeader Header,
    IReadOnlyList<WireGameMessageSubPacket> SubPackets);

public readonly record struct LegacyPacketFrame(
    BasePacketFrameHeader Header,
    IReadOnlyList<WireLegacySubPacket> SubPackets);

public sealed class BasePacketFrameCodec
{
    public const int HeaderSize = 0x10;
    public const int SubPacketHeaderSize = 0x10;
    public const int GameMessageHeaderSize = 0x10;
    public const ushort GameMessageSubPacketType = 0x03;
    public const ushort GameMessageHeaderMarker = 0x14;

    public byte[] Encode(
        IReadOnlyList<WireGameMessageSubPacket> subPackets,
        bool isAuthenticated,
        ulong timestamp,
        ushort connectionType = 0,
        bool isCompressed = false)
    {
        byte[] uncompressed = EncodeUncompressed(subPackets, isAuthenticated, timestamp, connectionType, isCompressed: false);
        if (!isCompressed)
            return uncompressed;

        byte[] compressedBody = CompressZLibBody(uncompressed.AsSpan(HeaderSize));
        int packetSize = HeaderSize + compressedBody.Length;
        if (packetSize > UInt16.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(subPackets), "Compressed base packet frame exceeds UInt16 packet size.");

        byte[] compressedFrame = new byte[packetSize];
        compressedFrame[0x00] = isAuthenticated ? (byte)1 : (byte)0;
        compressedFrame[0x01] = 1;
        PacketBinary.WriteUInt16LittleEndian(compressedFrame.AsSpan(0x02), connectionType);
        PacketBinary.WriteUInt16LittleEndian(compressedFrame.AsSpan(0x04), checked((ushort)packetSize));
        PacketBinary.WriteUInt16LittleEndian(compressedFrame.AsSpan(0x06), checked((ushort)subPackets.Count));
        PacketBinary.WriteUInt64LittleEndian(compressedFrame.AsSpan(0x08), timestamp);
        compressedBody.CopyTo(compressedFrame.AsSpan(HeaderSize));
        return compressedFrame;
    }

    public byte[] EncodeLegacy(
        IReadOnlyList<WireLegacySubPacket> subPackets,
        bool isAuthenticated,
        ulong timestamp,
        ushort connectionType = 0,
        bool isCompressed = false)
    {
        ArgumentNullException.ThrowIfNull(subPackets);

        RawLegacySubPacketCodec rawCodec = new();
        using MemoryStream body = new();
        foreach (WireLegacySubPacket subPacket in subPackets)
        {
            byte[] encoded = rawCodec.Encode(subPacket);
            body.Write(encoded);
        }

        byte[] uncompressedBody = body.ToArray();
        byte[] outputBody = isCompressed ? CompressZLibBody(uncompressedBody) : uncompressedBody;
        int packetSize = HeaderSize + outputBody.Length;
        if (packetSize > UInt16.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(subPackets), "Legacy base packet frame exceeds UInt16 packet size.");

        byte[] buffer = new byte[packetSize];
        buffer[0x00] = isAuthenticated ? (byte)1 : (byte)0;
        buffer[0x01] = isCompressed ? (byte)1 : (byte)0;
        PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(0x02), connectionType);
        PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(0x04), checked((ushort)packetSize));
        PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(0x06), checked((ushort)subPackets.Count));
        PacketBinary.WriteUInt64LittleEndian(buffer.AsSpan(0x08), timestamp);
        outputBody.CopyTo(buffer.AsSpan(HeaderSize));
        return buffer;
    }

    private byte[] EncodeUncompressed(
        IReadOnlyList<WireGameMessageSubPacket> subPackets,
        bool isAuthenticated,
        ulong timestamp,
        ushort connectionType,
        bool isCompressed)
    {
        int packetSize = HeaderSize;
        foreach (WireGameMessageSubPacket subPacket in subPackets)
            packetSize += EncodedSubPacketSize(subPacket.Packet);

        if (packetSize > UInt16.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(subPackets), "Base packet frame exceeds UInt16 packet size.");

        byte[] buffer = new byte[packetSize];
        buffer[0x00] = isAuthenticated ? (byte)1 : (byte)0;
        buffer[0x01] = isCompressed ? (byte)1 : (byte)0;
        PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(0x02), connectionType);
        PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(0x04), checked((ushort)packetSize));
        PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(0x06), checked((ushort)subPackets.Count));
        PacketBinary.WriteUInt64LittleEndian(buffer.AsSpan(0x08), timestamp);

        int offset = HeaderSize;
        foreach (WireGameMessageSubPacket subPacket in subPackets)
        {
            int subPacketSize = EncodedSubPacketSize(subPacket.Packet);
            PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(offset + 0x00), checked((ushort)subPacketSize));
            PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(offset + 0x02), GameMessageSubPacketType);
            PacketBinary.WriteUInt32LittleEndian(buffer.AsSpan(offset + 0x04), subPacket.Packet.Header.SourceActorId);
            PacketBinary.WriteUInt32LittleEndian(buffer.AsSpan(offset + 0x08), subPacket.TargetActorId);
            PacketBinary.WriteUInt32LittleEndian(buffer.AsSpan(offset + 0x0C), subPacket.SubPacketUnknown);

            int gameOffset = offset + SubPacketHeaderSize;
            PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(gameOffset + 0x00), GameMessageHeaderMarker);
            PacketBinary.WriteUInt16LittleEndian(buffer.AsSpan(gameOffset + 0x02), (ushort)subPacket.Packet.Header.Opcode);
            PacketBinary.WriteUInt32LittleEndian(buffer.AsSpan(gameOffset + 0x04), subPacket.GameUnknown5);
            PacketBinary.WriteUInt32LittleEndian(buffer.AsSpan(gameOffset + 0x08), subPacket.GameTimestamp);
            PacketBinary.WriteUInt32LittleEndian(buffer.AsSpan(gameOffset + 0x0C), subPacket.GameUnknown6);

            subPacket.Packet.Payload.Span.CopyTo(buffer.AsSpan(offset + SubPacketHeaderSize + GameMessageHeaderSize));
            offset += subPacketSize;
        }

        return buffer;
    }

    public BasePacketFrame Decode(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < HeaderSize)
            throw new InvalidDataException("Base packet frame ended before the header.");

        BasePacketFrameHeader header = new(
            buffer[0x00] != 0,
            buffer[0x01] != 0,
            PacketBinary.ReadUInt16LittleEndian(buffer[0x02..]),
            PacketBinary.ReadUInt16LittleEndian(buffer[0x04..]),
            PacketBinary.ReadUInt16LittleEndian(buffer[0x06..]),
            PacketBinary.ReadUInt64LittleEndian(buffer[0x08..]));

        if (buffer.Length < header.PacketSize)
            throw new InvalidDataException("Base packet frame ended before the declared packet size.");

        byte[]? decompressedFrame = null;
        int parsePacketSize = header.PacketSize;
        if (header.IsCompressed)
        {
            ReadOnlySpan<byte> compressedBody = buffer.Slice(HeaderSize, header.PacketSize - HeaderSize);
            byte[] decompressedBody = DecompressZLibBody(compressedBody);
            decompressedFrame = new byte[HeaderSize + decompressedBody.Length];
            buffer[..HeaderSize].CopyTo(decompressedFrame);
            decompressedBody.CopyTo(decompressedFrame.AsSpan(HeaderSize));
            parsePacketSize = decompressedFrame.Length;
        }

        ReadOnlySpan<byte> parseBuffer = decompressedFrame is null
            ? buffer[..header.PacketSize]
            : decompressedFrame;

        List<WireGameMessageSubPacket> subPackets = [];
        int offset = HeaderSize;
        while (offset < parsePacketSize)
        {
            if (offset + SubPacketHeaderSize > parsePacketSize)
                throw new InvalidDataException("Base packet frame ended inside a subpacket header.");

            ushort subPacketSize = PacketBinary.ReadUInt16LittleEndian(parseBuffer[(offset + 0x00)..]);
            ushort subPacketType = PacketBinary.ReadUInt16LittleEndian(parseBuffer[(offset + 0x02)..]);
            uint sourceActorId = PacketBinary.ReadUInt32LittleEndian(parseBuffer[(offset + 0x04)..]);
            uint targetActorId = PacketBinary.ReadUInt32LittleEndian(parseBuffer[(offset + 0x08)..]);
            uint subPacketUnknown = PacketBinary.ReadUInt32LittleEndian(parseBuffer[(offset + 0x0C)..]);

            if (subPacketSize < SubPacketHeaderSize)
                throw new InvalidDataException("Base packet frame contained an invalid subpacket size.");

            if (offset + subPacketSize > parsePacketSize)
                throw new InvalidDataException("Base packet frame ended inside a subpacket payload.");

            if (subPacketType != GameMessageSubPacketType)
                throw new NotSupportedException($"Subpacket type 0x{subPacketType:X4} is not a game-message subpacket.");

            int gameOffset = offset + SubPacketHeaderSize;
            if (subPacketSize < SubPacketHeaderSize + GameMessageHeaderSize)
                throw new InvalidDataException("Game-message subpacket ended before its game header.");

            ushort gameHeaderMarker = PacketBinary.ReadUInt16LittleEndian(parseBuffer[(gameOffset + 0x00)..]);
            if (gameHeaderMarker != GameMessageHeaderMarker)
                throw new InvalidDataException($"Unexpected game-message header marker 0x{gameHeaderMarker:X4}.");

            PacketOpcode opcode = (PacketOpcode)PacketBinary.ReadUInt16LittleEndian(parseBuffer[(gameOffset + 0x02)..]);
            uint gameUnknown5 = PacketBinary.ReadUInt32LittleEndian(parseBuffer[(gameOffset + 0x04)..]);
            uint gameTimestamp = PacketBinary.ReadUInt32LittleEndian(parseBuffer[(gameOffset + 0x08)..]);
            uint gameUnknown6 = PacketBinary.ReadUInt32LittleEndian(parseBuffer[(gameOffset + 0x0C)..]);
            int payloadOffset = gameOffset + GameMessageHeaderSize;
            int payloadLength = subPacketSize - SubPacketHeaderSize - GameMessageHeaderSize;
            byte[] payload = parseBuffer.Slice(payloadOffset, payloadLength).ToArray();
            SubPacket packet = SubPacket.Create(opcode, sourceActorId, payload);
            subPackets.Add(new WireGameMessageSubPacket(
                packet,
                targetActorId,
                gameTimestamp,
                subPacketUnknown,
                gameUnknown5,
                gameUnknown6));

            offset += subPacketSize;
        }

        if (subPackets.Count != header.SubPacketCount)
            throw new InvalidDataException($"Base packet frame declared {header.SubPacketCount} subpackets but contained {subPackets.Count}.");

        return new BasePacketFrame(header, subPackets);
    }

    public LegacyPacketFrame DecodeLegacy(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < HeaderSize)
            throw new InvalidDataException("Base packet frame ended before the header.");

        BasePacketFrameHeader header = new(
            buffer[0x00] != 0,
            buffer[0x01] != 0,
            PacketBinary.ReadUInt16LittleEndian(buffer[0x02..]),
            PacketBinary.ReadUInt16LittleEndian(buffer[0x04..]),
            PacketBinary.ReadUInt16LittleEndian(buffer[0x06..]),
            PacketBinary.ReadUInt64LittleEndian(buffer[0x08..]));

        if (buffer.Length < header.PacketSize)
            throw new InvalidDataException("Base packet frame ended before the declared packet size.");

        byte[]? decompressedFrame = null;
        int parsePacketSize = header.PacketSize;
        if (header.IsCompressed)
        {
            ReadOnlySpan<byte> compressedBody = buffer.Slice(HeaderSize, header.PacketSize - HeaderSize);
            byte[] decompressedBody = DecompressZLibBody(compressedBody);
            decompressedFrame = new byte[HeaderSize + decompressedBody.Length];
            buffer[..HeaderSize].CopyTo(decompressedFrame);
            decompressedBody.CopyTo(decompressedFrame.AsSpan(HeaderSize));
            parsePacketSize = decompressedFrame.Length;
        }

        ReadOnlySpan<byte> parseBuffer = decompressedFrame is null
            ? buffer[..header.PacketSize]
            : decompressedFrame;

        RawLegacySubPacketCodec rawCodec = new();
        List<WireLegacySubPacket> subPackets = [];
        int offset = HeaderSize;
        while (offset < parsePacketSize)
        {
            if (!rawCodec.TryDecode(parseBuffer[offset..parsePacketSize], out WireLegacySubPacket subPacket, out int consumed))
                throw new InvalidDataException("Base packet frame ended inside a legacy subpacket.");

            subPackets.Add(subPacket);
            offset += consumed;
        }

        if (subPackets.Count != header.SubPacketCount)
            throw new InvalidDataException($"Base packet frame declared {header.SubPacketCount} subpackets but contained {subPackets.Count}.");

        return new LegacyPacketFrame(header, subPackets);
    }

    private static int EncodedSubPacketSize(SubPacket packet)
    {
        return SubPacketHeaderSize + GameMessageHeaderSize + packet.Payload.Length;
    }


    private static byte[] CompressZLibBody(ReadOnlySpan<byte> body)
    {
        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            zlib.Write(body);
        }

        return compressed.ToArray();
    }

    private static byte[] DecompressZLibBody(ReadOnlySpan<byte> compressedBody)
    {
        using MemoryStream compressed = new(compressedBody.ToArray());
        using ZLibStream zlib = new(compressed, CompressionMode.Decompress);
        using MemoryStream decompressed = new();
        zlib.CopyTo(decompressed);
        return decompressed.ToArray();
    }
}
