using System.Buffers.Binary;
using System.Text;

namespace AetherXIV.Protocol;

public enum LuaParameterType : byte
{
    Int32 = 0x00,
    UInt32 = 0x01,
    String = 0x02,
    BooleanTrue = 0x03,
    BooleanFalse = 0x04,
    Null = 0x05,
    ActorId = 0x06,
    ItemReference = 0x07,
    ItemOffer = 0x08,
    PairOfUInt64 = 0x09,
    UInt8 = 0x0C,
    UInt16LittleEndian = 0x1B
}

public readonly record struct LuaParameter(LuaParameterType Type, object? Value);

public readonly record struct LuaItemReference(
    uint ActorId,
    byte Unknown,
    byte Slot,
    byte InventoryType);

public readonly record struct LuaItemOffer(
    uint ActorId,
    ushort RewardSlot,
    byte RewardPackageId,
    byte Unknown1,
    ushort SeekSlot,
    byte SeekPackageId,
    byte Unknown2);

public readonly record struct LuaUInt64Pair(ulong First, ulong Second);

public static class LuaParameterCodec
{
    public static byte[] Encode(IReadOnlyList<LuaParameter> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        using MemoryStream stream = new();
        foreach (LuaParameter parameter in parameters)
        {
            stream.WriteByte((byte)parameter.Type);

            switch (parameter.Type)
            {
                case LuaParameterType.Int32:
                    WriteInt32BigEndian(stream, Convert.ToInt32(parameter.Value));
                    break;
                case LuaParameterType.UInt32:
                case LuaParameterType.ActorId:
                    WriteUInt32BigEndian(stream, Convert.ToUInt32(parameter.Value));
                    break;
                case LuaParameterType.UInt8:
                    stream.WriteByte(Convert.ToByte(parameter.Value));
                    break;
                case LuaParameterType.ItemReference:
                    WriteItemReference(stream, (LuaItemReference)parameter.Value!);
                    break;
                case LuaParameterType.ItemOffer:
                    WriteItemOffer(stream, (LuaItemOffer)parameter.Value!);
                    break;
                case LuaParameterType.PairOfUInt64:
                    WriteUInt64Pair(stream, (LuaUInt64Pair)parameter.Value!);
                    break;
                case LuaParameterType.UInt16LittleEndian:
                    WriteUInt16LittleEndian(stream, Convert.ToUInt16(parameter.Value));
                    break;
                case LuaParameterType.String:
                    WriteNullTerminatedString(stream, Convert.ToString(parameter.Value) ?? string.Empty);
                    break;
                case LuaParameterType.Null:
                case LuaParameterType.BooleanFalse:
                case LuaParameterType.BooleanTrue:
                    break;
                default:
                    throw new NotSupportedException($"Lua parameter type {parameter.Type} is not supported yet.");
            }
        }

        stream.WriteByte(0x0F);
        return stream.ToArray();
    }

    public static IReadOnlyList<LuaParameter> Decode(ReadOnlySpan<byte> payload)
    {
        List<LuaParameter> parameters = new();
        int offset = 0;

        while (offset < payload.Length)
        {
            LuaParameterType type = (LuaParameterType)payload[offset++];
            object? value = type switch
            {
                LuaParameterType.Int32 => ReadInt32(payload, ref offset),
                LuaParameterType.UInt32 => ReadUInt32(payload, ref offset),
                LuaParameterType.ActorId => ReadUInt32(payload, ref offset),
                LuaParameterType.UInt8 => ReadUInt8(payload, ref offset),
                LuaParameterType.ItemReference => ReadItemReference(payload, ref offset),
                LuaParameterType.ItemOffer => ReadItemOffer(payload, ref offset),
                LuaParameterType.PairOfUInt64 => ReadUInt64Pair(payload, ref offset),
                LuaParameterType.UInt16LittleEndian => ReadUInt16LittleEndian(payload, ref offset),
                LuaParameterType.String => ReadNullTerminatedString(payload, ref offset),
                LuaParameterType.Null => null,
                LuaParameterType.BooleanFalse => false,
                LuaParameterType.BooleanTrue => true,
                (LuaParameterType)0x0F => null,
                _ => throw new NotSupportedException($"Lua parameter type {type} is not supported yet.")
            };

            if (type == (LuaParameterType)0x0F)
                break;

            parameters.Add(new LuaParameter(type, value));
        }

        return parameters;
    }

