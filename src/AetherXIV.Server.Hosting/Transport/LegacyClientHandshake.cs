using System.Security.Cryptography;
using System.Text;
using AetherXIV.Protocol;

namespace AetherXIV.Server.Hosting;

public sealed record LegacyClientHandshake(
    string TicketPhrase,
    uint ClientNumber,
    ushort PacketSize,
    ushort ConnectionType,
    ushort SubPacketSize,
    ushort SubPacketType);

public static class LegacyClientHandshakeParser
{
    public const ushort ExpectedPacketSize = 0x0288;
    public const ushort ExpectedConnectionType = 0x0003;
    public const ushort ExpectedSubPacketSize = 0x0278;
    public const ushort ExpectedSubPacketType = 0x0009;

    public const int TicketPhraseFrameOffset = BasePacketFrameCodec.HeaderSize + 0x34;
    public const int TicketPhraseLength = 0x40;
    public const int ClientNumberFrameOffset = TicketPhraseFrameOffset + TicketPhraseLength;

    private static readonly byte[] TestTicketPrefix = Encoding.ASCII.GetBytes("Test Ticket Data");

    public static bool TryParse(ReadOnlySpan<byte> frame, out LegacyClientHandshake handshake)
    {
        handshake = default!;

        if (frame.Length < ExpectedPacketSize)
            return false;

        ushort packetSize = PacketBinary.ReadUInt16LittleEndian(frame[0x04..]);
        ushort connectionType = PacketBinary.ReadUInt16LittleEndian(frame[0x02..]);
        ushort subPacketSize = PacketBinary.ReadUInt16LittleEndian(frame[BasePacketFrameCodec.HeaderSize..]);
        ushort subPacketType = PacketBinary.ReadUInt16LittleEndian(frame[(BasePacketFrameCodec.HeaderSize + 0x02)..]);

        if (packetSize != ExpectedPacketSize || frame.Length < packetSize)
            return false;

        if (connectionType != ExpectedConnectionType)
            return false;

        if (subPacketSize != ExpectedSubPacketSize || subPacketType != ExpectedSubPacketType)
            return false;

        ReadOnlySpan<byte> ticketBytes = frame.Slice(TicketPhraseFrameOffset, TicketPhraseLength);
        if (!ticketBytes.StartsWith(TestTicketPrefix))
            return false;

        int nulIndex = ticketBytes.IndexOf((byte)0);
        if (nulIndex < 0)
            nulIndex = ticketBytes.Length;

        string ticketPhrase = Encoding.ASCII.GetString(ticketBytes[..nulIndex]).TrimEnd('\0');
        if (ticketPhrase.Length == 0)
            return false;

        uint clientNumber = PacketBinary.ReadUInt32LittleEndian(frame[ClientNumberFrameOffset..]);
        handshake = new LegacyClientHandshake(
            ticketPhrase,
            clientNumber,
            packetSize,
            connectionType,
            subPacketSize,
            subPacketType);
        return true;
    }

    /// <summary>
    /// Derives the AetherXIV 1.23b-compatible 16-byte Blowfish key from the secure-start
    /// ticket phrase and client number.
    /// </summary>
    public static byte[] DeriveSessionKey(string ticketPhrase, uint clientNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketPhrase);

        Span<byte> keyMaterial = stackalloc byte[0x2C];
        keyMaterial[0x00] = 0x78;
        keyMaterial[0x01] = 0x56;
        keyMaterial[0x02] = 0x34;
        keyMaterial[0x03] = 0x12;
        PacketBinary.WriteUInt32LittleEndian(keyMaterial[0x04..], clientNumber);
        keyMaterial[0x08] = 0xE8;
        keyMaterial[0x09] = 0x03;
        keyMaterial[0x0A] = 0x00;
        keyMaterial[0x0B] = 0x00;

        byte[] phraseBytes = Encoding.ASCII.GetBytes(ticketPhrase);
        phraseBytes.AsSpan(0, Math.Min(phraseBytes.Length, 0x20)).CopyTo(keyMaterial[0x0C..]);
        return MD5.HashData(keyMaterial);
    }
}
