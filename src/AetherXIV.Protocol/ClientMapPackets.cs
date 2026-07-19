using System.Text;
using static AetherXIV.Protocol.ClientMapPacketCodecHelpers;

namespace AetherXIV.Protocol;

public readonly record struct MapPingPacket(uint ClientTime);

public sealed class MapPingPacketCodec : IPacketCodec<MapPingPacket>
{
    public const int PayloadSize = 0x38 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.Ping;

    public Type PacketType => typeof(MapPingPacket);

    public MapPingPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        RequirePayload(packet.Payload.Span, sizeof(uint), "map ping");
        return new MapPingPacket(PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span));
    }

    public SubPacket Encode(uint sourceActorId, MapPingPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.ClientTime);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct MapPongPacket(uint ClientTime, uint ServerConstant = 0x14D);

public sealed class MapPongPacketCodec : IPacketCodec<MapPongPacket>
{
    public const int PayloadSize = 0x40 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.Pong;

    public Type PacketType => typeof(MapPongPacket);

    public MapPongPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        RequirePayload(packet.Payload.Span, 8, "map pong");
        return new MapPongPacket(
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span),
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span[4..]));
    }

    public SubPacket Encode(uint sourceActorId, MapPongPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.ClientTime);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.ServerConstant);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct MapLoginHandshakeResponsePacket(uint ActorId);

public sealed class MapLoginHandshakeResponsePacketCodec : IPacketCodec<MapLoginHandshakeResponsePacket>
{
    public PacketOpcode Opcode => PacketOpcode.MapLoginHandshake;

    public Type PacketType => typeof(MapLoginHandshakeResponsePacket);

    public MapLoginHandshakeResponsePacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        RequirePayload(packet.Payload.Span, 0x10, "map login handshake response");
        return new MapLoginHandshakeResponsePacket(
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span[0x08..]));
    }

    public SubPacket Encode(uint sourceActorId, MapLoginHandshakeResponsePacket packet)
    {
        byte[] payload = new byte[0x10];
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x08), packet.ActorId);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct ClientLanguageCodePacket(uint LanguageCode);

public sealed class ClientLanguageCodePacketCodec : IPacketCodec<ClientLanguageCodePacket>
{
    public PacketOpcode Opcode => PacketOpcode.ClientLanguageCode;

    public Type PacketType => typeof(ClientLanguageCodePacket);

    public ClientLanguageCodePacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        RequirePayload(packet.Payload.Span, 8, "client language-code");
        return new ClientLanguageCodePacket(PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span[4..]));
    }

    public SubPacket Encode(uint sourceActorId, ClientLanguageCodePacket packet)
    {
        byte[] payload = new byte[8];
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.LanguageCode);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct ClientZoneInCompletePacket(uint Timestamp, int Unknown);

public sealed class ClientZoneInCompletePacketCodec : IPacketCodec<ClientZoneInCompletePacket>
{
    public PacketOpcode Opcode => PacketOpcode.ClientZoneInComplete;

    public Type PacketType => typeof(ClientZoneInCompletePacket);

    public ClientZoneInCompletePacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        RequirePayload(packet.Payload.Span, 8, "client zone-in-complete");
        return new ClientZoneInCompletePacket(
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span),
            PacketBinary.ReadInt32LittleEndian(packet.Payload.Span[4..]));
    }

    public SubPacket Encode(uint sourceActorId, ClientZoneInCompletePacket packet)
    {
        byte[] payload = new byte[8];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.Timestamp);
        PacketBinary.WriteInt32LittleEndian(payload.AsSpan(4), packet.Unknown);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct ClientCutsceneStatePacket(
    uint State,
    string CutsceneName,
    uint Detail);

public sealed class ClientCutsceneStatePacketCodec : IPacketCodec<ClientCutsceneStatePacket>
{
    public const int PayloadSize = 0x28;

    // The client-to-server key overlaps SetActorPosition in the opposite direction.
    public PacketOpcode Opcode => PacketOpcode.SetActorPosition;

    public Type PacketType => typeof(ClientCutsceneStatePacket);