    private static int ReadInt32(ReadOnlySpan<byte> payload, ref int offset)
    {
        Require(payload, offset, 4);
        int value = BinaryPrimitives.ReadInt32BigEndian(payload.Slice(offset, 4));
        offset += 4;
        return value;
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> payload, ref int offset)
    {
        Require(payload, offset, 4);
        uint value = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(offset, 4));
        offset += 4;
        return value;
    }

    private static byte ReadUInt8(ReadOnlySpan<byte> payload, ref int offset)
    {
        Require(payload, offset, 1);
        return payload[offset++];
    }

    private static LuaItemReference ReadItemReference(ReadOnlySpan<byte> payload, ref int offset)
    {
        Require(payload, offset, 7);
        LuaItemReference value = new(
            BinaryPrimitives.ReadUInt32BigEndian(payload[offset..]),
            payload[offset + 4],
            payload[offset + 5],
            payload[offset + 6]);
        offset += 7;
        return value;
    }

    private static LuaItemOffer ReadItemOffer(ReadOnlySpan<byte> payload, ref int offset)
    {
        Require(payload, offset, 12);
        LuaItemOffer value = new(
            BinaryPrimitives.ReadUInt32BigEndian(payload[offset..]),
            BinaryPrimitives.ReadUInt16BigEndian(payload[(offset + 4)..]),
            payload[offset + 6],
            payload[offset + 7],
            BinaryPrimitives.ReadUInt16BigEndian(payload[(offset + 8)..]),
            payload[offset + 10],
            payload[offset + 11]);
        offset += 12;
        return value;
    }

    private static LuaUInt64Pair ReadUInt64Pair(ReadOnlySpan<byte> payload, ref int offset)
    {
        Require(payload, offset, 16);
        LuaUInt64Pair value = new(
            BinaryPrimitives.ReadUInt64BigEndian(payload[offset..]),
            BinaryPrimitives.ReadUInt64BigEndian(payload[(offset + 8)..]));
        offset += 16;
        return value;
    }

    private static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> payload, ref int offset)
    {
        Require(payload, offset, 2);
        ushort value = BinaryPrimitives.ReadUInt16LittleEndian(payload[offset..]);
        offset += 2;
        return value;
    }

    private static string ReadNullTerminatedString(ReadOnlySpan<byte> payload, ref int offset)
    {
        int terminator = payload[offset..].IndexOf((byte)0);
        if (terminator < 0)
            throw new InvalidDataException("Lua string parameter is missing a null terminator.");

        string value = Encoding.UTF8.GetString(payload.Slice(offset, terminator));
        offset += terminator + 1;
        return value;
    }

    private static void WriteNullTerminatedString(Stream stream, string value)
    {
        stream.Write(Encoding.UTF8.GetBytes(value));
        stream.WriteByte(0);
    }

    private static void WriteInt32BigEndian(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt32BigEndian(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteItemReference(Stream stream, LuaItemReference value)
    {
        WriteUInt32BigEndian(stream, value.ActorId);
        stream.WriteByte(value.Unknown);
        stream.WriteByte(value.Slot);
        stream.WriteByte(value.InventoryType);
    }

    private static void WriteItemOffer(Stream stream, LuaItemOffer value)
    {
        WriteUInt32BigEndian(stream, value.ActorId);
        WriteUInt16BigEndian(stream, value.RewardSlot);
        stream.WriteByte(value.RewardPackageId);
        stream.WriteByte(value.Unknown1);
        WriteUInt16BigEndian(stream, value.SeekSlot);
        stream.WriteByte(value.SeekPackageId);
        stream.WriteByte(value.Unknown2);
    }

    private static void WriteUInt64Pair(Stream stream, LuaUInt64Pair value)
    {
        Span<byte> buffer = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64BigEndian(buffer, value.First);
        BinaryPrimitives.WriteUInt64BigEndian(buffer[8..], value.Second);
        stream.Write(buffer);
    }

    private static void WriteUInt16BigEndian(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt16LittleEndian(Stream stream, ushort value)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void Require(ReadOnlySpan<byte> payload, int offset, int length)
    {
        if (payload.Length - offset < length)
            throw new InvalidDataException("Lua parameter payload ended unexpectedly.");
    }
}
