using AetherXIV.Protocol;
using AetherXIV.Server.Hosting;

namespace AetherXIV.Server.Hosting.Tests;

public sealed class LegacyBlowfishHandshakeTests
{
    [Fact]
    public void ParseSecureStartFrameAndDeriveSessionKey()
    {
        byte[] frame = CreateSecureStartFrame("Test Ticket Data", 0x50E0E812);

        bool parsed = LegacyClientHandshakeParser.TryParse(frame, out LegacyClientHandshake handshake);

        Assert.True(parsed);
        Assert.Equal("Test Ticket Data", handshake.TicketPhrase);
        Assert.Equal(0x50E0E812u, handshake.ClientNumber);
        Assert.Equal(0x0288, handshake.PacketSize);
        Assert.Equal(0x0003, handshake.ConnectionType);
        Assert.Equal(0x0278, handshake.SubPacketSize);
        Assert.Equal(0x0009, handshake.SubPacketType);

        byte[] key = LegacyClientHandshakeParser.DeriveSessionKey(handshake.TicketPhrase, handshake.ClientNumber);
        Assert.Equal("B4EE3F6C016F5BD971500DB185A2AB43", Convert.ToHexString(key));
    }

    [Fact]
    public void LegacyAetherXivBlowfishMatchesSecureAckVector()
    {
        byte[] key = Convert.FromHexString("B4EE3F6C016F5BD971500DB185A2AB43");
        byte[] plain = Convert.FromHexString(
            "0C6900E000000000D0ED450200000000" +
            "C0EDDFFFAFF7F7AF10EFDFFF7FFDFFFF" +
            "428263520100000010EFDFFF53616D70" +
            "6C652053616D706C652052756E52756E" +
            "0000000000000000");
        byte[] expectedCipher = Convert.FromHexString(
            "03FFF6CD28A310F39484E920A6ACA740" +
            "9AAB875CD3B98894ADAAF6D0CE262E73" +
            "FDEBB73A85B2215E6CEAC9309D737D2F" +
            "01A5A5034B8F4E623F9DFABB1FD09B6F" +
            "0410D0F11267315E");

        byte[] encrypted = plain.ToArray();
        LegacyAetherXivBlowfishBlockCipher cipher = new(key);
        cipher.Transform(encrypted, decrypt: false);

        Assert.Equal(expectedCipher, encrypted);

        cipher.Transform(encrypted, decrypt: true);
        Assert.Equal(plain, encrypted);
    }

    private static byte[] CreateSecureStartFrame(string ticketPhrase, uint clientNumber)
    {
        byte[] frame = new byte[LegacyClientHandshakeParser.ExpectedPacketSize];
        frame[0x00] = 1;
        frame[0x01] = 0;
        PacketBinary.WriteUInt16LittleEndian(frame.AsSpan(0x02), LegacyClientHandshakeParser.ExpectedConnectionType);
        PacketBinary.WriteUInt16LittleEndian(frame.AsSpan(0x04), LegacyClientHandshakeParser.ExpectedPacketSize);
        PacketBinary.WriteUInt16LittleEndian(frame.AsSpan(0x06), 1);
        PacketBinary.WriteUInt16LittleEndian(frame.AsSpan(BasePacketFrameCodec.HeaderSize), LegacyClientHandshakeParser.ExpectedSubPacketSize);
        PacketBinary.WriteUInt16LittleEndian(frame.AsSpan(BasePacketFrameCodec.HeaderSize + 0x02), LegacyClientHandshakeParser.ExpectedSubPacketType);

        byte[] phrase = System.Text.Encoding.ASCII.GetBytes(ticketPhrase);
        phrase.AsSpan().CopyTo(frame.AsSpan(LegacyClientHandshakeParser.TicketPhraseFrameOffset));
        PacketBinary.WriteUInt32LittleEndian(frame.AsSpan(LegacyClientHandshakeParser.ClientNumberFrameOffset), clientNumber);
        return frame;
    }
}
