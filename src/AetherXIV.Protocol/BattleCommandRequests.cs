namespace AetherXIV.Protocol;

public enum ClientBattleCommandRequestKind
{
    Default,
    Forced
}

public sealed record ClientBattleCommandRequest(
    ClientBattleCommandRequestKind Kind,
    ushort CommandId,
    uint CommandActorId,
    uint PlayerActorId,
    uint? TargetActorId,
    EventStartPacket Event);

public sealed class ClientBattleCommandRequestCodec
{
    public const uint CommandActorPrefix = 0xA0F00000;
    public const uint CommandActorMask = 0xFFFF0000;

    private readonly EventStartPacketCodec eventStartCodec = new();

    public bool TryDecode(SubPacket packet, out ClientBattleCommandRequest? request)
    {
        request = null;
        if (packet.Header.Opcode != PacketOpcode.EventStart)
            return false;

        EventStartPacket eventStart = eventStartCodec.Decode(packet);
        ClientBattleCommandRequestKind kind;
        if (String.Equals(eventStart.EventName, "commandDefault", StringComparison.Ordinal))
            kind = ClientBattleCommandRequestKind.Default;
        else if (String.Equals(eventStart.EventName, "commandForced", StringComparison.Ordinal))
            kind = ClientBattleCommandRequestKind.Forced;
        else
            return false;

        if ((eventStart.OwnerActorId & CommandActorMask) != CommandActorPrefix)
            return false;

        uint? targetActorId = eventStart.Parameters
            .Where(parameter => parameter.Type == LuaParameterType.ActorId)
            .Select(parameter => (uint?)Convert.ToUInt32(parameter.Value))
            .FirstOrDefault();
        request = new ClientBattleCommandRequest(
            kind,
            checked((ushort)(eventStart.OwnerActorId & UInt16.MaxValue)),
            eventStart.OwnerActorId,
            eventStart.TriggerActorId,
            targetActorId,
            eventStart);
        return true;
    }
}
