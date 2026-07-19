namespace AetherXIV.Protocol;

/// <summary>
/// Trace-facing packet identity used before every packet has a confident domain name.
///
/// Retail trace mining showed that the useful early key is direction + inner message
/// type + category/marker + subcode/opcode + inner message length. This keeps unknown
/// packets comparable without pretending that we understand all payload fields yet.
/// </summary>
public enum ProtocolService
{
    Unknown,
    Lobby,
    World,
    Map,
    WorldToMap,
    MapToWorld
}

public readonly record struct ObservedProtocolKey(
    ProtocolService Service,
    PacketDirection Direction,
    ushort MessageType,
    ushort Category,
    ushort Subcode,
    ushort MessageLength)
{
    public static ObservedProtocolKey FromSubPacket(
        PacketDirection direction,
        WireGameMessageSubPacket subPacket)
    {
        return FromSubPacket(ProtocolService.Unknown, direction, subPacket);
    }

    public static ObservedProtocolKey FromSubPacket(
        ProtocolService service,
        PacketDirection direction,
        WireGameMessageSubPacket subPacket)
    {
        ushort messageLength = checked((ushort)(
            BasePacketFrameCodec.SubPacketHeaderSize +
            BasePacketFrameCodec.GameMessageHeaderSize +
            subPacket.Packet.Payload.Length));

        return new ObservedProtocolKey(
            service,
            direction,
            BasePacketFrameCodec.GameMessageSubPacketType,
            BasePacketFrameCodec.GameMessageHeaderMarker,
            (ushort)subPacket.Packet.Header.Opcode,
            messageLength);
    }

    public static ObservedProtocolKey FromLegacySubPacket(
        ProtocolService service,
        PacketDirection direction,
        WireLegacySubPacket subPacket)
    {
        ushort messageLength = checked((ushort)(
            RawLegacySubPacketCodec.HeaderSize +
            subPacket.Payload.Length +
            (subPacket.IsGameMessage ? RawLegacySubPacketCodec.GameMessageHeaderSize : 0)));

        ushort category = subPacket.IsGameMessage ? RawLegacySubPacketCodec.GameMessageHeaderMarker : (ushort)0;
        ushort subcode = subPacket.IsGameMessage && subPacket.Opcode is not null
            ? (ushort)subPacket.Opcode.Value
            : subPacket.Type;

        return new ObservedProtocolKey(
            service,
            direction,
            subPacket.Type,
            category,
            subcode,
            messageLength);
    }

    public override string ToString()
    {
        string direction = Direction switch
        {
            PacketDirection.ClientToServer => "C2S",
            PacketDirection.ServerToClient => "S2C",
            PacketDirection.ServerToServer => "S2S",
            _ => Direction.ToString()
        };

        string service = Service == ProtocolService.Unknown ? String.Empty : $"{Service}:";
        return $"{service}{direction}:type=0x{MessageType:X4}:cat=0x{Category:X4}:sub=0x{Subcode:X4}:len={MessageLength}";
    }
}

public static class ObservedProtocolKeySet
{
    public static IReadOnlyList<ObservedProtocolKey> FromFrame(
        PacketDirection direction,
        BasePacketFrame frame)
    {
        return FromFrame(ProtocolService.Unknown, direction, frame);
    }

    public static IReadOnlyList<ObservedProtocolKey> FromFrame(
        ProtocolService service,
        PacketDirection direction,
        BasePacketFrame frame)
    {
        ObservedProtocolKey[] keys = new ObservedProtocolKey[frame.SubPackets.Count];
        for (int index = 0; index < frame.SubPackets.Count; index++)
            keys[index] = ObservedProtocolKey.FromSubPacket(service, direction, frame.SubPackets[index]);

        return keys;
    }

    public static IReadOnlyList<ObservedProtocolKey> FromLegacyFrame(
        ProtocolService service,
        PacketDirection direction,
        LegacyPacketFrame frame)
    {
        ObservedProtocolKey[] keys = new ObservedProtocolKey[frame.SubPackets.Count];
        for (int index = 0; index < frame.SubPackets.Count; index++)
            keys[index] = ObservedProtocolKey.FromLegacySubPacket(service, direction, frame.SubPackets[index]);

        return keys;
    }
}
