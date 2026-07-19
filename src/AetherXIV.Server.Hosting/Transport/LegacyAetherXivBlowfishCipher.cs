using AetherXIV.Core;
using AetherXIV.Protocol;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace AetherXIV.Server.Hosting;

public sealed class LegacyAetherXivBlowfishCipherFactory : ILegacyStreamCipherFactory
{
    private readonly IDiagnosticSink diagnostics;
    private readonly ILegacySecureAcknowledgementProvider acknowledgementProvider;

    public LegacyAetherXivBlowfishCipherFactory(
        IDiagnosticSink? diagnostics = null,
        ILegacySecureAcknowledgementProvider? acknowledgementProvider = null)
    {
        this.diagnostics = diagnostics ?? NullDiagnosticSink.Instance;
        this.acknowledgementProvider = acknowledgementProvider ?? TraceVerifiedSecureAcknowledgementProvider.Instance;
    }

    public ILegacyStreamCipher CreateCipher(LegacyConnectionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new LegacyAetherXivBlowfishCipher(context, diagnostics, acknowledgementProvider);
    }
}

public interface ILegacySecureAcknowledgementProvider
{
    byte[] CreatePlainAcknowledgement(LegacyClientHandshake handshake);
}

/// <summary>
/// Supplies the secure-connection acknowledgement body recovered from the uploaded
/// retail login trace. This is a trace-backed bootstrap fixture for the lobby
/// handshake path; replace it with a typed composer once all control fields are named.
/// </summary>
public sealed class TraceVerifiedSecureAcknowledgementProvider : ILegacySecureAcknowledgementProvider
{
    public static TraceVerifiedSecureAcknowledgementProvider Instance { get; } = new();

    private const string SecureAckPlainHex = "00000000a0020100000000000000000090020a000000000000000000000000000c6900e000000000d0ed450200000000c0eddfffaff7f7af10efdfff7ffdffff428263520100000010efdfff53616d706c652053616d706c652052756e52756e0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000200f7afaff70000b86c4d0200000000106c4d0200000000402cac010000000063726561746543616c6c6261636b4f626a6563742e2e2e5b36362e3133302e39392e38323a36333430375d000000000070eedfff7ffdffff6c4e38010000000032efdfff7ffdffffaff700000000000002000000000000003832000000000000c0eedfff7ffdfffffe4e3801000000000b0000010100000020efdfff7ffdffff0001cccc0c6900e0d058330200000000100000003000000080efdfff7ffdffffc0eedfff7ffdffffd0ed450200000000f0eedfffaff7f7af20efdfff7ffdffff0c6900e000000000106c4d02000000004500000000000000333430370000000090efdfff7ffdffff18be340100000000d832ac0100000000d032ac01000000000200f7af428263520000000000000000000036362e3133302e39392e38320000000036362e3133302e39392e383200ff90efdfff7ffdffff24cf760100000000106c4d0200000000707ab701000000000000000000000000106c4d020000000090efdfff7ffdffffd1f3370100000000106c4d0200000000a032ac0100000000c0efdfff7ffdffffe83e7701000000007099aa010c6900e0a032ac01000000005859330200000000106c4d0200000000e0efdfff7ffdffff053f7701000000000c6900e00c6900e0a032ac010000000000f0dfff7ffdffff233f770100000000c05a33020c6900e0a032ac0100000000";

    private TraceVerifiedSecureAcknowledgementProvider()
    {
    }

    public byte[] CreatePlainAcknowledgement(LegacyClientHandshake handshake)
    {
        ArgumentNullException.ThrowIfNull(handshake);
        return Convert.FromHexString(SecureAckPlainHex);
    }
}

public sealed class LegacyAetherXivBlowfishCipher : ILegacyStreamCipher
{
    private readonly LegacyConnectionContext context;
    private readonly IDiagnosticSink diagnostics;
    private readonly ILegacySecureAcknowledgementProvider acknowledgementProvider;
    private LegacyAetherXivBlowfishBlockCipher? blockCipher;
    private LegacyClientHandshake? handshake;

    public LegacyAetherXivBlowfishCipher(
        LegacyConnectionContext context,
        IDiagnosticSink? diagnostics = null,
        ILegacySecureAcknowledgementProvider? acknowledgementProvider = null)
    {
        this.context = context;
        this.diagnostics = diagnostics ?? NullDiagnosticSink.Instance;
        this.acknowledgementProvider = acknowledgementProvider ?? TraceVerifiedSecureAcknowledgementProvider.Instance;
    }

    public string Name => blockCipher is null ? "legacy-aetherxiv-blowfish-pending-handshake" : "legacy-aetherxiv-blowfish";

    public bool TryHandleControlFrame(ReadOnlySpan<byte> frame, out byte[]? immediateResponse)
    {
        immediateResponse = null;
        if (!LegacyClientHandshakeParser.TryParse(frame, out LegacyClientHandshake parsedHandshake))
            return false;

        byte[] sessionKey = LegacyClientHandshakeParser.DeriveSessionKey(
            parsedHandshake.TicketPhrase,
            parsedHandshake.ClientNumber);
        blockCipher = new LegacyAetherXivBlowfishBlockCipher(sessionKey);
        handshake = parsedHandshake;

        byte[] response = acknowledgementProvider.CreatePlainAcknowledgement(parsedHandshake);
        TransformOutboundFrame(response);
        immediateResponse = response;

        diagnostics.Trace("legacy.blowfish.handshake", new Dictionary<string, object?>
        {
            ["connectionId"] = context.ConnectionId,
            ["remote"] = context.RemoteEndPoint?.ToString(),
            ["ticketPhrase"] = parsedHandshake.TicketPhrase,
            ["ticketLength"] = parsedHandshake.TicketPhrase.Length,
            ["clientNumber"] = $"0x{parsedHandshake.ClientNumber:X8}",
            ["sessionKey"] = Convert.ToHexString(sessionKey),
            ["responseBytes"] = response.Length,
            ["traceBasis"] = "ffxiv_traces/login.pcapng secure-start + recovered secure ack plaintext"
        });

        return true;
    }

