using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class EventPacketCodecTests
{
    [Fact]
    public void EventStartPacketRoundTripsLegacyLayout()
    {
        EventStartPacketCodec codec = new();
        EventStartPacket packet = new(
            0x10001,
            0x20002,
            0x30400000,
            0,
            5,
            "noticeEvent",
            [
                new LuaParameter(LuaParameterType.String, "choice"),
                new LuaParameter(LuaParameterType.Int32, 7)
            ]);

        SubPacket encoded = codec.Encode(packet.TriggerActorId, packet);
        EventStartPacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.EventStart, encoded.Header.Opcode);
        Assert.Equal(EventStartPacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(0x02, encoded.Payload.Span[0x1D]);
        Assert.Equal(0x00, encoded.Payload.Span[0x24]);
        Assert.Equal(0x07, encoded.Payload.Span[0x29]);
        Assert.Equal(packet.TriggerActorId, decoded.TriggerActorId);
        Assert.Equal(packet.OwnerActorId, decoded.OwnerActorId);
        Assert.Equal(packet.EventName, decoded.EventName);
        Assert.Equal("choice", decoded.Parameters[0].Value);
        Assert.Equal(7, decoded.Parameters[1].Value);
    }

    [Fact]
    public void OfficialCombatCommandEnvelopeDecodesVariableNameAndTypedTarget()
    {
        byte[] payload = Convert.FromHexString(
            "41299B027C6AF0A0000080261B9625E200636F6D6D616E6444656661756C740005071854050718680507187C05071890050500000000000000000001050644D035D5050505050F0000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000");
        SubPacket packet = SubPacket.Create(PacketOpcode.EventStart, 0x029B2941, payload);

        EventStartPacket decoded = new EventStartPacketCodec().Decode(packet);
        bool recognized = new ClientBattleCommandRequestCodec().TryDecode(packet, out ClientBattleCommandRequest? request);

        Assert.Equal("commandDefault", decoded.EventName);
        Assert.Equal(12, decoded.Parameters.Count);
        Assert.Equal(new LuaItemReference(0x18540507, 0x18, 0x68, 0x05), decoded.Parameters[1].Value);
        Assert.Equal(0x44D035D5u, decoded.Parameters[7].Value);
        Assert.True(recognized);
        Assert.NotNull(request);
        Assert.Equal(ClientBattleCommandRequestKind.Default, request.Kind);
        Assert.Equal(0x6A7C, request.CommandId);
        Assert.Equal(0x44D035D5u, request.TargetActorId);
    }

    [Fact]
    public void ClientScriptErrorEnvelopeDecodesWithoutBecomingAnEvent()
    {
        byte[] payload = new byte[EventStartPacketCodec.PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, 1);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), 3);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(8), 0x30400000);
        payload[16] = EventStartPacket.ClientScriptErrorEventType;
        System.Text.Encoding.ASCII.GetBytes("attempt to call missing client function", payload.AsSpan(0x31));

        EventStartPacket decoded = new EventStartPacketCodec().Decode(
            SubPacket.Create(PacketOpcode.EventStart, 0x10001, payload));

        Assert.True(decoded.IsClientScriptError);
        Assert.Equal(1u, decoded.ClientScriptErrorIndex);
        Assert.Equal(3u, decoded.ClientScriptErrorCount);
        Assert.Equal("attempt to call missing client function", decoded.ClientScriptErrorText);
        Assert.Equal(string.Empty, decoded.EventName);
        Assert.Empty(decoded.Parameters);
    }

    [Fact]
    public void EventUpdatePacketDecodesClientReplyParameters()
    {
        EventUpdatePacketCodec codec = new();
        EventUpdatePacket packet = new(
            0x10001,
            0x30400000,
            1,
            2,
            5,
            [
                new LuaParameter(LuaParameterType.BooleanTrue, null),
                new LuaParameter(LuaParameterType.UInt8, (byte)3)
            ]);

        SubPacket encoded = codec.Encode(packet.TriggerActorId, packet);
        EventUpdatePacket decoded = codec.Decode(encoded);

        Assert.Equal(PacketOpcode.EventUpdate, encoded.Header.Opcode);
        Assert.Equal(EventUpdatePacketCodec.PayloadSize, encoded.Payload.Length);
        Assert.Equal(true, decoded.Parameters[0].Value);
        Assert.Equal((byte)3, decoded.Parameters[1].Value);
    }

    [Fact]
    public void OfficialAttributeAllocationExchangeDecodesExactUiAndCommitValues()
    {
        byte[] runPayload = Convert.FromHexString(
            "41299B02E52EF0A000636F6D6D616E644A756467654D6F64650000000000000000000000000000000064656C6567617465436F6D6D616E64000000000000000000000000000000000006A0F02EE5026F706572617465554900000000001A000000000D000000000C0000000000000000000D0000000000000000000000000000000F00000000000000404D3E40000000");
        RunEventFunctionPacket run = new RunEventFunctionPacketCodec().Decode(
            SubPacket.Create(PacketOpcode.RunEventFunction, 0x029B2941, runPayload));

        Assert.Equal("commandJudgeMode", run.EventName);
        Assert.Equal("delegateCommand", run.FunctionName);
        Assert.Equal(0xA0F02EE5u, run.Parameters[0].Value);
        Assert.Equal("operateUI", run.Parameters[1].Value);
        Assert.Equal([26, 13, 12, 0, 13, 0, 0, 0], run.Parameters.Skip(2).Select(parameter => (int)parameter.Value!).ToArray());

        byte[] updatePayload = Convert.FromHexString(
            "41299B02000040240000000034A358460003000000000D0000000000000000000D0000000000000000000000000000000F00000000000000000000000000000000000000000000000000000000000000005B9E0000000020");
        EventUpdatePacket update = new EventUpdatePacketCodec().Decode(
            SubPacket.Create(PacketOpcode.EventUpdate, 0x029B2941, updatePayload));

        Assert.Equal(true, update.Parameters[0].Value);
        Assert.Equal([13, 0, 13, 0, 0, 0], update.Parameters.Skip(1).Select(parameter => (int)parameter.Value!).ToArray());
    }

    [Fact]
    public void ServerEventPacketsUseKnownLegacyOpcodesAndOffsets()
    {
        KickEventPacketCodec kickCodec = new();
        RunEventFunctionPacketCodec runCodec = new();
        EndEventPacketCodec endCodec = new();

        KickEventPacket kick = new(0x10001, 0x20002, 5, "noticeEvent", [new LuaParameter(LuaParameterType.String, "ok")]);
        RunEventFunctionPacket run = new(0x10001, 0x20002, 5, "noticeEvent", "delegateEvent", [new LuaParameter(LuaParameterType.Null, null)]);
        EndEventPacket end = new(0x10001, 5, "noticeEvent");

        SubPacket kickEncoded = kickCodec.Encode(kick.TriggerActorId, kick);
        SubPacket runEncoded = runCodec.Encode(run.TriggerActorId, run);
        SubPacket endEncoded = endCodec.Encode(end.SourcePlayerActorId, end);

        Assert.Equal(PacketOpcode.KickEvent, kickEncoded.Header.Opcode);
        Assert.Equal(PacketOpcode.RunEventFunction, runEncoded.Header.Opcode);
        Assert.Equal(PacketOpcode.EndEvent, endEncoded.Header.Opcode);
        Assert.Equal("noticeEvent", kickCodec.Decode(kickEncoded).EventName);
        Assert.Equal("delegateEvent", runCodec.Decode(runEncoded).FunctionName);
        Assert.Equal("noticeEvent", endCodec.Decode(endEncoded).EventName);
    }
}
