namespace AetherXIV.Protocol;

public readonly record struct GrandCompanyStatePacket(
    byte CurrentAllegiance,
    byte LimsaRank,
    byte GridaniaRank,
    byte UldahRank);

public sealed class GrandCompanyStatePacketCodec : IPacketCodec<GrandCompanyStatePacket>
{
    public const int PayloadSize = 0x08;

    public PacketOpcode Opcode => PacketOpcode.GrandCompanyState;

    public Type PacketType => typeof(GrandCompanyStatePacket);

    public GrandCompanyStatePacket Decode(SubPacket packet)
    {
        ReadOnlySpan<byte> payload = PlayerStatePacketBinary.EnsurePayload(packet, Opcode, PayloadSize);
        return new GrandCompanyStatePacket(payload[0], payload[1], payload[2], payload[3]);
    }

    public SubPacket Encode(uint sourceActorId, GrandCompanyStatePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        payload[0] = packet.CurrentAllegiance;
        payload[1] = packet.LimsaRank;
        payload[2] = packet.GridaniaRank;
        payload[3] = packet.UldahRank;
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct PlayerTitleStatePacket(ulong TitleId);

public sealed class PlayerTitleStatePacketCodec : IPacketCodec<PlayerTitleStatePacket>
{
    public const int PayloadSize = 0x08;

    public PacketOpcode Opcode => PacketOpcode.PlayerTitleState;

    public Type PacketType => typeof(PlayerTitleStatePacket);

    public PlayerTitleStatePacket Decode(SubPacket packet)
    {
        ReadOnlySpan<byte> payload = PlayerStatePacketBinary.EnsurePayload(packet, Opcode, PayloadSize);
        return new PlayerTitleStatePacket(PacketBinary.ReadUInt64LittleEndian(payload));
    }

    public SubPacket Encode(uint sourceActorId, PlayerTitleStatePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.TitleId);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct CurrentJobStatePacket(uint JobId);

public sealed class CurrentJobStatePacketCodec : IPacketCodec<CurrentJobStatePacket>
{
    public const int PayloadSize = 0x08;

    public PacketOpcode Opcode => PacketOpcode.CurrentJobState;

    public Type PacketType => typeof(CurrentJobStatePacket);

    public CurrentJobStatePacket Decode(SubPacket packet)
    {
        ReadOnlySpan<byte> payload = PlayerStatePacketBinary.EnsurePayload(packet, Opcode, PayloadSize);
        return new CurrentJobStatePacket(PacketBinary.ReadUInt32LittleEndian(payload));
    }

    public SubPacket Encode(uint sourceActorId, CurrentJobStatePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.JobId);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct SpecialEventWorkStatePacket(ReadOnlyMemory<byte> WorkData)
{
    public static SpecialEventWorkStatePacket Empty { get; } = new(new byte[SpecialEventWorkStatePacketCodec.PayloadSize]);
}

public sealed class SpecialEventWorkStatePacketCodec : IPacketCodec<SpecialEventWorkStatePacket>
{
    public const int PayloadSize = 0x18;

    public PacketOpcode Opcode => PacketOpcode.SpecialEventWorkState;

    public Type PacketType => typeof(SpecialEventWorkStatePacket);

    public SpecialEventWorkStatePacket Decode(SubPacket packet)
    {
        ReadOnlySpan<byte> payload = PlayerStatePacketBinary.EnsurePayload(packet, Opcode, PayloadSize);
        return new SpecialEventWorkStatePacket(payload.ToArray());
    }

    public SubPacket Encode(uint sourceActorId, SpecialEventWorkStatePacket packet)
    {
        if (packet.WorkData.Length != PayloadSize)
            throw new ArgumentOutOfRangeException(nameof(packet), $"Special-event work requires {PayloadSize} bytes.");
        return SubPacket.Create(Opcode, sourceActorId, packet.WorkData.ToArray());
    }
}

public readonly record struct CompletedAchievementsStatePacket(ReadOnlyMemory<byte> Flags)
{
    public static CompletedAchievementsStatePacket Empty { get; } = new(new byte[CompletedAchievementsStatePacketCodec.PayloadSize]);
}

public sealed class CompletedAchievementsStatePacketCodec : IPacketCodec<CompletedAchievementsStatePacket>
{
    public const int PayloadSize = 0x80;

    public PacketOpcode Opcode => PacketOpcode.CompletedAchievementsState;

    public Type PacketType => typeof(CompletedAchievementsStatePacket);

    public CompletedAchievementsStatePacket Decode(SubPacket packet)
    {
        ReadOnlySpan<byte> payload = PlayerStatePacketBinary.EnsurePayload(packet, Opcode, PayloadSize);
        return new CompletedAchievementsStatePacket(payload.ToArray());
    }

    public SubPacket Encode(uint sourceActorId, CompletedAchievementsStatePacket packet)
    {
        if (packet.Flags.Length != PayloadSize)
            throw new ArgumentOutOfRangeException(nameof(packet), $"Completed-achievement state requires {PayloadSize} bytes.");
        return SubPacket.Create(Opcode, sourceActorId, packet.Flags.ToArray());
    }
}

public readonly record struct LatestAchievementsStatePacket(IReadOnlyList<uint> AchievementIds);

public sealed class LatestAchievementsStatePacketCodec : IPacketCodec<LatestAchievementsStatePacket>
{
    public const int PayloadSize = 0x20;
    public const int MaximumEntries = PayloadSize / sizeof(uint);

    public PacketOpcode Opcode => PacketOpcode.LatestAchievementsState;

    public Type PacketType => typeof(LatestAchievementsStatePacket);

    public LatestAchievementsStatePacket Decode(SubPacket packet)
    {
        ReadOnlySpan<byte> payload = PlayerStatePacketBinary.EnsurePayload(packet, Opcode, PayloadSize);
        uint[] ids = new uint[MaximumEntries];
        for (int index = 0; index < ids.Length; index++)
            ids[index] = PacketBinary.ReadUInt32LittleEndian(payload[(index * sizeof(uint))..]);
        return new LatestAchievementsStatePacket(ids);
    }

    public SubPacket Encode(uint sourceActorId, LatestAchievementsStatePacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet.AchievementIds);
        if (packet.AchievementIds.Count > MaximumEntries)
            throw new ArgumentOutOfRangeException(nameof(packet), $"Latest-achievement state supports at most {MaximumEntries} entries.");

        byte[] payload = new byte[PayloadSize];
        for (int index = 0; index < packet.AchievementIds.Count; index++)
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(index * sizeof(uint)), packet.AchievementIds[index]);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct AchievementPointsStatePacket(ulong Points);

public sealed class AchievementPointsStatePacketCodec : IPacketCodec<AchievementPointsStatePacket>
{
    public const int PayloadSize = 0x08;

    public PacketOpcode Opcode => PacketOpcode.AchievementPointsState;

    public Type PacketType => typeof(AchievementPointsStatePacket);

    public AchievementPointsStatePacket Decode(SubPacket packet)
    {
        ReadOnlySpan<byte> payload = PlayerStatePacketBinary.EnsurePayload(packet, Opcode, PayloadSize);
        return new AchievementPointsStatePacket(PacketBinary.ReadUInt64LittleEndian(payload));
    }

    public SubPacket Encode(uint sourceActorId, AchievementPointsStatePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Points);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

internal static class PlayerStatePacketBinary
{
    public static ReadOnlySpan<byte> EnsurePayload(SubPacket packet, PacketOpcode opcode, int expectedLength)
    {
        if (packet.Header.Opcode != opcode)
            throw new InvalidDataException($"Expected opcode 0x{(ushort)opcode:X4} but received 0x{(ushort)packet.Header.Opcode:X4}.");
        if (packet.Payload.Length != expectedLength)
            throw new InvalidDataException($"Opcode 0x{(ushort)opcode:X4} requires {expectedLength} payload bytes.");
        return packet.Payload.Span;
    }
}
