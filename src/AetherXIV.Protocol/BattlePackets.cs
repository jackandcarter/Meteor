using static AetherXIV.Protocol.ActorPacketCodecHelpers;

namespace AetherXIV.Protocol;

public readonly record struct CommandResultAction(
    uint TargetActorId,
    ushort Amount,
    ushort WorldMasterTextId,
    uint EffectId,
    byte Param,
    byte HitNumber);

public sealed record CommandResultX00Packet(
    uint ActorId,
    uint AnimationId,
    ushort CommandId,
    ushort LayoutFlags,
    ReadOnlyMemory<byte> RawPayload)
{
    public CommandResultX00Packet(
        uint actorId,
        uint animationId,
        ushort commandId,
        ushort layoutFlags)
        : this(actorId, animationId, commandId, layoutFlags, ReadOnlyMemory<byte>.Empty)
    {
    }
}

public sealed class CommandResultX00PacketCodec : IPacketCodec<CommandResultX00Packet>
{
    public const int PayloadSize = 0x48 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.CommandResultX00;

    public Type PacketType => typeof(CommandResultX00Packet);

    public CommandResultX00Packet Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        uint actionCount = PacketBinary.ReadUInt32LittleEndian(payload[0x20..]);
        if (actionCount != 0)
            throw new InvalidDataException($"CommandResultX00 expected 0 actions but payload declared {actionCount}.");

        return new CommandResultX00Packet(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            PacketBinary.ReadUInt16LittleEndian(payload[0x24..]),
            PacketBinary.ReadUInt16LittleEndian(payload[0x26..]),
            payload[..PayloadSize].ToArray());
    }

    public SubPacket Encode(uint sourceActorId, CommandResultX00Packet packet)
    {
        byte[] payload = packet.RawPayload.Length == PayloadSize
            ? packet.RawPayload.ToArray()
            : new byte[PayloadSize];

        PacketBinary.WriteUInt32LittleEndian(payload, packet.ActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.AnimationId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x20), 0);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x24), packet.CommandId);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x26), packet.LayoutFlags);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public sealed record CommandResultX01Packet(
    uint ActorId,
    uint AnimationId,
    ushort CommandId,
    ushort LayoutFlags,
    CommandResultAction Action,
    ReadOnlyMemory<byte> RawPayload)
{
    public CommandResultX01Packet(
        uint actorId,
        uint animationId,
        ushort commandId,
        ushort layoutFlags,
        CommandResultAction action)
        : this(actorId, animationId, commandId, layoutFlags, action, ReadOnlyMemory<byte>.Empty)
    {
    }
}

public sealed class CommandResultX01PacketCodec : IPacketCodec<CommandResultX01Packet>
{
    public const int PayloadSize = 0x58 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.CommandResultX01;

    public Type PacketType => typeof(CommandResultX01Packet);

    public CommandResultX01Packet Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        uint actionCount = PacketBinary.ReadUInt32LittleEndian(payload[0x20..]);
        if (actionCount != 1)
            throw new InvalidDataException($"CommandResultX01 expected 1 action but payload declared {actionCount}.");

        CommandResultAction action = new(
            PacketBinary.ReadUInt32LittleEndian(payload[0x28..]),
            PacketBinary.ReadUInt16LittleEndian(payload[0x2C..]),
            PacketBinary.ReadUInt16LittleEndian(payload[0x2E..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x30..]),
            payload[0x34],
            payload[0x35]);

        return new CommandResultX01Packet(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            PacketBinary.ReadUInt16LittleEndian(payload[0x24..]),
            PacketBinary.ReadUInt16LittleEndian(payload[0x26..]),
            action,
            payload[..PayloadSize].ToArray());
    }

    public SubPacket Encode(uint sourceActorId, CommandResultX01Packet packet)
    {
        byte[] payload = packet.RawPayload.Length == PayloadSize
            ? packet.RawPayload.ToArray()
            : new byte[PayloadSize];

        PacketBinary.WriteUInt32LittleEndian(payload, packet.ActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.AnimationId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x20), 1);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x24), packet.CommandId);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x26), packet.LayoutFlags);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x28), packet.Action.TargetActorId);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x2C), packet.Action.Amount);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x2E), packet.Action.WorldMasterTextId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x30), packet.Action.EffectId);
        payload[0x34] = packet.Action.Param;
        payload[0x35] = packet.Action.HitNumber;
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public sealed record CommandResultX10Packet(
    uint ActorId,
    uint AnimationId,
    ushort CommandId,
    ushort LayoutFlags,
    IReadOnlyList<CommandResultAction> Actions,
    ReadOnlyMemory<byte> RawPayload)
{
    public CommandResultX10Packet(
        uint actorId,
        uint animationId,
        ushort commandId,
        ushort layoutFlags,
        IReadOnlyList<CommandResultAction> actions)
        : this(actorId, animationId, commandId, layoutFlags, actions, ReadOnlyMemory<byte>.Empty)
    {
    }
}

