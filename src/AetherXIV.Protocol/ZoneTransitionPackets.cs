namespace AetherXIV.Protocol;

public readonly record struct ServerZoneInstanceBeginPacket;

public sealed class ServerZoneInstanceBeginPacketCodec : IPacketCodec<ServerZoneInstanceBeginPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.ServerZoneInstanceBegin;

    public Type PacketType => typeof(ServerZoneInstanceBeginPacket);

    public ServerZoneInstanceBeginPacket Decode(SubPacket packet)
    {
        EnsurePacket(packet, Opcode, PayloadSize, "zone-instance begin");
        return new ServerZoneInstanceBeginPacket();
    }

    public SubPacket Encode(uint sourceActorId, ServerZoneInstanceBeginPacket packet) =>
        SubPacket.Create(Opcode, sourceActorId, new byte[PayloadSize]);

    private static void EnsurePacket(SubPacket packet, PacketOpcode opcode, int payloadSize, string name)
    {
        if (packet.Header.Opcode != opcode)
            throw new InvalidDataException($"Expected {name} opcode 0x{(ushort)opcode:X4}.");
        if (packet.Payload.Length != payloadSize)
            throw new InvalidDataException($"{name} payload must be {payloadSize} bytes, got {packet.Payload.Length}.");
    }
}

public sealed record ServerZoneInstanceActorsPacket(IReadOnlyList<uint> ActorIds);

public sealed class ServerZoneInstanceActorsPacketCodec : IPacketCodec<ServerZoneInstanceActorsPacket>
{
    public const int MaximumActors = 8;
    public const int PayloadSize = 0x50 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.ServerZoneInstanceActors;

    public Type PacketType => typeof(ServerZoneInstanceActorsPacket);

    public ServerZoneInstanceActorsPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected zone-instance actor opcode 0x{(ushort)Opcode:X4}.");
        if (packet.Payload.Length != PayloadSize)
            throw new InvalidDataException($"Zone-instance actor payload must be {PayloadSize} bytes, got {packet.Payload.Length}.");

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        uint count = PacketBinary.ReadUInt32LittleEndian(payload);
        if (count > MaximumActors)
            throw new InvalidDataException($"Zone-instance actor count {count} exceeds {MaximumActors}.");

        uint[] actorIds = new uint[count];
        for (int index = 0; index < actorIds.Length; index++)
            actorIds[index] = PacketBinary.ReadUInt32LittleEndian(payload[(4 + index * sizeof(uint))..]);
        return new ServerZoneInstanceActorsPacket(actorIds);
    }

    public SubPacket Encode(uint sourceActorId, ServerZoneInstanceActorsPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.ActorIds.Count > MaximumActors)
            throw new ArgumentOutOfRangeException(nameof(packet), $"At most {MaximumActors} actor IDs fit in one packet.");

        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, checked((uint)packet.ActorIds.Count));
        for (int index = 0; index < packet.ActorIds.Count; index++)
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4 + index * sizeof(uint)), packet.ActorIds[index]);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct ServerZoneInstanceEndPacket;

public sealed class ServerZoneInstanceEndPacketCodec : IPacketCodec<ServerZoneInstanceEndPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.ServerZoneInstanceEnd;

    public Type PacketType => typeof(ServerZoneInstanceEndPacket);

    public ServerZoneInstanceEndPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected zone-instance end opcode 0x{(ushort)Opcode:X4}.");
        if (packet.Payload.Length != PayloadSize)
            throw new InvalidDataException($"Zone-instance end payload must be {PayloadSize} bytes, got {packet.Payload.Length}.");
        return new ServerZoneInstanceEndPacket();
    }

    public SubPacket Encode(uint sourceActorId, ServerZoneInstanceEndPacket packet) =>
        SubPacket.Create(Opcode, sourceActorId, new byte[PayloadSize]);
}

public readonly record struct DeleteAllActorsPacket;

public sealed class DeleteAllActorsPacketCodec : IPacketCodec<DeleteAllActorsPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.DeleteAllActors;

    public Type PacketType => typeof(DeleteAllActorsPacket);

    public DeleteAllActorsPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected opcode 0x{(ushort)Opcode:X4}, got 0x{(ushort)packet.Header.Opcode:X4}.");
        if (packet.Payload.Length != PayloadSize)
            throw new InvalidDataException($"Delete-all-actors payload must be {PayloadSize} bytes, got {packet.Payload.Length}.");
        return new DeleteAllActorsPacket();
    }

    public SubPacket Encode(uint sourceActorId, DeleteAllActorsPacket packet) =>
        SubPacket.Create(Opcode, sourceActorId, new byte[PayloadSize]);
}

public readonly record struct ZoneTransitionStatePacket(byte State);

public sealed class ZoneTransitionStatePacketCodec : IPacketCodec<ZoneTransitionStatePacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.ZoneTransitionState;

    public Type PacketType => typeof(ZoneTransitionStatePacket);

    public ZoneTransitionStatePacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected opcode 0x{(ushort)Opcode:X4}, got 0x{(ushort)packet.Header.Opcode:X4}.");
        if (packet.Payload.Length != PayloadSize)
            throw new InvalidDataException($"Zone-transition payload must be {PayloadSize} bytes, got {packet.Payload.Length}.");
        return new ZoneTransitionStatePacket(packet.Payload.Span[0]);
    }

    public SubPacket Encode(uint sourceActorId, ZoneTransitionStatePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        payload[0] = packet.State;
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}
