using static AetherXIV.Protocol.EventConditionPacketCodecHelpers;

namespace AetherXIV.Protocol;

public sealed record EventConditionList(
    IReadOnlyList<TalkEventCondition> Talk,
    IReadOnlyList<NoticeEventCondition> Notice,
    IReadOnlyList<EmoteEventCondition> Emote,
    IReadOnlyList<PushCircleEventCondition> PushCircle,
    IReadOnlyList<PushFanEventCondition> PushFan,
    IReadOnlyList<PushBoxEventCondition> PushBox)
{
    public static EventConditionList Empty { get; } = new([], [], [], [], [], []);

    public bool HasAny =>
        Talk.Count > 0 ||
        Notice.Count > 0 ||
        Emote.Count > 0 ||
        PushCircle.Count > 0 ||
        PushFan.Count > 0 ||
        PushBox.Count > 0;
}

public sealed record TalkEventCondition(string ConditionName, byte Unknown1 = 4, bool IsDisabled = false);

public sealed record NoticeEventCondition(
    string ConditionName,
    byte Unknown1,
    byte Unknown2,
    bool SendStatus = true);

public sealed record EmoteEventCondition(string ConditionName, byte Unknown1, byte Unknown2, byte EmoteId);

public sealed record PushCircleEventCondition(
    string ConditionName,
    float Radius = 30.0f,
    bool Outwards = false,
    bool Silent = true,
    bool IsDisabled = false,
    uint Unknown1 = 0x44533088,
    float SecondaryRadius = 100.0f,
    byte Flags = 0x01,
    byte Unknown2 = 0,
    bool UseSourceActorId = false);

public sealed record PushFanEventCondition(
    string ConditionName,
    float Radius = 30.0f,
    bool Outwards = false,
    bool Silent = true);

public sealed record PushBoxEventCondition(
    string ConditionName,
    string ReactName,
    uint BgObj,
    uint Layout,
    bool Outwards = false,
    bool Silent = true);

public readonly record struct EventStatusPacket(bool Enabled, byte Type, string ConditionName);

public static class EventConditionPacketSequences
{
    public static IReadOnlyList<SubPacket> CreateDefinitionPackets(uint sourceActorId, EventConditionList conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        List<SubPacket> packets = new();
        SetTalkEventConditionPacketCodec talkCodec = new();
        SetNoticeEventConditionPacketCodec noticeCodec = new();
        SetEmoteEventConditionPacketCodec emoteCodec = new();
        SetPushEventConditionWithCirclePacketCodec pushCircleCodec = new();
        SetPushEventConditionWithFanPacketCodec pushFanCodec = new();
        SetPushEventConditionWithTriggerBoxPacketCodec pushBoxCodec = new();

        foreach (TalkEventCondition condition in conditions.Talk)
            packets.Add(talkCodec.Encode(sourceActorId, condition));

        foreach (NoticeEventCondition condition in conditions.Notice)
            packets.Add(noticeCodec.Encode(sourceActorId, condition));

        foreach (EmoteEventCondition condition in conditions.Emote)
            packets.Add(emoteCodec.Encode(sourceActorId, condition));

        foreach (PushCircleEventCondition condition in conditions.PushCircle)
            packets.Add(pushCircleCodec.Encode(sourceActorId, condition));

        foreach (PushFanEventCondition condition in conditions.PushFan)
            packets.Add(pushFanCodec.Encode(sourceActorId, condition));

        foreach (PushBoxEventCondition condition in conditions.PushBox)
            packets.Add(pushBoxCodec.Encode(sourceActorId, condition));

        return packets;
    }

