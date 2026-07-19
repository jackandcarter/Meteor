using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace AetherXIV.Core;

public static class LauncherPasswordHasher
{
    private static readonly uint[] K =
    [
        0x428A2F98, 0x71374491, 0xB5C0FBCF, 0xE9B5DBA5,
        0x3956C25B, 0x59F111F1, 0x923F82A4, 0xAB1C5ED5,
        0xD807AA98, 0x12835B01, 0x243185BE, 0x550C7DC3,
        0x72BE5D74, 0x80DEB1FE, 0x9BDC06A7, 0xC19BF174,
        0xE49B69C1, 0xEFBE4786, 0x0FC19DC6, 0x240CA1CC,
        0x2DE92C6F, 0x4A7484AA, 0x5CB0A9DC, 0x76F988DA,
        0x983E5152, 0xA831C66D, 0xB00327C8, 0xBF597FC7,
        0xC6E00BF3, 0xD5A79147, 0x06CA6351, 0x14292967,
        0x27B70A85, 0x2E1B2138, 0x4D2C6DFC, 0x53380D13,
        0x650A7354, 0x766A0ABB, 0x81C2C92E, 0x92722C85,
        0xA2BFE8A1, 0xA81A664B, 0xC24B8B70, 0xC76C51A3,
        0xD192E819, 0xD6990624, 0xF40E3585, 0x106AA070,
        0x19A4C116, 0x1E376C08, 0x2748774C, 0x34B0BCB5,
        0x391C0CB3, 0x4ED8AA4A, 0x5B9CCA4F, 0x682E6FF3,
        0x748F82EE, 0x78A5636F, 0x84C87814, 0x8CC70208,
        0x90BEFFFA, 0xA4506CEB, 0xBEF9A3F7, 0xC67178F2
    ];

    public static string GenerateSalt()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(28);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string HashPassword(string password, string salt)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(salt);

        byte[] input = Encoding.UTF8.GetBytes(password + salt);
        byte[] hash = ComputeSha224(input);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool Verify(string password, string? expectedHash, string? salt)
    {
        if (String.IsNullOrEmpty(expectedHash) || String.IsNullOrEmpty(salt))
            return false;
        if (!TryDecodeSha224Hex(expectedHash, out byte[] expected))
            return false;

        string actualHex = HashPassword(password, salt);
        byte[] actual = Convert.FromHexString(actualHex);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static byte[] ComputeSha224(ReadOnlySpan<byte> data)
    {
        uint h0 = 0xC1059ED8;
        uint h1 = 0x367CD507;
        uint h2 = 0x3070DD17;
        uint h3 = 0xF70E5939;
        uint h4 = 0xFFC00B31;
        uint h5 = 0x68581511;
        uint h6 = 0x64F98FA7;
        uint h7 = 0xBEFA4FA4;

        byte[] padded = PadMessage(data);
        Span<uint> w = stackalloc uint[64];

        for (int offset = 0; offset < padded.Length; offset += 64)
        {
            ReadOnlySpan<byte> block = padded.AsSpan(offset, 64);
            for (int i = 0; i < 16; i++)
                w[i] = BinaryPrimitives.ReadUInt32BigEndian(block.Slice(i * 4, 4));

            for (int i = 16; i < 64; i++)
            {
                uint s0 = RotateRight(w[i - 15], 7) ^ RotateRight(w[i - 15], 18) ^ (w[i - 15] >> 3);
                uint s1 = RotateRight(w[i - 2], 17) ^ RotateRight(w[i - 2], 19) ^ (w[i - 2] >> 10);
                w[i] = unchecked(w[i - 16] + s0 + w[i - 7] + s1);
            }

            uint a = h0;
            uint b = h1;
            uint c = h2;
            uint d = h3;
            uint e = h4;
            uint f = h5;
            uint g = h6;
            uint h = h7;

            for (int i = 0; i < 64; i++)
            {
                uint s1 = RotateRight(e, 6) ^ RotateRight(e, 11) ^ RotateRight(e, 25);
                uint ch = (e & f) ^ (~e & g);
                uint temp1 = unchecked(h + s1 + ch + K[i] + w[i]);
                uint s0 = RotateRight(a, 2) ^ RotateRight(a, 13) ^ RotateRight(a, 22);
                uint maj = (a & b) ^ (a & c) ^ (b & c);
                uint temp2 = unchecked(s0 + maj);

                h = g;
                g = f;
                f = e;
                e = unchecked(d + temp1);
                d = c;
                c = b;
                b = a;
                a = unchecked(temp1 + temp2);
            }

            h0 = unchecked(h0 + a);
            h1 = unchecked(h1 + b);
            h2 = unchecked(h2 + c);
            h3 = unchecked(h3 + d);
            h4 = unchecked(h4 + e);
            h5 = unchecked(h5 + f);
            h6 = unchecked(h6 + g);
            h7 = unchecked(h7 + h);
        }

        byte[] output = new byte[28];
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(0, 4), h0);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(4, 4), h1);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(8, 4), h2);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(12, 4), h3);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(16, 4), h4);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(20, 4), h5);
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(24, 4), h6);
        return output;
    }

    private static byte[] PadMessage(ReadOnlySpan<byte> data)
    {
        ulong bitLength = checked((ulong)data.Length * 8);
        int zeroPaddingLength = (56 - ((data.Length + 1) % 64) + 64) % 64;
        byte[] padded = new byte[data.Length + 1 + zeroPaddingLength + 8];
        data.CopyTo(padded);
        padded[data.Length] = 0x80;
        BinaryPrimitives.WriteUInt64BigEndian(padded.AsSpan(padded.Length - 8, 8), bitLength);
        return padded;
    }

    private static bool TryDecodeSha224Hex(string value, out byte[] bytes)
    {
        bytes = [];
        if (value.Length != 56)
            return false;

        try
        {
            bytes = Convert.FromHexString(value);
            return bytes.Length == 28;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static uint RotateRight(uint value, int bits)
    {
        return (value >> bits) | (value << (32 - bits));
    }
}
