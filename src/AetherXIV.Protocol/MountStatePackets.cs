namespace AetherXIV.Protocol;

public readonly record struct SetCurrentMountChocoboPacket(
    uint RentalExpiresAt,
    byte RentalMinutesLeft,
    byte AppearanceId);

public sealed class SetCurrentMountChocoboPacketCodec : IPacketCodec<SetCurrentMountChocoboPacket>
{
    public PacketOpcode Opcode => PacketOpcode.SetCurrentMountChocobo;

    public Type PacketType => typeof(SetCurrentMountChocoboPacket);

    public SetCurrentMountChocoboPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode || packet.Payload.Length < 6)
            throw new ArgumentException("Invalid current-mount chocobo packet.", nameof(packet));
        return new SetCurrentMountChocoboPacket(
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span),
            packet.Payload.Span[4],
            packet.Payload.Span[5]);
    }

    public SubPacket Encode(uint sourceActorId, SetCurrentMountChocoboPacket packet)
    {
        byte[] payload = new byte[8];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.RentalExpiresAt);
        payload[4] = packet.RentalMinutesLeft;
        payload[5] = packet.AppearanceId;
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}