    public ClientCutsceneStatePacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = packet.Payload.Span;
        RequirePayload(payload, PayloadSize, "client cutscene state");
        return new ClientCutsceneStatePacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            EventStartPacketCodec.ReadFixedString(payload[4..], 0x20),
            PacketBinary.ReadUInt32LittleEndian(payload[0x24..]));
    }

    public SubPacket Encode(uint sourceActorId, ClientCutsceneStatePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.State);
        EventStartPacketCodec.WriteFixedString(payload.AsSpan(4), 0x20, packet.CutsceneName);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x24), packet.Detail);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct ClientUpdateItemPackagePacket(uint ActorId, uint PackageId);

public sealed class ClientUpdateItemPackagePacketCodec : IPacketCodec<ClientUpdateItemPackagePacket>
{
    public PacketOpcode Opcode => PacketOpcode.ClientUpdateItemPackage;

    public Type PacketType => typeof(ClientUpdateItemPackagePacket);

    public ClientUpdateItemPackagePacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        RequirePayload(packet.Payload.Span, 8, "client item-package update request");
        return new ClientUpdateItemPackagePacket(
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span),
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span[4..]));
    }

    public SubPacket Encode(uint sourceActorId, ClientUpdateItemPackagePacket packet)
    {
        byte[] payload = new byte[8];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.ActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.PackageId);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct ClientPlayerPositionPacket(
    ulong Timestamp,
    float X,
    float Y,
    float Z,
    float Rotation,
    ushort MoveState);

public sealed class ClientPlayerPositionPacketCodec : IPacketCodec<ClientPlayerPositionPacket>
{
    public PacketOpcode Opcode => PacketOpcode.ClientUpdatePosition;

    public Type PacketType => typeof(ClientPlayerPositionPacket);

    public ClientPlayerPositionPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = packet.Payload.Span;
        RequirePayload(payload, 0x1A, "client position update");
        return new ClientPlayerPositionPacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            PacketBinary.ReadSingleLittleEndian(payload[8..]),
            PacketBinary.ReadSingleLittleEndian(payload[12..]),
            PacketBinary.ReadSingleLittleEndian(payload[16..]),
            PacketBinary.ReadSingleLittleEndian(payload[20..]),
            PacketBinary.ReadUInt16LittleEndian(payload[24..]));
    }

    public SubPacket Encode(uint sourceActorId, ClientPlayerPositionPacket packet)
    {
        byte[] payload = new byte[0x20];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.Timestamp);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(8), packet.X);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(12), packet.Y);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(16), packet.Z);
        PacketBinary.WriteSingleLittleEndian(payload.AsSpan(20), packet.Rotation);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(24), packet.MoveState);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct ClientSetTargetPacket(uint ActorId, uint AttackTarget)
{
    public const uint InvalidActorId = 0xE0000000;

    public bool IsClear => ActorId is 0 or InvalidActorId;

    public bool RequestsAutoAttack => AttackTarget != InvalidActorId;
}

public sealed class ClientSetTargetPacketCodec : IPacketCodec<ClientSetTargetPacket>
{
    public PacketOpcode Opcode => PacketOpcode.ClientSetTarget;

    public Type PacketType => typeof(ClientSetTargetPacket);

    public ClientSetTargetPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        RequirePayload(packet.Payload.Span, 8, "client target");
        return new ClientSetTargetPacket(
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span),
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span[4..]));
    }

    public SubPacket Encode(uint sourceActorId, ClientSetTargetPacket packet)
    {
        byte[] payload = new byte[8];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.ActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.AttackTarget);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct ClientActorInstantiateAcknowledgePacket(uint ActorId, uint Unknown);

public readonly record struct ClientLockTargetPacket(uint ActorId, uint Context)
{
    public const uint ClearActorId = 0xC0000000;

    public bool IsClear => ActorId is ClearActorId or ClientSetTargetPacket.InvalidActorId or UInt32.MaxValue;
}

public sealed class ClientLockTargetPacketCodec : IPacketCodec<ClientLockTargetPacket>
{
    public PacketOpcode Opcode => PacketOpcode.ClientLockTarget;

    public Type PacketType => typeof(ClientLockTargetPacket);

