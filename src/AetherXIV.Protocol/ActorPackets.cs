using static AetherXIV.Protocol.ActorPacketCodecHelpers;
using System.Text;

namespace AetherXIV.Protocol;

public readonly record struct AddActorPacket(byte Value);

public sealed class AddActorPacketCodec : IPacketCodec<AddActorPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.AddActor;

    public Type PacketType => typeof(AddActorPacket);

    public AddActorPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new AddActorPacket(payload[0]);
    }

    public SubPacket Encode(uint sourceActorId, AddActorPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        payload[0] = packet.Value;
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct PlayerCommandCategoryPacket(ushort Number, string FunctionName);

public sealed class PlayerCommandCategoryPacketCodec : IPacketCodec<PlayerCommandCategoryPacket>
{
    public const int PayloadSize = 0x48 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.PlayerCommandCategory;

    public Type PacketType => typeof(PlayerCommandCategoryPacket);

    public PlayerCommandCategoryPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new PlayerCommandCategoryPacket(
            PacketBinary.ReadUInt16LittleEndian(payload),
            EventStartPacketCodec.ReadFixedString(payload[2..], 0x20));
    }

    public SubPacket Encode(uint sourceActorId, PlayerCommandCategoryPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt16LittleEndian(payload, packet.Number);
        EventStartPacketCodec.WriteFixedString(payload.AsSpan(2), 0x20, packet.FunctionName);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct SetActorSpeedPacket(
    float Stop,
    float Walk,
    float Run,
    float Active)
{
    public static SetActorSpeedPacket LegacyDefault { get; } = new(0.0f, 2.0f, 5.0f, 5.0f);
}

public sealed class SetActorSpeedPacketCodec : IPacketCodec<SetActorSpeedPacket>
{
    public const int PayloadSize = 0xA8 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetActorSpeed;

    public Type PacketType => typeof(SetActorSpeedPacket);

    public SetActorSpeedPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new SetActorSpeedPacket(
            PacketBinary.ReadSingleLittleEndian(payload),
            PacketBinary.ReadSingleLittleEndian(payload[8..]),
            PacketBinary.ReadSingleLittleEndian(payload[16..]),
            PacketBinary.ReadSingleLittleEndian(payload[24..]));
    }

    public SubPacket Encode(uint sourceActorId, SetActorSpeedPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        WriteSpeedEntry(payload.AsSpan(0), packet.Stop, 0);
        WriteSpeedEntry(payload.AsSpan(8), packet.Walk, 1);
        WriteSpeedEntry(payload.AsSpan(16), packet.Run, 2);
        WriteSpeedEntry(payload.AsSpan(24), packet.Active, 3);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x80), 4);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }

    private static void WriteSpeedEntry(Span<byte> payload, float speed, uint index)
    {
        PacketBinary.WriteSingleLittleEndian(payload, speed);
        PacketBinary.WriteUInt32LittleEndian(payload[4..], index);
    }
}

public readonly record struct SetActorPositionPacket(
    uint ActorId,
    float X,
    float Y,
    float Z,
    float Rotation,
    ushort SpawnType,
    bool IsZoningPlayer);

public sealed class SetActorPositionPacketCodec : IPacketCodec<SetActorPositionPacket>
{
    public const int PayloadSize = 0x48 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetActorPosition;

    public Type PacketType => typeof(SetActorPositionPacket);

    public SetActorPositionPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new SetActorPositionPacket(
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            PacketBinary.ReadSingleLittleEndian(payload[8..]),
            PacketBinary.ReadSingleLittleEndian(payload[12..]),
            PacketBinary.ReadSingleLittleEndian(payload[16..]),
            PacketBinary.ReadSingleLittleEndian(payload[20..]),
            PacketBinary.ReadUInt16LittleEndian(payload[0x24..]),
            PacketBinary.ReadUInt16LittleEndian(payload[0x26..]) != 0);
    }

    public SubPacket Encode(uint sourceActorId, SetActorPositionPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, 0);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.ActorId);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(8), packet.X);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(12), packet.Y);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(16), packet.Z);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(20), packet.Rotation);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x24), packet.SpawnType);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x26), packet.IsZoningPlayer ? (ushort)1 : (ushort)0);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct MoveActorToPositionPacket(
    float X,
    float Y,
    float Z,
    float Rotation,
    ushort MoveState);

public sealed class MoveActorToPositionPacketCodec : IPacketCodec<MoveActorToPositionPacket>
{
    public const int PayloadSize = 0x50 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.MoveActorToPosition;

    public Type PacketType => typeof(MoveActorToPositionPacket);

