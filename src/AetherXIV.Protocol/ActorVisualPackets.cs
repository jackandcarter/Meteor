using static AetherXIV.Protocol.ActorPacketCodecHelpers;

namespace AetherXIV.Protocol;

public readonly record struct SetActorQuestGraphicPacket(uint GraphicId);

public sealed class SetActorQuestGraphicPacketCodec : IPacketCodec<SetActorQuestGraphicPacket>
{
    public PacketOpcode Opcode => PacketOpcode.SetActorQuestGraphic;

    public Type PacketType => typeof(SetActorQuestGraphicPacket);

    public SetActorQuestGraphicPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, 8);
        return new SetActorQuestGraphicPacket(PacketBinary.ReadUInt32LittleEndian(payload));
    }

    public SubPacket Encode(uint sourceActorId, SetActorQuestGraphicPacket packet)
    {
        byte[] payload = new byte[8];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.GraphicId);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct PlayAnimationOnActorPacket(uint AnimationId);

public sealed class PlayAnimationOnActorPacketCodec : IPacketCodec<PlayAnimationOnActorPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.PlayAnimationOnActor;

    public Type PacketType => typeof(PlayAnimationOnActorPacket);

    public PlayAnimationOnActorPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new PlayAnimationOnActorPacket(PacketBinary.ReadUInt32LittleEndian(payload));
    }

    public SubPacket Encode(uint sourceActorId, PlayAnimationOnActorPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.AnimationId);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public sealed record SetActorAppearancePacket(uint ModelId, IReadOnlyList<uint> AppearanceIds);

public sealed class SetActorAppearancePacketCodec : IPacketCodec<SetActorAppearancePacket>
{
    public const int AppearanceValueCount = 28;
    public const int PayloadSize = 0x128 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetActorAppearance;

    public Type PacketType => typeof(SetActorAppearancePacket);

    public SetActorAppearancePacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        uint modelId = PacketBinary.ReadUInt32LittleEndian(payload);
        int count = PacketBinary.ReadInt32LittleEndian(payload[0x100..]);
        if (count < 0 || count > AppearanceValueCount)
            throw new InvalidDataException($"Actor appearance count {count} is outside the legacy v1 packet limit.");

        uint[] appearanceIds = new uint[AppearanceValueCount];
        int offset = 4;
        for (int i = 0; i < count; i++)
        {
            uint index = PacketBinary.ReadUInt32LittleEndian(payload[offset..]);
            if (index >= AppearanceValueCount)
                throw new InvalidDataException($"Actor appearance index {index} is outside the legacy v1 packet limit.");

            appearanceIds[index] = PacketBinary.ReadUInt32LittleEndian(payload[(offset + 4)..]);
            offset += 8;
        }

        return new SetActorAppearancePacket(modelId, appearanceIds);
    }

    public SubPacket Encode(uint sourceActorId, SetActorAppearancePacket packet)
    {
        if (packet.AppearanceIds.Count != AppearanceValueCount)
            throw new InvalidDataException($"legacy v1 actor appearance packets require exactly {AppearanceValueCount} appearance values.");

        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.ModelId);
        int offset = 4;
        for (int i = 0; i < AppearanceValueCount; i++)
        {
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(offset), (uint)i);
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(offset + 4), packet.AppearanceIds[i]);
            offset += 8;
        }

        PacketBinary.WriteInt32LittleEndian(payload.AsSpan(0x100), AppearanceValueCount);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct SetActorBGPropertiesPacket(uint InstanceId, uint LayoutId);

public sealed class SetActorBGPropertiesPacketCodec : IPacketCodec<SetActorBGPropertiesPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetActorBGProperties;

    public Type PacketType => typeof(SetActorBGPropertiesPacket);

    public SetActorBGPropertiesPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new SetActorBGPropertiesPacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]));
    }

    public SubPacket Encode(uint sourceActorId, SetActorBGPropertiesPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.InstanceId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.LayoutId);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct SetActorSubStatePacket(
    byte Breakage,
    byte ChantId,
    byte Guard,
    byte Waste,
    byte Mode,
    ushort MotionPack);

public sealed class SetActorSubStatePacketCodec : IPacketCodec<SetActorSubStatePacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetActorSubState;

    public Type PacketType => typeof(SetActorSubStatePacket);

    public SetActorSubStatePacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new SetActorSubStatePacket(
            payload[0],
            payload[1],
            (byte)(payload[2] & 0xF),
            payload[3],
            payload[4],
            PacketBinary.ReadUInt16LittleEndian(payload[6..]));
    }

    public SubPacket Encode(uint sourceActorId, SetActorSubStatePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        payload[0] = packet.Breakage;
        payload[1] = packet.ChantId;
        payload[2] = (byte)(packet.Guard & 0xF);
        payload[3] = packet.Waste;
        payload[4] = packet.Mode;
        payload[5] = 0;
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(6), packet.MotionPack);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public sealed record SetActorStatusAllPacket(IReadOnlyList<ushort> StatusIds);

public sealed class SetActorStatusAllPacketCodec : IPacketCodec<SetActorStatusAllPacket>
{
    public const int StatusSlotCount = 20;
    public const int PayloadSize = 0x48 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetActorStatusAll;

    public Type PacketType => typeof(SetActorStatusAllPacket);

    public SetActorStatusAllPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        ushort[] statusIds = new ushort[StatusSlotCount];
        for (int i = 0; i < StatusSlotCount; i++)
            statusIds[i] = PacketBinary.ReadUInt16LittleEndian(payload[(i * 2)..]);

        return new SetActorStatusAllPacket(statusIds);
    }

    public SubPacket Encode(uint sourceActorId, SetActorStatusAllPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        int count = Math.Min(packet.StatusIds.Count, StatusSlotCount);
        for (int i = 0; i < count; i++)
            PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(i * 2), packet.StatusIds[i]);

        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct SetActorIconPacket(uint IconCode)
{
    public const uint Disconnecting = 0x00010000;
    public const uint IsGm = 0x00020000;
    public const uint IsAfk = 0x00000100;
}

public sealed class SetActorIconPacketCodec : IPacketCodec<SetActorIconPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetActorIcon;

    public Type PacketType => typeof(SetActorIconPacket);

    public SetActorIconPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new SetActorIconPacket(PacketBinary.ReadUInt32LittleEndian(payload));
    }

    public SubPacket Encode(uint sourceActorId, SetActorIconPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.IconCode);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}
