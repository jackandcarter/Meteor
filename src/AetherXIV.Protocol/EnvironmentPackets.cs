namespace AetherXIV.Protocol;

public readonly record struct SetMapPacket(uint RegionId, uint ZoneId, uint Unknown = 0x28);

public sealed class SetMapPacketCodec : IPacketCodec<SetMapPacket>
{
    public const int PayloadSize = 0x30 - 0x20;
    public const uint LegacyUnknown = 0x28;

    public PacketOpcode Opcode => PacketOpcode.MapSetMap;

    public Type PacketType => typeof(SetMapPacket);

    public SetMapPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        if (payload.Length < PayloadSize)
            throw new InvalidDataException($"Set map payload requires {PayloadSize} bytes; received {payload.Length}.");

        return new SetMapPacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            PacketBinary.ReadUInt32LittleEndian(payload[8..]));
    }

    public SubPacket Encode(uint sourceActorId, SetMapPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.RegionId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.ZoneId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(8), packet.Unknown);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public enum WeatherId : ushort
{
    Clear = 8001,
    Fair = 8002,
    Cloudy = 8003,
    Foggy = 8004,
    Windy = 8005,
    Blustery = 8006,
    Rainy = 8007,
    Showery = 8008,
    Thundery = 8009,
    Stormy = 8010,
    Dusty = 8011,
    Sandy = 8012,
    Hot = 8013,
    Blistering = 8014,
    Snowy = 8015,
    Wintry = 8016,
    Gloomy = 8017,
    Seasonal = 8027,
    Primal = 8028,
    SeasonalFireworks = 8029,
    Dalamud = 8030,
    Aurora = 8031,
    DalamudThunder = 8032,
    Day = 8065,
    Twilight = 8066
}

public readonly record struct SetWeatherPacket(WeatherId Weather, ushort TransitionTime);

public sealed class SetWeatherPacketCodec : IPacketCodec<SetWeatherPacket>
{
    public PacketOpcode Opcode => PacketOpcode.SetWeather;

    public Type PacketType => typeof(SetWeatherPacket);

    public SetWeatherPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ulong combined = PacketBinary.ReadUInt64LittleEndian(packet.Payload.Span);
        WeatherId weather = (WeatherId)(combined & 0xFFFF);
        ushort transitionTime = (ushort)((combined >> 16) & 0xFFFF);
        return new SetWeatherPacket(weather, transitionTime);
    }

    public SubPacket Encode(uint sourceActorId, SetWeatherPacket packet)
    {
        byte[] payload = new byte[8];
        ulong combined = (ushort)packet.Weather | ((ulong)packet.TransitionTime << 16);
        PacketBinary.WriteUInt64LittleEndian(payload, combined);
        return SubPacket.Create(Opcode, 0, payload);
    }
}

public readonly record struct SetDalamudPacket(sbyte Level);

public sealed class SetDalamudPacketCodec : IPacketCodec<SetDalamudPacket>
{
    public PacketOpcode Opcode => PacketOpcode.SetDalamud;

    public Type PacketType => typeof(SetDalamudPacket);

    public SetDalamudPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        int level = unchecked((int)PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span));
        return new SetDalamudPacket((sbyte)level);
    }

    public SubPacket Encode(uint sourceActorId, SetDalamudPacket packet)
    {
        byte[] payload = new byte[8];
        PacketBinary.WriteInt32LittleEndian(payload, packet.Level);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}