    public MoveActorToPositionPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new MoveActorToPositionPacket(
            PacketBinary.ReadSingleLittleEndian(payload[8..]),
            PacketBinary.ReadSingleLittleEndian(payload[12..]),
            PacketBinary.ReadSingleLittleEndian(payload[16..]),
            PacketBinary.ReadSingleLittleEndian(payload[20..]),
            PacketBinary.ReadUInt16LittleEndian(payload[24..]));
    }

    public SubPacket Encode(uint sourceActorId, MoveActorToPositionPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(8), packet.X);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(12), packet.Y);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(16), packet.Z);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(20), packet.Rotation);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(24), packet.MoveState);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct RemoveActorPacket(uint ActorId);

public sealed class RemoveActorPacketCodec : IPacketCodec<RemoveActorPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.RemoveActor;

    public Type PacketType => typeof(RemoveActorPacket);

    public RemoveActorPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        EnsurePayload(packet, PayloadSize);
        return new RemoveActorPacket(packet.Header.SourceActorId);
    }

    public SubPacket Encode(uint sourceActorId, RemoveActorPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        return SubPacket.Create(Opcode, packet.ActorId, payload);
    }
}

public readonly record struct SetActorTargetAnimatedPacket(uint TargetActorId);

public sealed class SetActorTargetAnimatedPacketCodec : IPacketCodec<SetActorTargetAnimatedPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetActorTargetAnimated;

    public Type PacketType => typeof(SetActorTargetAnimatedPacket);

    public SetActorTargetAnimatedPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new SetActorTargetAnimatedPacket((uint)PacketBinary.ReadUInt64LittleEndian(payload));
    }

    public SubPacket Encode(uint sourceActorId, SetActorTargetAnimatedPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.TargetActorId);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct SetActorNamePacket(uint DisplayNameId, string CustomName);

public sealed class SetActorNamePacketCodec : IPacketCodec<SetActorNamePacket>
{
    public const int PayloadSize = 0x48 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetActorName;

    public Type PacketType => typeof(SetActorNamePacket);

    public SetActorNamePacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new SetActorNamePacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            EventStartPacketCodec.ReadFixedString(payload[4..], 0x20));
    }

    public SubPacket Encode(uint sourceActorId, SetActorNamePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.DisplayNameId);
        if (packet.DisplayNameId is 0 or 0xFFFFFFFF)
            EventStartPacketCodec.WriteFixedString(payload.AsSpan(4), 0x19, packet.CustomName);

        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct SetActorStatePacket(uint MainState, uint SubState)
{
    public const byte PlayerSubState = 0xBF;
    public const byte MonsterSubState = 0x03;
}

public sealed class SetActorStatePacketCodec : IPacketCodec<SetActorStatePacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetActorState;

    public Type PacketType => typeof(SetActorStatePacket);

    public SetActorStatePacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        ulong combined = PacketBinary.ReadUInt64LittleEndian(payload);
        return new SetActorStatePacket((uint)(combined & 0xFF), (uint)((combined >> 8) & 0xFF));
    }

    public SubPacket Encode(uint sourceActorId, SetActorStatePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        ulong combined = (packet.MainState & 0xFF) | ((packet.SubState & 0xFF) << 8);
        PacketBinary.WriteUInt64LittleEndian(payload, combined);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct SetActorIsZoningPacket(bool IsDimmed);

public sealed class SetActorIsZoningPacketCodec : IPacketCodec<SetActorIsZoningPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetActorIsZoning;

    public Type PacketType => typeof(SetActorIsZoningPacket);

    public SetActorIsZoningPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new SetActorIsZoningPacket(payload[0] != 0);
    }

    public SubPacket Encode(uint sourceActorId, SetActorIsZoningPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        payload[0] = packet.IsDimmed ? (byte)1 : (byte)0;
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct MapPlayerSpawnUnknownPacket;

public sealed class MapPlayerSpawnUnknownPacketCodec : IPacketCodec<MapPlayerSpawnUnknownPacket>
{
    public const int PayloadSize = 0x38 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.MapPlayerSpawnUnknown0x000F;

    public Type PacketType => typeof(MapPlayerSpawnUnknownPacket);

    public MapPlayerSpawnUnknownPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        EnsurePayload(packet, PayloadSize);
        return new MapPlayerSpawnUnknownPacket();
    }

    public SubPacket Encode(uint sourceActorId, MapPlayerSpawnUnknownPacket packet)
    {
        return SubPacket.Create(Opcode, sourceActorId, new byte[PayloadSize]);
    }
}

public sealed record ActorInstantiatePacket(
    string ObjectName,
    string ClassName,
    IReadOnlyList<LuaParameter> InitParameters);

public sealed class ActorInstantiatePacketCodec : IPacketCodec<ActorInstantiatePacket>
{
    public const int PayloadSize = 0x128 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.ActorInstantiate;

    public Type PacketType => typeof(ActorInstantiatePacket);