public sealed class CommandResultX10PacketCodec : IPacketCodec<CommandResultX10Packet>
{
    public const int PayloadSize = 0xB4;
    public const int MaxActions = 10;

    public PacketOpcode Opcode => PacketOpcode.CommandResultX10;

    public Type PacketType => typeof(CommandResultX10Packet);

    public CommandResultX10Packet Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        uint actionCount = PacketBinary.ReadUInt32LittleEndian(payload[0x20..]);
        if (actionCount > MaxActions)
            throw new InvalidDataException($"CommandResultX10 supports up to {MaxActions} actions but payload declared {actionCount}.");

        List<CommandResultAction> actions = new((int)actionCount);
        for (int index = 0; index < actionCount; index++)
        {
            actions.Add(new CommandResultAction(
                PacketBinary.ReadUInt32LittleEndian(payload[(0x28 + index * 4)..]),
                PacketBinary.ReadUInt16LittleEndian(payload[(0x4C + index * 2)..]),
                PacketBinary.ReadUInt16LittleEndian(payload[(0x60 + index * 2)..]),
                PacketBinary.ReadUInt32LittleEndian(payload[(0x74 + index * 4)..]),
                payload[0x9C + index],
                payload[0xA6 + index]));
        }

        return new CommandResultX10Packet(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            PacketBinary.ReadUInt16LittleEndian(payload[0x24..]),
            PacketBinary.ReadUInt16LittleEndian(payload[0x26..]),
            actions,
            payload[..PayloadSize].ToArray());
    }

    public SubPacket Encode(uint sourceActorId, CommandResultX10Packet packet)
    {
        if (packet.Actions.Count > MaxActions)
            throw new InvalidDataException($"CommandResultX10 supports up to {MaxActions} actions.");

        byte[] payload = packet.RawPayload.Length == PayloadSize
            ? packet.RawPayload.ToArray()
            : new byte[PayloadSize];

        PacketBinary.WriteUInt32LittleEndian(payload, packet.ActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.AnimationId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x20), checked((uint)packet.Actions.Count));
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x24), packet.CommandId);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x26), packet.LayoutFlags);

        for (int index = 0; index < packet.Actions.Count; index++)
        {
            CommandResultAction action = packet.Actions[index];
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x28 + index * 4), action.TargetActorId);
            PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x4C + index * 2), action.Amount);
            PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x60 + index * 2), action.WorldMasterTextId);
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x74 + index * 4), action.EffectId);
            payload[0x9C + index] = action.Param;
            payload[0xA6 + index] = action.HitNumber;
        }

        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public sealed record CommandStateRowPacket(
    ushort PrimaryId,
    ushort SecondaryId,
    uint Unknown04,
    uint Unknown08,
    uint Unknown0C,
    uint Unknown10,
    uint Unknown14,
    uint Unknown18,
    ReadOnlyMemory<byte> RawPayload)
{
    public CommandStateRowPacket(
        ushort primaryId,
        ushort secondaryId,
        uint unknown04,
        uint unknown08,
        uint unknown0C,
        uint unknown10,
        uint unknown14,
        uint unknown18)
        : this(primaryId, secondaryId, unknown04, unknown08, unknown0C, unknown10, unknown14, unknown18, ReadOnlyMemory<byte>.Empty)
    {
    }
}

public sealed class CommandStateRowPacketCodec : IPacketCodec<CommandStateRowPacket>
{
    public const int PayloadSize = 0x88 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.CommandStateRow;

    public Type PacketType => typeof(CommandStateRowPacket);

    public CommandStateRowPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new CommandStateRowPacket(
            PacketBinary.ReadUInt16LittleEndian(payload),
            PacketBinary.ReadUInt16LittleEndian(payload[2..]),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            PacketBinary.ReadUInt32LittleEndian(payload[8..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x0C..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x10..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x14..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x18..]),
            payload[..PayloadSize].ToArray());
    }

    public SubPacket Encode(uint sourceActorId, CommandStateRowPacket packet)
    {
        byte[] payload = packet.RawPayload.Length == PayloadSize
            ? packet.RawPayload.ToArray()
            : new byte[PayloadSize];

        PacketBinary.WriteUInt16LittleEndian(payload, packet.PrimaryId);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(2), packet.SecondaryId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.Unknown04);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(8), packet.Unknown08);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x0C), packet.Unknown0C);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x10), packet.Unknown10);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x14), packet.Unknown14);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x18), packet.Unknown18);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}