    public static IReadOnlyList<SubPacket> CreateStatusPackets(uint sourceActorId, EventConditionList conditions, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        List<SubPacket> packets = new();
        SetEventStatusPacketCodec codec = new();

        foreach (TalkEventCondition condition in conditions.Talk)
            packets.Add(codec.Encode(sourceActorId, new EventStatusPacket(enabled, 1, condition.ConditionName)));

        foreach (NoticeEventCondition condition in conditions.Notice)
        {
            if (condition.SendStatus)
                packets.Add(codec.Encode(sourceActorId, new EventStatusPacket(enabled, 5, condition.ConditionName)));
        }

        foreach (EmoteEventCondition condition in conditions.Emote)
            packets.Add(codec.Encode(sourceActorId, new EventStatusPacket(enabled, 3, condition.ConditionName)));

        foreach (PushCircleEventCondition condition in conditions.PushCircle)
            packets.Add(codec.Encode(sourceActorId, new EventStatusPacket(enabled && !condition.IsDisabled, 2, condition.ConditionName)));

        foreach (PushFanEventCondition condition in conditions.PushFan)
            packets.Add(codec.Encode(sourceActorId, new EventStatusPacket(enabled, 2, condition.ConditionName)));

        foreach (PushBoxEventCondition condition in conditions.PushBox)
            packets.Add(codec.Encode(sourceActorId, new EventStatusPacket(enabled, 2, condition.ConditionName)));

        return packets;
    }
}

public sealed class SetTalkEventConditionPacketCodec : IPacketCodec<TalkEventCondition>
{
    public const int PayloadSize = 0x48 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetTalkEventCondition;

    public Type PacketType => typeof(TalkEventCondition);