    public ClientLockTargetPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        RequirePayload(packet.Payload.Span, 8, "client lock-target state");
        return new ClientLockTargetPacket(
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span),
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span[4..]));
    }

    public SubPacket Encode(uint sourceActorId, ClientLockTargetPacket packet)
    {
        byte[] payload = new byte[8];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.ActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.Context);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public sealed class ClientActorInstantiateAcknowledgePacketCodec : IPacketCodec<ClientActorInstantiateAcknowledgePacket>
{
    public PacketOpcode Opcode => PacketOpcode.ActorInstantiate;

    public Type PacketType => typeof(ClientActorInstantiateAcknowledgePacket);

    public ClientActorInstantiateAcknowledgePacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        RequirePayload(packet.Payload.Span, 8, "client actor-instantiate acknowledge");
        return new ClientActorInstantiateAcknowledgePacket(
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span),
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span[4..]));
    }

    public SubPacket Encode(uint sourceActorId, ClientActorInstantiateAcknowledgePacket packet)
    {
        byte[] payload = new byte[8];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.ActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.Unknown);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct ClientParameterDataRequestPacket(uint ActorId, string ParameterName);

public sealed class ClientParameterDataRequestPacketCodec : IPacketCodec<ClientParameterDataRequestPacket>
{
    public PacketOpcode Opcode => PacketOpcode.ClientParameterDataRequest;

    public Type PacketType => typeof(ClientParameterDataRequestPacket);

    public ClientParameterDataRequestPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = packet.Payload.Span;
        RequirePayload(payload, 5, "client parameter-data request");
        return new ClientParameterDataRequestPacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            ReadNullTerminatedAscii(payload[4..], 0x20));
    }

    public SubPacket Encode(uint sourceActorId, ClientParameterDataRequestPacket packet)
    {
        byte[] payload = new byte[0x28];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.ActorId);
        WriteNullTerminatedAscii(payload.AsSpan(4), 0x20, packet.ParameterName);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct ClientGroupCreatedPacket(ulong GroupId, string WorkString);

public sealed class ClientGroupCreatedPacketCodec : IPacketCodec<ClientGroupCreatedPacket>
{
    public PacketOpcode Opcode => PacketOpcode.ClientGroupCreated;

    public Type PacketType => typeof(ClientGroupCreatedPacket);

    public ClientGroupCreatedPacket Decode(SubPacket packet)
    {
        EnsureOpcode(packet, Opcode);
        ReadOnlySpan<byte> payload = packet.Payload.Span;
        RequirePayload(payload, 8, "client group-created confirm");
        return new ClientGroupCreatedPacket(
            PacketBinary.ReadUInt64LittleEndian(payload),
            ReadNullTerminatedAscii(payload[8..], payload.Length - 8));
    }

    public SubPacket Encode(uint sourceActorId, ClientGroupCreatedPacket packet)
    {
        int stringBytes = Math.Min(Encoding.ASCII.GetByteCount(packet.WorkString), 0x40);
        byte[] payload = new byte[8 + stringBytes + 1];
        PacketBinary.WriteUInt64LittleEndian(payload, packet.GroupId);
        WriteNullTerminatedAscii(payload.AsSpan(8), stringBytes + 1, packet.WorkString);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

internal static class ClientMapPacketCodecHelpers
{
    public static void EnsureOpcode(SubPacket packet, PacketOpcode opcode)
    {
        if (packet.Header.Opcode != opcode)
            throw new ArgumentException($"Expected opcode {opcode} but received {packet.Header.Opcode}.", nameof(packet));
    }

    public static void RequirePayload(ReadOnlySpan<byte> payload, int requiredLength, string packetName)
    {
        if (payload.Length < requiredLength)
            throw new InvalidDataException($"{packetName} payload ended before {requiredLength} bytes.");
    }

    public static string ReadNullTerminatedAscii(ReadOnlySpan<byte> payload, int maxLength)
    {
        int length = Math.Min(payload.Length, maxLength);
        ReadOnlySpan<byte> slice = payload[..length];
        int terminator = slice.IndexOf((byte)0);
        if (terminator >= 0)
            slice = slice[..terminator];

        return Encoding.ASCII.GetString(slice);
    }

    public static void WriteNullTerminatedAscii(Span<byte> payload, int maxLength, string value)
    {
        int length = Math.Min(payload.Length, maxLength);
        int count = Math.Min(Encoding.ASCII.GetByteCount(value), Math.Max(0, length - 1));
        string truncated = value.Length > count ? value[..count] : value;
        Encoding.ASCII.GetBytes(truncated, payload[..count]);
        if (count < length)
            payload[count] = 0;
    }
}
