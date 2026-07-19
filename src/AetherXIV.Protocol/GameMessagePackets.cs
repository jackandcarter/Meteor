namespace AetherXIV.Protocol;

public sealed record GameMessageWithActorPacket(
    uint MessageActorId,
    uint TextOwnerActorId,
    ushort TextId,
    byte LogType,
    IReadOnlyList<LuaParameter> Parameters);

public sealed class GameMessageWithActorPacketCodec
{
    private const int HeaderLength = 12;

    public SubPacket Encode(uint sourceActorId, GameMessageWithActorPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        byte[] parameterBytes = packet.Parameters.Count == 0
            ? []
            : LuaParameterCodec.Encode(packet.Parameters);
        (PacketOpcode opcode, int payloadLength) = SelectLayout(parameterBytes.Length);
        byte[] payload = new byte[payloadLength];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.MessageActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.TextOwnerActorId);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(8), packet.TextId);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(10), packet.LogType);
        if (parameterBytes.Length > 0)
        {
            if (HeaderLength + parameterBytes.Length > payload.Length)
                throw new InvalidDataException("Game message Lua parameters exceed the largest legacy packet layout.");

            parameterBytes.CopyTo(payload.AsSpan(HeaderLength));
            if (parameterBytes.Length <= 8)
                PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x14), 8);
        }

        return SubPacket.Create(opcode, sourceActorId, payload);
    }

    public GameMessageWithActorPacket Decode(SubPacket packet)
    {
        if (!IsSupported(packet.Header.Opcode))
            throw new ArgumentException($"Opcode {packet.Header.Opcode} is not a game-message-with-actor packet.", nameof(packet));

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        if (payload.Length < HeaderLength)
            throw new InvalidDataException("Game message payload is shorter than its fixed header.");

        IReadOnlyList<LuaParameter> parameters = packet.Header.Opcode == PacketOpcode.GameMessageWithActorX01
            ? []
            : LuaParameterCodec.Decode(payload[HeaderLength..]);
        return new GameMessageWithActorPacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            PacketBinary.ReadUInt16LittleEndian(payload[8..]),
            checked((byte)PacketBinary.ReadUInt16LittleEndian(payload[10..])),
            parameters);
    }

    public static bool IsSupported(PacketOpcode opcode) => opcode is
        PacketOpcode.GameMessageWithActorX01
        or PacketOpcode.GameMessageWithActorX02
        or PacketOpcode.GameMessageWithActorX03
        or PacketOpcode.GameMessageWithActorX04
        or PacketOpcode.GameMessageWithActorX05;

    private static (PacketOpcode Opcode, int PayloadLength) SelectLayout(int parameterLength)
    {
        if (parameterLength == 0)
            return (PacketOpcode.GameMessageWithActorX01, 0x10);
        if (parameterLength <= 0x08)
            return (PacketOpcode.GameMessageWithActorX02, 0x18);
        if (parameterLength <= 0x10)
            return (PacketOpcode.GameMessageWithActorX03, 0x20);
        if (parameterLength <= 0x20)
            return (PacketOpcode.GameMessageWithActorX04, 0x30);

        return (PacketOpcode.GameMessageWithActorX05, 0x50);
    }
}

public sealed record GameMessageWithoutActorPacket(
    uint TextOwnerActorId,
    ushort TextId,
    byte LogType,
    IReadOnlyList<LuaParameter> Parameters);

public sealed class GameMessageWithoutActorPacketCodec
{
    private const int HeaderLength = 8;

    public SubPacket Encode(uint sourceActorId, GameMessageWithoutActorPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        byte[] parameterBytes = packet.Parameters.Count == 0
            ? []
            : LuaParameterCodec.Encode(packet.Parameters);
        (PacketOpcode opcode, int payloadLength) = SelectLayout(parameterBytes.Length);
        byte[] payload = new byte[payloadLength];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.TextOwnerActorId);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(4), packet.TextId);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(6), packet.LogType);
        if (parameterBytes.Length > 0)
        {
            if (HeaderLength + parameterBytes.Length > payload.Length)
                throw new InvalidDataException("Game message Lua parameters exceed the largest legacy packet layout.");

            parameterBytes.CopyTo(payload.AsSpan(HeaderLength));
            if (parameterBytes.Length <= 8)
                PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x10), 8);
        }

        return SubPacket.Create(opcode, sourceActorId, payload);
    }

    public GameMessageWithoutActorPacket Decode(SubPacket packet)
    {
        if (!IsSupported(packet.Header.Opcode))
            throw new ArgumentException($"Opcode {packet.Header.Opcode} is not a game-message-without-actor packet.", nameof(packet));

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        if (payload.Length < HeaderLength)
            throw new InvalidDataException("Game message payload is shorter than its fixed header.");

        IReadOnlyList<LuaParameter> parameters = packet.Header.Opcode == PacketOpcode.GameMessageWithoutActorX01
            ? []
            : LuaParameterCodec.Decode(payload[HeaderLength..]);
        return new GameMessageWithoutActorPacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt16LittleEndian(payload[4..]),
            checked((byte)PacketBinary.ReadUInt16LittleEndian(payload[6..])),
            parameters);
    }

    public static bool IsSupported(PacketOpcode opcode) => opcode is
        PacketOpcode.GameMessageWithoutActorX01
        or PacketOpcode.GameMessageWithoutActorX02
        or PacketOpcode.GameMessageWithoutActorX03
        or PacketOpcode.GameMessageWithoutActorX04
        or PacketOpcode.GameMessageWithoutActorX05;

    private static (PacketOpcode Opcode, int PayloadLength) SelectLayout(int parameterLength)
    {
        if (parameterLength == 0)
            return (PacketOpcode.GameMessageWithoutActorX01, 0x08);
        if (parameterLength <= 0x08)
            return (PacketOpcode.GameMessageWithoutActorX02, 0x18);
        if (parameterLength <= 0x10)
            return (PacketOpcode.GameMessageWithoutActorX03, 0x18);
        if (parameterLength <= 0x20)
            return (PacketOpcode.GameMessageWithoutActorX04, 0x28);

        return (PacketOpcode.GameMessageWithoutActorX05, 0x48);
    }
}