    public TalkEventCondition Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new TalkEventCondition(
            EventStartPacketCodec.ReadFixedString(payload[2..], 0x24),
            payload[0],
            payload[1] != 0);
    }

    public SubPacket Encode(uint sourceActorId, TalkEventCondition packet)
    {
        byte[] payload = new byte[PayloadSize];
        payload[0] = 4;
        payload[1] = packet.IsDisabled ? (byte)1 : (byte)0;
        WriteFixedString(payload.AsSpan(2), 0x24, packet.ConditionName);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public sealed class SetNoticeEventConditionPacketCodec : IPacketCodec<NoticeEventCondition>
{
    public const int PayloadSize = 0x48 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetNoticeEventCondition;

    public Type PacketType => typeof(NoticeEventCondition);

    public NoticeEventCondition Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new NoticeEventCondition(
            EventStartPacketCodec.ReadFixedString(payload[2..], 0x24),
            payload[0],
            payload[1]);
    }

    public SubPacket Encode(uint sourceActorId, NoticeEventCondition packet)
    {
        byte[] payload = new byte[PayloadSize];
        payload[0] = packet.Unknown1;
        payload[1] = packet.Unknown2;
        WriteFixedString(payload.AsSpan(2), 0x24, packet.ConditionName);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public sealed class SetEmoteEventConditionPacketCodec : IPacketCodec<EmoteEventCondition>
{
    public const int PayloadSize = 0x48 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetEmoteEventCondition;

    public Type PacketType => typeof(EmoteEventCondition);

    public EmoteEventCondition Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new EmoteEventCondition(
            EventStartPacketCodec.ReadFixedString(payload[3..], 0x24),
            payload[0],
            0,
            (byte)PacketBinary.ReadUInt16LittleEndian(payload[1..]));
    }

    public SubPacket Encode(uint sourceActorId, EmoteEventCondition packet)
    {
        byte[] payload = new byte[PayloadSize];
        payload[0] = packet.Unknown1;
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(1), packet.EmoteId);
        WriteFixedString(payload.AsSpan(3), 0x24, packet.ConditionName);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public sealed class SetPushEventConditionWithCirclePacketCodec : IPacketCodec<PushCircleEventCondition>
{
    public const int PayloadSize = 0x58 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetPushEventConditionWithCircle;

    public Type PacketType => typeof(PushCircleEventCondition);

    public PushCircleEventCondition Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new PushCircleEventCondition(
            EventStartPacketCodec.ReadFixedString(payload[19..], 0x24),
            PacketBinary.ReadSingleLittleEndian(payload),
            (payload[16] & 0x10) != 0,
            payload[18] != 0,
            false,
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            PacketBinary.ReadSingleLittleEndian(payload[8..]),
            (byte)(payload[16] & ~0x10),
            payload[17]);
    }

    public SubPacket Encode(uint sourceActorId, PushCircleEventCondition packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteSingleLittleEndian(payload, packet.Radius);
        PacketBinary.WriteUInt32LittleEndian(
            payload.AsSpan(4),
            packet.UseSourceActorId ? sourceActorId : packet.Unknown1);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(8), packet.SecondaryRadius);
        payload[16] = (byte)(packet.Flags | (packet.Outwards ? 0x10 : 0x00));
        payload[17] = packet.Unknown2;
        payload[18] = packet.Silent ? (byte)1 : (byte)0;
        WriteFixedString(payload.AsSpan(19), 0x24, packet.ConditionName);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public sealed class SetPushEventConditionWithFanPacketCodec : IPacketCodec<PushFanEventCondition>
{
    public const int PayloadSize = 0x60 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetPushEventConditionWithFan;

    public Type PacketType => typeof(PushFanEventCondition);

    public PushFanEventCondition Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new PushFanEventCondition(
            EventStartPacketCodec.ReadFixedString(payload[27..], 0x24),
            PacketBinary.ReadSingleLittleEndian(payload),
            (payload[24] & 0x10) != 0,
            payload[26] != 0);
    }

    public SubPacket Encode(uint sourceActorId, PushFanEventCondition packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteSingleLittleEndian(payload, packet.Radius);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), 0xBFC90FDB);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(8), 0x3F860A92);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(12), sourceActorId);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(16), 10.0f);
        payload[24] = packet.Outwards ? (byte)0x11 : (byte)0x01;
        payload[25] = 0;
        payload[26] = packet.Silent ? (byte)1 : (byte)0;
        WriteFixedString(payload.AsSpan(27), 0x24, packet.ConditionName);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public sealed class SetPushEventConditionWithTriggerBoxPacketCodec : IPacketCodec<PushBoxEventCondition>
{
    public const int PayloadSize = 0x60 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetPushEventConditionWithTriggerBox;

    public Type PacketType => typeof(PushBoxEventCondition);

    public PushBoxEventCondition Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new PushBoxEventCondition(
            EventStartPacketCodec.ReadFixedString(payload[23..], 0x20),
            EventStartPacketCodec.ReadFixedString(payload[56..], 0x04),
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            (payload[20] & 0x10) != 0,
            payload[22] != 0);
    }

    public SubPacket Encode(uint sourceActorId, PushBoxEventCondition packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.BgObj);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.Layout);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(8), 4);
        payload[20] = packet.Outwards ? (byte)0x11 : (byte)0x00;
        payload[21] = 3;
        payload[22] = packet.Silent ? (byte)1 : (byte)0;
        WriteFixedString(payload.AsSpan(23), 0x20, packet.ConditionName);
        payload[55] = 0;
        WriteFixedString(payload.AsSpan(56), 0x04, packet.ReactName);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public sealed class SetEventStatusPacketCodec : IPacketCodec<EventStatusPacket>
{
    public const int PayloadSize = 0x48 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.SetEventStatus;

    public Type PacketType => typeof(EventStatusPacket);

    public EventStatusPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = EnsurePayload(packet, PayloadSize);
        return new EventStatusPacket(
            PacketBinary.ReadUInt32LittleEndian(payload) != 0,
            payload[4],
            EventStartPacketCodec.ReadFixedString(payload[5..], 0x23));
    }

    public SubPacket Encode(uint sourceActorId, EventStatusPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.Enabled ? 1u : 0u);
        payload[4] = packet.Type;
        WriteFixedString(payload.AsSpan(5), 0x23, packet.ConditionName);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

internal static class EventConditionPacketCodecHelpers
{
    public static void EnsureOpcode(SubPacket packet, PacketOpcode opcode)
    {
        if (packet.Header.Opcode != opcode)
            throw new ArgumentException($"Expected opcode {opcode} but received {packet.Header.Opcode}.", nameof(packet));
    }

    public static ReadOnlySpan<byte> EnsurePayload(SubPacket packet, int minimumLength)
    {
        if (packet.Payload.Length < minimumLength)
            throw new InvalidDataException($"Event condition payload ended before {minimumLength} bytes.");

        return packet.Payload.Span;
    }

    public static void WriteFixedString(Span<byte> payload, int length, string value)
    {
        EventStartPacketCodec.WriteFixedString(payload, Math.Min(length, payload.Length), value);
    }
}
