using AetherXIV.Core;
using AetherXIV.Protocol;
using System.Security.Cryptography;

namespace AetherXIV.Server.Hosting;

public sealed class LegacyFrameConnection
{
    private readonly Stream stream;
    private readonly ILegacyStreamCipher cipher;
    private readonly BasePacketFrameCodec codec;
    private readonly IDiagnosticSink diagnostics;
    private readonly int maxFrameSize;
    private readonly string diagnosticPrefix;

    public LegacyFrameConnection(
        Stream stream,
        ILegacyStreamCipher cipher,
        BasePacketFrameCodec? codec = null,
        IDiagnosticSink? diagnostics = null,
        int maxFrameSize = LegacyTcpFrameServerOptions.DefaultMaxFrameSize,
        string diagnosticPrefix = "legacy.frame")
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(cipher);

        if (!stream.CanRead || !stream.CanWrite)
            throw new ArgumentException("Legacy frame connection requires a readable and writable stream.", nameof(stream));

        if (maxFrameSize < BasePacketFrameCodec.HeaderSize)
            throw new ArgumentOutOfRangeException(nameof(maxFrameSize), maxFrameSize, "Max frame size must allow at least the 16-byte legacy frame header.");

        this.stream = stream;
        this.cipher = cipher;
        this.codec = codec ?? new BasePacketFrameCodec();
        this.diagnostics = diagnostics ?? NullDiagnosticSink.Instance;
        this.maxFrameSize = maxFrameSize;
        this.diagnosticPrefix = diagnosticPrefix;
    }

    public async ValueTask<BasePacketFrame?> ReadFrameAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            byte[]? frameBytes = await ReadRawFrameBytesAsync(cancellationToken).ConfigureAwait(false);
            if (frameBytes is null)
                return null;

            if (cipher.TryHandleControlFrame(frameBytes, out byte[]? immediateResponse))
            {
                if (immediateResponse is { Length: > 0 })
                    await WriteRawFrameBytesAsync(immediateResponse, cancellationToken).ConfigureAwait(false);

                diagnostics.Trace($"{diagnosticPrefix}.control", new Dictionary<string, object?>
                {
                    ["cipher"] = cipher.Name,
                    ["packetSize"] = frameBytes.Length,
                    ["immediateResponseBytes"] = immediateResponse?.Length ?? 0
                });

                continue;
            }

            cipher.TransformInboundFrame(frameBytes);
            string plaintextSha256 = Convert.ToHexString(SHA256.HashData(frameBytes)).ToLowerInvariant();

            if (TryGetFirstNonGameSubPacketType(frameBytes, out ushort controlType))
            {
                diagnostics.Trace($"{diagnosticPrefix}.skip-control", new Dictionary<string, object?>
                {
                    ["cipher"] = cipher.Name,
                    ["packetSize"] = frameBytes.Length,
                    ["subPacketType"] = $"0x{controlType:X4}",
                    ["reason"] = "non-game control subpacket is not dispatched to LobbyPacketEntryService"
                });

                continue;
            }

            BasePacketFrame frame = codec.Decode(frameBytes);
            diagnostics.Trace($"{diagnosticPrefix}.read", new Dictionary<string, object?>
            {
                ["cipher"] = cipher.Name,
                ["packetSize"] = frame.Header.PacketSize,
                ["subPackets"] = frame.SubPackets.Count,
                ["compressed"] = frame.Header.IsCompressed,
                ["authenticated"] = frame.Header.IsAuthenticated,
                ["connectionType"] = frame.Header.ConnectionType,
                ["plaintextSha256"] = plaintextSha256,
                ["observedKeys"] = String.Join(",", FormatObservedKeys(PacketDirection.ClientToServer, frame.SubPackets))
            });

            return frame;
        }
    }

    public async ValueTask<LegacyPacketFrame?> ReadLegacyFrameAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            byte[]? frameBytes = await ReadRawFrameBytesAsync(cancellationToken).ConfigureAwait(false);
            if (frameBytes is null)
                return null;

            if (cipher.TryHandleControlFrame(frameBytes, out byte[]? immediateResponse))
            {
                if (immediateResponse is { Length: > 0 })
                    await WriteRawFrameBytesAsync(immediateResponse, cancellationToken).ConfigureAwait(false);

                diagnostics.Trace($"{diagnosticPrefix}.control", new Dictionary<string, object?>
                {
                    ["cipher"] = cipher.Name,
                    ["packetSize"] = frameBytes.Length,
                    ["immediateResponseBytes"] = immediateResponse?.Length ?? 0
                });

                continue;
            }

            cipher.TransformInboundFrame(frameBytes);
            string plaintextSha256 = Convert.ToHexString(SHA256.HashData(frameBytes)).ToLowerInvariant();
            LegacyPacketFrame frame = codec.DecodeLegacy(frameBytes);
            diagnostics.Trace($"{diagnosticPrefix}.readLegacy", new Dictionary<string, object?>
            {
                ["cipher"] = cipher.Name,
                ["packetSize"] = frame.Header.PacketSize,
                ["subPackets"] = frame.SubPackets.Count,
                ["compressed"] = frame.Header.IsCompressed,
                ["authenticated"] = frame.Header.IsAuthenticated,
                ["connectionType"] = frame.Header.ConnectionType,
                ["plaintextSha256"] = plaintextSha256,
                ["observedKeys"] = String.Join(",", FormatObservedLegacyKeys(PacketDirection.ClientToServer, frame.SubPackets))
            });

            return frame;
        }
    }

    public async ValueTask WriteFrameAsync(
        IReadOnlyList<WireGameMessageSubPacket> subPackets,
        bool isAuthenticated,
        ushort connectionType = 0,
        ulong? timestamp = null,
        bool isCompressed = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subPackets);
        if (subPackets.Count == 0)
            return;

        ulong frameTimestamp = timestamp ?? unchecked((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        byte[] frameBytes = codec.Encode(subPackets, isAuthenticated, frameTimestamp, connectionType, isCompressed);
        if (frameBytes.Length > maxFrameSize)
            throw new InvalidDataException($"Encoded legacy frame size {frameBytes.Length} exceeds configured max {maxFrameSize}.");

        string plaintextSha256 = Convert.ToHexString(SHA256.HashData(frameBytes)).ToLowerInvariant();
        cipher.TransformOutboundFrame(frameBytes);
        await WriteRawFrameBytesAsync(frameBytes, cancellationToken).ConfigureAwait(false);

        diagnostics.Trace($"{diagnosticPrefix}.write", new Dictionary<string, object?>
        {
            ["cipher"] = cipher.Name,
            ["packetSize"] = frameBytes.Length,
            ["subPackets"] = subPackets.Count,
            ["compressed"] = isCompressed,
            ["authenticated"] = isAuthenticated,
            ["connectionType"] = connectionType,
            ["plaintextSha256"] = plaintextSha256,
            ["observedKeys"] = String.Join(",", FormatObservedKeys(PacketDirection.ServerToClient, subPackets))
        });
    }

    public async ValueTask WriteLegacyFrameAsync(
        IReadOnlyList<WireLegacySubPacket> subPackets,
        bool isAuthenticated,
        ushort connectionType = 0,
        ulong? timestamp = null,
        bool isCompressed = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subPackets);
        if (subPackets.Count == 0)
            return;

        ulong frameTimestamp = timestamp ?? unchecked((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        byte[] frameBytes = codec.EncodeLegacy(subPackets, isAuthenticated, frameTimestamp, connectionType, isCompressed);
        if (frameBytes.Length > maxFrameSize)
            throw new InvalidDataException($"Encoded legacy frame size {frameBytes.Length} exceeds configured max {maxFrameSize}.");

        string plaintextSha256 = Convert.ToHexString(SHA256.HashData(frameBytes)).ToLowerInvariant();
        cipher.TransformOutboundFrame(frameBytes);
        await WriteRawFrameBytesAsync(frameBytes, cancellationToken).ConfigureAwait(false);

        diagnostics.Trace($"{diagnosticPrefix}.writeLegacy", new Dictionary<string, object?>
        {
            ["cipher"] = cipher.Name,
            ["packetSize"] = frameBytes.Length,
            ["subPackets"] = subPackets.Count,
            ["compressed"] = isCompressed,
            ["authenticated"] = isAuthenticated,
            ["connectionType"] = connectionType,
            ["plaintextSha256"] = plaintextSha256,
            ["observedKeys"] = String.Join(",", FormatObservedLegacyKeys(PacketDirection.ServerToClient, subPackets))
        });
    }

    private async ValueTask<byte[]?> ReadRawFrameBytesAsync(CancellationToken cancellationToken)
    {
        byte[] header = new byte[BasePacketFrameCodec.HeaderSize];
        bool hasHeader = await ReadExactlyOrEndAsync(header, cancellationToken).ConfigureAwait(false);
        if (!hasHeader)
            return null;

        ushort packetSize = PacketBinary.ReadUInt16LittleEndian(header.AsSpan(0x04));
        if (packetSize < BasePacketFrameCodec.HeaderSize)
            throw new InvalidDataException($"Legacy frame declared impossible size {packetSize}.");

        if (packetSize > maxFrameSize)
            throw new InvalidDataException($"Legacy frame declared size {packetSize}, above configured max {maxFrameSize}.");

        byte[] frameBytes = new byte[packetSize];
        header.CopyTo(frameBytes.AsSpan(0, BasePacketFrameCodec.HeaderSize));

        int bodyLength = packetSize - BasePacketFrameCodec.HeaderSize;
        if (bodyLength > 0)
            await ReadExactlyAsync(frameBytes.AsMemory(BasePacketFrameCodec.HeaderSize, bodyLength), cancellationToken).ConfigureAwait(false);

        return frameBytes;
    }

    private async ValueTask WriteRawFrameBytesAsync(byte[] frameBytes, CancellationToken cancellationToken)
    {
        if (frameBytes.Length > maxFrameSize)
            throw new InvalidDataException($"Raw legacy frame size {frameBytes.Length} exceeds configured max {maxFrameSize}.");

        await stream.WriteAsync(frameBytes, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<bool> ReadExactlyOrEndAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        int read = 0;
        while (read < destination.Length)
        {
            int current = await stream.ReadAsync(destination[read..], cancellationToken).ConfigureAwait(false);
            if (current == 0)
            {
                if (read == 0)
                    return false;

                throw new EndOfStreamException($"Stream ended after {read} of {destination.Length} bytes.");
            }

            read += current;
        }

        return true;
    }

    private async ValueTask ReadExactlyAsync(Memory<byte> destination, CancellationToken cancellationToken)
    {
        bool success = await ReadExactlyOrEndAsync(destination, cancellationToken).ConfigureAwait(false);
        if (!success)
            throw new EndOfStreamException("Stream ended before the frame body was read.");
    }

    private static bool TryGetFirstNonGameSubPacketType(ReadOnlySpan<byte> frame, out ushort subPacketType)
    {
        subPacketType = 0;
        if (frame.Length < BasePacketFrameCodec.HeaderSize || frame[0x01] != 0)
            return false;

        ushort packetSize = PacketBinary.ReadUInt16LittleEndian(frame[0x04..]);
        if (packetSize > frame.Length)
            return false;

        int offset = BasePacketFrameCodec.HeaderSize;
        while (offset + BasePacketFrameCodec.SubPacketHeaderSize <= packetSize)
        {
            ushort subPacketSize = PacketBinary.ReadUInt16LittleEndian(frame[(offset + 0x00)..]);
            subPacketType = PacketBinary.ReadUInt16LittleEndian(frame[(offset + 0x02)..]);

            if (subPacketSize < BasePacketFrameCodec.SubPacketHeaderSize || offset + subPacketSize > packetSize)
                return false;

            if (subPacketType != BasePacketFrameCodec.GameMessageSubPacketType)
                return true;

            offset += subPacketSize;
        }

        return false;
    }

    private static string[] FormatObservedKeys(PacketDirection direction, IReadOnlyList<WireGameMessageSubPacket> subPackets)
    {
        string[] keys = new string[subPackets.Count];
        for (int index = 0; index < subPackets.Count; index++)
            keys[index] = ObservedProtocolKey.FromSubPacket(direction, subPackets[index]).ToString();

        return keys;
    }

    private static string[] FormatObservedLegacyKeys(PacketDirection direction, IReadOnlyList<WireLegacySubPacket> subPackets)
    {
        string[] keys = new string[subPackets.Count];
        for (int index = 0; index < subPackets.Count; index++)
        {
            WireLegacySubPacket subPacket = subPackets[index];
            ushort subcode = subPacket.IsGameMessage && subPacket.Opcode is not null
                ? (ushort)subPacket.Opcode.Value
                : subPacket.Type;
            int messageLength = RawLegacySubPacketCodec.HeaderSize
                + subPacket.Payload.Length
                + (subPacket.IsGameMessage ? RawLegacySubPacketCodec.GameMessageHeaderSize : 0);
            keys[index] = $"{direction}:type=0x{subPacket.Type:X4}:sub=0x{subcode:X4}:len={messageLength}";
        }

        return keys;
    }
}