    public ActorInstantiatePacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new ActorInstantiatePacket(
            EventStartPacketCodec.ReadFixedString(payload[4..], 0x20),
            EventStartPacketCodec.ReadFixedString(payload[0x24..], 0x20),
            LuaParameterCodec.Decode(payload[0x44..]));
    }

    public SubPacket Encode(uint sourceActorId, ActorInstantiatePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt16LittleEndian(payload, 0);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(2), 0x3040);
        EventStartPacketCodec.WriteFixedString(payload.AsSpan(4), 0x20, packet.ObjectName);
        EventStartPacketCodec.WriteFixedString(payload.AsSpan(0x24), 0x20, packet.ClassName);
        LuaParameterCodec.Encode(packet.InitParameters).CopyTo(payload.AsSpan(0x44));
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public enum ActorPropertyValueKind : byte
{
    Byte = 1,
    UInt16 = 2,
    UInt32 = 4,
    Buffer = 0xFF
}

public sealed record ActorPropertyValue(uint PropertyId, ActorPropertyValueKind Kind, object Value)
{
    public static ActorPropertyValue Byte(uint propertyId, byte value) =>
        new(propertyId, ActorPropertyValueKind.Byte, value);

    public static ActorPropertyValue UInt16(uint propertyId, ushort value) =>
        new(propertyId, ActorPropertyValueKind.UInt16, value);

    public static ActorPropertyValue UInt32(uint propertyId, uint value) =>
        new(propertyId, ActorPropertyValueKind.UInt32, value);

    public static ActorPropertyValue Buffer(uint propertyId, ReadOnlyMemory<byte> value) =>
        new(propertyId, ActorPropertyValueKind.Buffer, value.ToArray());
}

public sealed record SetActorPropertyPacket(
    string Target,
    IReadOnlyList<ActorPropertyValue> Values,
    bool IsArrayMode = false,
    bool HasMore = false);

public sealed class SetActorPropertyPacketCodec : IPacketCodec<SetActorPropertyPacket>
{
    public const int PayloadSize = 0xA8 - 0x20;
    public const int MaxBytes = 0x7D;

    public PacketOpcode Opcode => PacketOpcode.SetActorProperty;

    public Type PacketType => typeof(SetActorPropertyPacket);

    public SetActorPropertyPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        int usedBytes = payload[0];
        if (usedBytes <= 0 || usedBytes > MaxBytes)
            throw new InvalidDataException($"Actor property packet used byte count {usedBytes} is outside the legacy v1 payload limit.");
        int endOffset = usedBytes + 1;

        int offset = 1;
        List<ActorPropertyValue> values = new();
        while (offset < endOffset)
        {
            byte marker = payload[offset];
            if (IsTargetMarker(marker))
                break;

            if (offset + 5 > endOffset)
                throw new InvalidDataException("Actor property entry ended before its property id.");

            byte size = marker;
            uint propertyId = PacketBinary.ReadUInt32LittleEndian(payload[(offset + 1)..]);
            offset += 5;

            if (offset + size > endOffset)
                throw new InvalidDataException("Actor property entry ended before its value bytes.");

            values.Add(DecodeValue(propertyId, size, payload.Slice(offset, size)));
            offset += size;
        }

        if (offset >= endOffset)
            throw new InvalidDataException("Actor property packet ended before target marker.");

        byte targetMarker = payload[offset++];
        int remainingTargetBytes = endOffset - offset;
        bool isArrayMode = targetMarker >= 0xA4
            && targetMarker - 0xA4 == remainingTargetBytes;
        bool hasMore = !isArrayMode
            && targetMarker >= 0x60
            && targetMarker - 0x60 == remainingTargetBytes;
        bool isFinalTarget = !isArrayMode
            && !hasMore
            && targetMarker >= 0x82
            && targetMarker - 0x82 == remainingTargetBytes;
        if (!isArrayMode && !hasMore && !isFinalTarget)
            throw new InvalidDataException("Actor property target marker had an invalid target length.");

