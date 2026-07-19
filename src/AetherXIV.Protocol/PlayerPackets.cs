namespace AetherXIV.Protocol;

public readonly record struct SetMusicPacket(ushort MusicId, ushort TrackMode);

public sealed class SetMusicPacketCodec : IPacketCodec<SetMusicPacket>
{
    public const int PayloadSize = 0x28 - 0x20;
    public const ushort EffectImmediate = 0x1;
    public const ushort EffectCrossfade = 0x2;
    public const ushort EffectLayer = 0x3;
    public const ushort EffectFadeIn = 0x4;
    public const ushort EffectPlayNormalChannel = 0x5;
    public const ushort EffectPlayBattleChannel = 0x6;

    public PacketOpcode Opcode => PacketOpcode.SetMusic;

    public Type PacketType => typeof(SetMusicPacket);

    public SetMusicPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ulong combined = PacketBinary.ReadUInt64LittleEndian(packet.Payload.Span);
        return new SetMusicPacket(
            (ushort)(combined & 0xFFFF),
            (ushort)((combined >> 16) & 0xFFFF));
    }

    public SubPacket Encode(uint sourceActorId, SetMusicPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        ulong combined = packet.MusicId | ((ulong)packet.TrackMode << 16);
        PacketBinary.WriteUInt64LittleEndian(payload, combined);
        return SubPacket.Create(Opcode, 0, payload);
    }
}

public readonly record struct GenericDataPacket(IReadOnlyList<LuaParameter> Parameters);

public sealed class GenericDataPacketCodec : IPacketCodec<GenericDataPacket>
{
    public const int PayloadSize = 0xE0 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.GenericData;

    public Type PacketType => typeof(GenericDataPacket);

    public GenericDataPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        return new GenericDataPacket(LuaParameterCodec.Decode(packet.Payload.Span));
    }

    public SubPacket Encode(uint sourceActorId, GenericDataPacket packet)
    {
        byte[] encoded = LuaParameterCodec.Encode(packet.Parameters);
        if (encoded.Length > PayloadSize)
            throw new InvalidDataException($"Generic data Lua parameters require {encoded.Length} bytes; maximum is {PayloadSize}.");

        byte[] payload = new byte[PayloadSize];
        encoded.CopyTo(payload, 0);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}
