using System.Text;

namespace AetherXIV.Protocol;

public readonly record struct ClientCountdownRequestPacket(byte CountdownLength, ulong SyncTime);

public sealed class ClientCountdownRequestPacketCodec : IPacketCodec<ClientCountdownRequestPacket>
{
    public const int PayloadSize = 0x10;

    public PacketOpcode Opcode => PacketOpcode.ClientCountdownRequest;

    public Type PacketType => typeof(ClientCountdownRequestPacket);

    public ClientCountdownRequestPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet);
        if (packet.Payload.Length < PayloadSize)
            throw new InvalidDataException($"Client countdown payload requires at least {PayloadSize} bytes; received {packet.Payload.Length}.");

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        return new ClientCountdownRequestPacket(
            payload[0],
            PacketBinary.ReadUInt64LittleEndian(payload[0x08..]));
    }

    public SubPacket Encode(uint sourceActorId, ClientCountdownRequestPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        payload[0] = packet.CountdownLength;
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x08), packet.SyncTime);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }

    private void EnsureOpcode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected opcode 0x{(ushort)Opcode:X4}; received 0x{(ushort)packet.Header.Opcode:X4}.");
    }
}

public readonly record struct StartCountdownPacket(byte CountdownLength, ulong SyncTime, string Message);

public sealed class StartCountdownPacketCodec : IPacketCodec<StartCountdownPacket>
{
    public const int PayloadSize = 0x28;
    public const int MessageOffset = 0x12;
    public const int MessageCapacity = PayloadSize - MessageOffset;

    public PacketOpcode Opcode => PacketOpcode.StartCountdown;

    public Type PacketType => typeof(StartCountdownPacket);

    public StartCountdownPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected opcode 0x{(ushort)Opcode:X4}; received 0x{(ushort)packet.Header.Opcode:X4}.");
        if (packet.Payload.Length < PayloadSize)
            throw new InvalidDataException($"Start countdown payload requires at least {PayloadSize} bytes; received {packet.Payload.Length}.");

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        ReadOnlySpan<byte> messageBytes = payload.Slice(MessageOffset, MessageCapacity);
        int terminator = messageBytes.IndexOf((byte)0);
        return new StartCountdownPacket(
            payload[0],
            PacketBinary.ReadUInt64LittleEndian(payload[0x08..]),
            Encoding.ASCII.GetString(terminator >= 0 ? messageBytes[..terminator] : messageBytes));
    }

    public SubPacket Encode(uint sourceActorId, StartCountdownPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        payload[0] = packet.CountdownLength;
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x08), packet.SyncTime);
        byte[] messageBytes = Encoding.ASCII.GetBytes(packet.Message ?? String.Empty);
        messageBytes.AsSpan(0, Math.Min(messageBytes.Length, MessageCapacity))
            .CopyTo(payload.AsSpan(MessageOffset, MessageCapacity));
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}
