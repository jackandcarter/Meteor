using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class EventConditionPacketCodecTests
{
    [Fact]
    public void TalkConditionUsesLegacyOpcodeAndNormalizesUnknownByte()
    {
        SetTalkEventConditionPacketCodec codec = new();

        SubPacket encoded = codec.Encode(0x46800001, new TalkEventCondition("talkDefault", Unknown1: 99, IsDisabled: true));
        TalkEventCondition decoded = codec.Decode(encoded);

        Assert.Equal((ushort)0x012E, (ushort)encoded.Header.Opcode);
        Assert.Equal(SetTalkEventConditionPacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(4, encoded.Payload.Span[0]);
        Assert.Equal(1, encoded.Payload.Span[1]);
        Assert.Equal("talkDefault", decoded.ConditionName);
        Assert.Equal(4, decoded.Unknown1);
        Assert.True(decoded.IsDisabled);
    }

    [Fact]
    public void NoticeAndEmoteConditionsUseLegacyLayouts()
    {
        SetNoticeEventConditionPacketCodec noticeCodec = new();
        SetEmoteEventConditionPacketCodec emoteCodec = new();

        SubPacket notice = noticeCodec.Encode(0x46800001, new NoticeEventCondition("noticeEvent", 0x0E, 0));
        SubPacket emote = emoteCodec.Encode(0x46800001, new EmoteEventCondition("bowEvent", 4, 7, 0x52));

        Assert.Equal((ushort)0x016B, (ushort)notice.Header.Opcode);
        Assert.Equal(0x0E, notice.Payload.Span[0]);
        Assert.Equal(0, notice.Payload.Span[1]);
        Assert.Equal("noticeEvent", noticeCodec.Decode(notice).ConditionName);

        Assert.Equal((ushort)0x016C, (ushort)emote.Header.Opcode);
        Assert.Equal(4, emote.Payload.Span[0]);
        Assert.Equal(7, emote.Payload.Span[1]);
        Assert.Equal(0x52, PacketBinary.ReadUInt16LittleEndian(emote.Payload.Span[2..]));
        Assert.Equal((byte)'b', emote.Payload.Span[4]);
        EmoteEventCondition decodedEmote = emoteCodec.Decode(emote);
        Assert.Equal("bowEvent", decodedEmote.ConditionName);
        Assert.Equal(7, decodedEmote.Unknown2);
    }

    [Fact]
    public void PushConditionPacketsMatchLegacyConstantsAndOffsets()
    {
        SetPushEventConditionWithCirclePacketCodec circleCodec = new();
        SetPushEventConditionWithFanPacketCodec fanCodec = new();
        SetPushEventConditionWithTriggerBoxPacketCodec boxCodec = new();

        SubPacket circle = circleCodec.Encode(0x46800001, new PushCircleEventCondition("pushCircle", 12.5f, Outwards: true, Silent: false));
        SubPacket fan = fanCodec.Encode(0x46800001, new PushFanEventCondition("pushFan", 15.0f));
        SubPacket box = boxCodec.Encode(0x46800001, new PushBoxEventCondition("pushBox", "open", 0x11223344, 0x55667788));

        Assert.Equal((ushort)0x016F, (ushort)circle.Header.Opcode);
        Assert.Equal(12.5f, PacketBinary.ReadSingleLittleEndian(circle.Payload.Span));
        Assert.Equal(0x44533088u, PacketBinary.ReadUInt32LittleEndian(circle.Payload.Span[4..]));
        Assert.Equal(100.0f, PacketBinary.ReadSingleLittleEndian(circle.Payload.Span[8..]));
        Assert.Equal(0x11, circle.Payload.Span[16]);
        Assert.Equal(0, circle.Payload.Span[18]);
        Assert.Equal("pushCircle", circleCodec.Decode(circle).ConditionName);

        Assert.Equal((ushort)0x0170, (ushort)fan.Header.Opcode);
        Assert.Equal(0xBFC90FDBu, PacketBinary.ReadUInt32LittleEndian(fan.Payload.Span[4..]));
        Assert.Equal(0x3F860A92u, PacketBinary.ReadUInt32LittleEndian(fan.Payload.Span[8..]));
        Assert.Equal(0x46800001u, PacketBinary.ReadUInt32LittleEndian(fan.Payload.Span[12..]));
        Assert.Equal(10.0f, PacketBinary.ReadSingleLittleEndian(fan.Payload.Span[16..]));
        Assert.Equal("pushFan", fanCodec.Decode(fan).ConditionName);

        Assert.Equal((ushort)0x0175, (ushort)box.Header.Opcode);
        Assert.Equal(0x11223344u, PacketBinary.ReadUInt32LittleEndian(box.Payload.Span));
        Assert.Equal(0x55667788u, PacketBinary.ReadUInt32LittleEndian(box.Payload.Span[4..]));
        Assert.Equal(4u, PacketBinary.ReadUInt32LittleEndian(box.Payload.Span[8..]));
        Assert.Equal(3, box.Payload.Span[21]);
        Assert.Equal("pushBox", boxCodec.Decode(box).ConditionName);
        Assert.Equal("open", boxCodec.Decode(box).ReactName);
    }

    [Fact]
    public void PushCircleCodecPreservesTraceSpecificOpaqueFields()
    {
        SetPushEventConditionWithCirclePacketCodec codec = new();
        PushCircleEventCondition observed = new(
            "pushDefault",
            Radius: 6.0f,
            Outwards: false,
            Silent: false,
            IsDisabled: true,
            Unknown1: 0x4EA3ADB8,
            SecondaryRadius: 6.0f,
            Flags: 0,
            Unknown2: 3);

        SubPacket encoded = codec.Encode(0x44D8002E, observed);
        PushCircleEventCondition decoded = codec.Decode(encoded);

        Assert.Equal(0x4EA3ADB8u, decoded.Unknown1);
        Assert.Equal(6.0f, decoded.SecondaryRadius);
        Assert.Equal(0, decoded.Flags);
        Assert.Equal(3, decoded.Unknown2);
        Assert.False(decoded.Silent);
    }

    [Fact]
    public void MarketEntranceCircleUsesTheRuntimeActorAsItsOpaqueAnchor()
    {
        const uint actorId = 0x47480002;
        SetPushEventConditionWithCirclePacketCodec codec = new();
        PushCircleEventCondition condition = new(
            "pushDefault",
            Radius: 4.0f,
            Outwards: false,
            Silent: false,
            IsDisabled: false,
            SecondaryRadius: 10.0f,
            Flags: 1,
            Unknown2: 0,
            UseSourceActorId: true);

        SubPacket encoded = codec.Encode(actorId, condition);

        Assert.Equal(4.0f, PacketBinary.ReadSingleLittleEndian(encoded.Payload.Span));
        Assert.Equal(actorId, PacketBinary.ReadUInt32LittleEndian(encoded.Payload.Span[4..]));
        Assert.Equal(10.0f, PacketBinary.ReadSingleLittleEndian(encoded.Payload.Span[8..]));
        Assert.Equal(1, encoded.Payload.Span[16]);
        Assert.Equal(0, encoded.Payload.Span[17]);
        Assert.Equal(0, encoded.Payload.Span[18]);
        Assert.Equal("pushDefault", codec.Decode(encoded).ConditionName);
    }

    [Fact]
    public void OfficialGridaniaEntranceFrameConfirmsTriggerBoxFieldLayout()
    {
        SetPushEventConditionWithTriggerBoxPacketCodec codec = new();
        byte[] payload = Convert.FromHexString(
            "FA0C000041010000040000000000000000000000000300696E000000000000000000000000000000000000000000000000000000000000006474776900000000");

        PushBoxEventCondition observed = codec.Decode(SubPacket.Create(
            PacketOpcode.SetPushEventConditionWithTriggerBox,
            0x466F1A22,
            payload));

        Assert.Equal(3322u, observed.BgObj);
        Assert.Equal(321u, observed.Layout);
        Assert.Equal("in", observed.ConditionName);
        Assert.Equal("dtwi", observed.ReactName);
        Assert.False(observed.Outwards);
        Assert.False(observed.Silent);
    }

    [Fact]
    public void UldahCompanyOfficeEntranceUsesTheReviewedTriggerMetadata()
    {
        SetPushEventConditionWithTriggerBoxPacketCodec codec = new();
        PushBoxEventCondition expected = new("in", "dtwi", 4143, 421, Outwards: false, Silent: false);

        PushBoxEventCondition decoded = codec.Decode(codec.Encode(0x46800001, expected));

        Assert.Equal(expected, decoded);
    }

    [Fact]
    public void StatusSequenceHonorsPerConditionTraceDefaults()
    {
        EventConditionList conditions = new(
            [],
            [new NoticeEventCondition("noticeEvent", 0, 1, SendStatus: false)],
            [],
            [new PushCircleEventCondition("pushDefault", IsDisabled: true)],
            [],
            []);

        EventStatusPacket status = new SetEventStatusPacketCodec().Decode(
            Assert.Single(EventConditionPacketSequences.CreateStatusPackets(0x44D8002E, conditions)));

        Assert.Equal("pushDefault", status.ConditionName);
        Assert.Equal(2, status.Type);
        Assert.False(status.Enabled);
    }

    [Fact]
    public void StatusPacketsUseLegacyConditionTypeMapping()
    {
        EventConditionList conditions = new(
            [new TalkEventCondition("talk")],
            [new NoticeEventCondition("notice", 0, 1)],
            [new EmoteEventCondition("emote", 4, 0, 0x52)],
            [new PushCircleEventCondition("circle")],
            [new PushFanEventCondition("fan")],
            [new PushBoxEventCondition("box", "", 1, 2)]);

        IReadOnlyList<SubPacket> packets = EventConditionPacketSequences.CreateStatusPackets(0x46800001, conditions);
        SetEventStatusPacketCodec codec = new();
        EventStatusPacket[] decoded = packets.Select(codec.Decode).ToArray();

        Assert.Equal([1, 5, 3, 2, 2, 2], decoded.Select(packet => (int)packet.Type).ToArray());
        Assert.All(decoded, packet => Assert.True(packet.Enabled));
        Assert.All(packets, packet => Assert.Equal((ushort)0x0136, (ushort)packet.Header.Opcode));
    }
}