    public void TransformInboundFrame(Span<byte> frame)
    {
        TransformFrameSubPacketBodies(frame, decrypt: true);
    }

    public void TransformOutboundFrame(Span<byte> frame)
    {
        TransformFrameSubPacketBodies(frame, decrypt: false);
    }

    private void TransformFrameSubPacketBodies(Span<byte> frame, bool decrypt)
    {
        if (blockCipher is null)
            return;

        if (frame.Length < BasePacketFrameCodec.HeaderSize)
            return;

        if (frame[0x01] != 0)
        {
            diagnostics.Trace("legacy.blowfish.skip-compressed", new Dictionary<string, object?>
            {
                ["connectionId"] = context.ConnectionId,
                ["direction"] = decrypt ? "inbound" : "outbound",
                ["reason"] = "compressed frame encryption coverage is not promoted yet"
            });
            return;
        }

        ushort packetSize = PacketBinary.ReadUInt16LittleEndian(frame[0x04..]);
        if (packetSize > frame.Length || packetSize < BasePacketFrameCodec.HeaderSize)
            return;

        int offset = BasePacketFrameCodec.HeaderSize;
        while (offset + BasePacketFrameCodec.SubPacketHeaderSize <= packetSize)
        {
            ushort subPacketSize = PacketBinary.ReadUInt16LittleEndian(frame[(offset + 0x00)..]);
            if (subPacketSize < BasePacketFrameCodec.SubPacketHeaderSize || offset + subPacketSize > packetSize)
                break;

            int bodyOffset = offset + BasePacketFrameCodec.SubPacketHeaderSize;
            int bodyLength = subPacketSize - BasePacketFrameCodec.SubPacketHeaderSize;
            int alignedBodyLength = bodyLength & ~0x7;
            if (alignedBodyLength > 0)
                blockCipher.Transform(frame.Slice(bodyOffset, alignedBodyLength), decrypt);

            offset += subPacketSize;
        }
    }
}

/// <summary>
/// BouncyCastle adapter that reproduces the AetherXIV 1.23b Blowfish behavior used by the
/// working 1.23b lobby path:
/// - the MD5 session key is consumed through the legacy signed-byte key schedule;
/// - each 8-byte block is made of two little-endian UInt32 halves.
/// </summary>
public sealed class LegacyAetherXivBlowfishBlockCipher
{
    private readonly BlowfishEngine encryptEngine = new();
    private readonly BlowfishEngine decryptEngine = new();

    public LegacyAetherXivBlowfishBlockCipher(ReadOnlySpan<byte> sessionKey)
    {
        if (sessionKey.Length == 0 || sessionKey.Length % 4 != 0)
            throw new ArgumentException("session key length must be a non-empty multiple of four bytes.", nameof(sessionKey));

        byte[] normalizedKey = NormalizeSignedKeyScheduleWords(sessionKey);
        KeyParameter parameter = new(normalizedKey);
        encryptEngine.Init(true, parameter);
        decryptEngine.Init(false, parameter);
    }

    public void Transform(Span<byte> buffer, bool decrypt)
    {
        int blockLength = buffer.Length & ~0x7;
        BlowfishEngine engine = decrypt ? decryptEngine : encryptEngine;
        Span<byte> block = stackalloc byte[8];

        for (int offset = 0; offset < blockLength; offset += 8)
        {
            ReverseWordHalves(buffer.Slice(offset, 8), block);
            byte[] input = block.ToArray();
            byte[] output = new byte[8];
            engine.ProcessBlock(input, 0, output, 0);
            ReverseWordHalves(output, buffer.Slice(offset, 8));
        }
    }

    private static byte[] NormalizeSignedKeyScheduleWords(ReadOnlySpan<byte> key)
    {
        byte[] normalized = new byte[key.Length];
        for (int offset = 0; offset < key.Length; offset += 4)
        {
            int data = 0;
            for (int index = 0; index < 4; index++)
            {
                int signedByte = unchecked((sbyte)key[offset + index]);
                unchecked { data = (data << 8) | signedByte; }
            }

            unchecked
            {
                normalized[offset + 0] = (byte)((uint)data >> 24);
                normalized[offset + 1] = (byte)((uint)data >> 16);
                normalized[offset + 2] = (byte)((uint)data >> 8);
                normalized[offset + 3] = (byte)((uint)data);
            }
        }

        return normalized;
    }

    private static void ReverseWordHalves(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        if (source.Length < 8 || destination.Length < 8)
            throw new ArgumentException("Blowfish block transform requires eight bytes.");

        destination[0] = source[3];
        destination[1] = source[2];
        destination[2] = source[1];
        destination[3] = source[0];
        destination[4] = source[7];
        destination[5] = source[6];
        destination[6] = source[5];
        destination[7] = source[4];
    }
}
