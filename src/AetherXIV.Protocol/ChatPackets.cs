using System.Text;

namespace AetherXIV.Protocol;

public static class ChatMessageType
{
    public const uint Say = 1;
    public const uint Shout = 2;
    public const uint Tell = 3;
    public const uint Party = 4;
}

public readonly record struct ClientChatMessagePacket(
    ulong Unknown,
    float X,
    float Y,
    float Z,
    float Rotation,
    uint MessageType,
    string Message);

public sealed class ClientChatMessagePacketCodec : IPacketCodec<ClientChatMessagePacket>
{
    public const int PayloadSize = 0x21C;
    public const int MessageSize = 0x200;

    public PacketOpcode Opcode => PacketOpcode.ClientChatMessage;

    public Type PacketType => typeof(ClientChatMessagePacket);

    public ClientChatMessagePacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet);
        if (packet.Payload.Length < PayloadSize)
            throw new InvalidDataException($"Client chat payload requires at least {PayloadSize} bytes; received {packet.Payload.Length}.");

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        return new ClientChatMessagePacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            PacketBinary.ReadSingleLittleEndian(payload[0x08..]),
            PacketBinary.ReadSingleLittleEndian(payload[0x0C..]),
            PacketBinary.ReadSingleLittleEndian(payload[0x10..]),
            PacketBinary.ReadSingleLittleEndian(payload[0x14..]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x18..]),
            ReadFixedAscii(payload.Slice(0x1C, MessageSize)));
    }

    public SubPacket Encode(uint sourceActorId, ClientChatMessagePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Unknown);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(0x08), packet.X);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(0x0C), packet.Y);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(0x10), packet.Z);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(0x14), packet.Rotation);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x18), packet.MessageType);
        WriteFixedAscii(payload.AsSpan(0x1C, MessageSize), packet.Message);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }

    private void EnsureOpcode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected opcode 0x{(ushort)Opcode:X4}; received 0x{(ushort)packet.Header.Opcode:X4}.");
    }

    internal static string ReadFixedAscii(ReadOnlySpan<byte> source)
    {
        int terminator = source.IndexOf((byte)0);
        return Encoding.ASCII.GetString(terminator >= 0 ? source[..terminator] : source);
    }

    internal static void WriteFixedAscii(Span<byte> destination, string value)
    {
        destination.Clear();
        byte[] encoded = Encoding.ASCII.GetBytes(value ?? String.Empty);
        encoded.AsSpan(0, Math.Min(encoded.Length, destination.Length)).CopyTo(destination);
    }
}

public readonly record struct ServerChatMessagePacket(
    string Sender,
    uint MessageType,
    string Message);

public sealed class ServerChatMessagePacketCodec : IPacketCodec<ServerChatMessagePacket>
{
    public const int PayloadSize = 0x228;
    public const int SenderSize = 0x20;
    public const int MessageSize = 0x200;

    public PacketOpcode Opcode => PacketOpcode.ClientChatMessage;

    public Type PacketType => typeof(ServerChatMessagePacket);

    public ServerChatMessagePacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected opcode 0x{(ushort)Opcode:X4}; received 0x{(ushort)packet.Header.Opcode:X4}.");
        if (packet.Payload.Length < PayloadSize)
            throw new InvalidDataException($"Server chat payload requires at least {PayloadSize} bytes; received {packet.Payload.Length}.");

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        return new ServerChatMessagePacket(
            ClientChatMessagePacketCodec.ReadFixedAscii(payload[..SenderSize]),
            PacketBinary.ReadUInt32LittleEndian(payload[0x20..]),
            ClientChatMessagePacketCodec.ReadFixedAscii(payload.Slice(0x24, MessageSize)));
    }

    public SubPacket Encode(uint sourceActorId, ServerChatMessagePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        ClientChatMessagePacketCodec.WriteFixedAscii(payload.AsSpan(0, SenderSize), packet.Sender);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x20), packet.MessageType);
        ClientChatMessagePacketCodec.WriteFixedAscii(payload.AsSpan(0x24, MessageSize), packet.Message);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}