        string target = Encoding.ASCII.GetString(payload.Slice(offset, remainingTargetBytes));
        return new SetActorPropertyPacket(target, values, isArrayMode, hasMore);
    }

    public SubPacket Encode(uint sourceActorId, SetActorPropertyPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        int offset = 1;
        foreach (ActorPropertyValue value in packet.Values)
            offset += WriteValue(payload.AsSpan(offset), value);

        int targetByteCount = Encoding.ASCII.GetByteCount(packet.Target);
        int payloadBytes = offset + 1 + targetByteCount;
        int usedBytes = payloadBytes - 1;
        if (usedBytes > MaxBytes)
            throw new InvalidDataException($"Actor property packet would use {usedBytes} bytes, exceeding the legacy v1 server's {MaxBytes} byte payload limit.");

        payload[offset++] = BuildTargetMarker(packet.Target, packet.IsArrayMode, packet.HasMore);
        Encoding.ASCII.GetBytes(packet.Target, payload.AsSpan(offset, targetByteCount));
        payload[0] = (byte)usedBytes;
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }

    private static int WriteValue(Span<byte> destination, ActorPropertyValue value)
    {
        switch (value.Kind)
        {
            case ActorPropertyValueKind.Byte:
                destination[0] = 1;
                PacketBinary.WriteUInt32LittleEndian(destination[1..], value.PropertyId);
                destination[5] = Convert.ToByte(value.Value);
                return 6;
            case ActorPropertyValueKind.UInt16:
                destination[0] = 2;
                PacketBinary.WriteUInt32LittleEndian(destination[1..], value.PropertyId);
                PacketBinary.WriteUInt16LittleEndian(destination[5..], Convert.ToUInt16(value.Value));
                return 7;
            case ActorPropertyValueKind.UInt32:
                destination[0] = 4;
                PacketBinary.WriteUInt32LittleEndian(destination[1..], value.PropertyId);
                PacketBinary.WriteUInt32LittleEndian(destination[5..], Convert.ToUInt32(value.Value));
                return 9;
            case ActorPropertyValueKind.Buffer:
                byte[] bytes = value.Value switch
                {
                    byte[] buffer => buffer,
                    ReadOnlyMemory<byte> memory => memory.ToArray(),
                    _ => throw new InvalidDataException($"Unsupported buffer value type {value.Value.GetType().Name}.")
                };
                if (bytes.Length > byte.MaxValue)
                    throw new InvalidDataException("Actor property buffer values cannot exceed 255 bytes.");

                destination[0] = (byte)bytes.Length;
                PacketBinary.WriteUInt32LittleEndian(destination[1..], value.PropertyId);
                bytes.CopyTo(destination[5..]);
                return 5 + bytes.Length;
            default:
                throw new ArgumentOutOfRangeException(nameof(value), $"Unsupported actor property value kind {value.Kind}.");
        }
    }

    private static ActorPropertyValue DecodeValue(uint propertyId, byte size, ReadOnlySpan<byte> value)
    {
        return size switch
        {
            1 => ActorPropertyValue.Byte(propertyId, value[0]),
            2 => ActorPropertyValue.UInt16(propertyId, PacketBinary.ReadUInt16LittleEndian(value)),
            4 => ActorPropertyValue.UInt32(propertyId, PacketBinary.ReadUInt32LittleEndian(value)),
            _ => ActorPropertyValue.Buffer(propertyId, value.ToArray())
        };
    }

    private static byte BuildTargetMarker(string target, bool isArrayMode, bool hasMore)
    {
        int length = Encoding.ASCII.GetByteCount(target);
        int marker = isArrayMode ? 0xA4 + length : hasMore ? 0x60 + length : 0x82 + length;
        if (marker > byte.MaxValue)
            throw new InvalidDataException($"Actor property target '{target}' is too long.");

        return (byte)marker;
    }

    private static bool IsTargetMarker(byte marker)
    {
        return marker >= 0x60;
    }
}

public static class ActorPropertyHash
{
    public static uint LegacyMurmurHash2(string key, uint seed = 0)
    {
        byte[] data = Encoding.ASCII.GetBytes(key);
        const uint m = 0x5BD1E995;
        const int r = 24;
        int len = key.Length;
        int dataIndex = len - 4;
        uint h = seed ^ (uint)len;

        while (len >= 4)
        {
            h *= m;

            uint k = (uint)BitConverter.ToInt32(data, dataIndex);
            k = ((k >> 24) & 0xFF) |
                ((k << 8) & 0xFF0000) |
                ((k >> 8) & 0xFF00) |
                ((k << 24) & 0xFF000000);

            k *= m;
            k ^= k >> r;
            k *= m;

            h ^= k;

            dataIndex -= 4;
            len -= 4;
        }

        switch (len)
        {
            case 3:
                h ^= (uint)data[0] << 16;
                goto case 2;
            case 2:
                h ^= (uint)data[len - 2] << 8;
                goto case 1;
            case 1:
                h ^= data[len - 1];
                h *= m;
                break;
        }

        h ^= h >> 13;
        h *= m;
        h ^= h >> 15;

        return h;
    }
}

internal static class ActorPacketCodecHelpers
{
    public static void EnsureOpcode(SubPacket packet, PacketOpcode opcode)
    {
        if (packet.Header.Opcode != opcode)
            throw new ArgumentException($"Expected opcode {opcode} but received {packet.Header.Opcode}.", nameof(packet));
    }

    public static ReadOnlySpan<byte> EnsurePayload(SubPacket packet, int minimumLength)
    {
        if (packet.Payload.Length < minimumLength)
            throw new InvalidDataException($"Actor packet payload ended before {minimumLength} bytes.");

        return packet.Payload.Span;
    }
}
