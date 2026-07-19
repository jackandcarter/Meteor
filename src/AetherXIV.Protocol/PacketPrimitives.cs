using System.Buffers.Binary;

namespace AetherXIV.Protocol;

public enum PacketDirection
{
    ClientToServer,
    ServerToClient,
    ServerToServer
}

public enum PacketOpcode : ushort
{
    Ping = 0x0001,
    Pong = 0x0001,
    LobbyError = 0x0002,
    MapLoginHandshake = 0x0002,
    ClientChatMessage = 0x0003,
    LobbyGetCharacters = 0x0003,
    LobbySelectCharacter = 0x0004,
    LobbySessionAcknowledgement = 0x0005,
    MapSetMap = 0x0005,
    ClientLanguageCode = 0x0006,
    ServerZoneInstanceBegin = 0x0006,
    ClientZoneInComplete = 0x0007,
    DeleteAllActors = 0x0007,
    ServerZoneInstanceEnd = 0x0007,
    ServerZoneInstanceActors = 0x0008,
    LobbyAccountList = 0x000C,
    SetMusic = 0x000C,
    SetWeather = 0x000D,
    LobbyCharacterList = 0x000D,
    LobbyModifyCharacter = 0x000B,
    LobbyCharacterCreationResult = 0x000E,
    PlayerLogout = 0x000E,
    LobbySelectCharacterConfirm = 0x000F,
    MapPlayerSpawnUnknown0x000F = 0x000F,
    SetDalamud = 0x0010,
    PlayerQuit = 0x0011,
    LobbyWorldList = 0x0015,
    LobbyImportList = 0x0016,
    LobbyRetainerList = 0x0017,
    ClientUpdatePosition = 0x00CA,
    ClientPartyChat = 0x00C9,
    AddActor = 0x00CA,
    RemoveActor = 0x00CB,
    ActorInstantiate = 0x00CC,
    ClientLockTarget = 0x00CC,
    ClientSetTarget = 0x00CD,
    SetActorPosition = 0x00CE,
    MoveActorToPosition = 0x00CF,
    ClientCountdownRequest = 0x00CF,
    SetActorSpeed = 0x00D0,
    ZoneTransitionState = 0x00E2,
    SetActorQuestGraphic = 0x00E3,
    StartCountdown = 0x00E5,
    SetActorTargetAnimated = 0x00D3,
    SetActorAppearance = 0x00D6,
    SetActorBGProperties = 0x00D8,
    PlayAnimationOnActor = 0x00DA,
    EventStart = 0x012D,
    ClientEventStart = 0x012D,
    EventUpdate = 0x012E,
    ClientEventUpdate = 0x012E,
    SetTalkEventCondition = 0x012E,
    ClientParameterDataRequest = 0x012F,
    KickEvent = 0x012F,
    RunEventFunction = 0x0130,
    ClientUpdateItemPackage = 0x0131,
    EndEvent = 0x0131,
    PlayerCommandCategory = 0x0132,
    ClientGroupCreated = 0x0133,
    GenericData = 0x0133,
    SetActorState = 0x0134,
    SetEventStatus = 0x0136,
    SetActorProperty = 0x0137,
    CommandResultX01 = 0x0139,
    CommandResultX10 = 0x013A,
    CommandResultX18 = 0x013B,
    CommandResultX00 = 0x013C,
    SetActorName = 0x013D,
    DeleteGroup = 0x0143,
    SetActorSubState = 0x0144,
    SetActorIcon = 0x0145,
    InventorySetBegin = 0x0146,
    InventorySetEnd = 0x0147,
    InventoryListX01 = 0x0148,
    InventoryListX08 = 0x0149,
    InventoryListX16 = 0x014A,
    InventoryListX32 = 0x014B,
    InventoryListX64 = 0x014C,
    LinkedItemListX01 = 0x014D,
    LinkedItemListX08 = 0x014E,
    LinkedItemListX16 = 0x014F,
    LinkedItemListX32 = 0x0150,
    LinkedItemListX64 = 0x0151,
    InventoryRemoveX01 = 0x0152,
    InventoryRemoveX08 = 0x0153,
    InventoryRemoveX16 = 0x0154,
    InventoryRemoveX32 = 0x0155,
    InventoryRemoveX64 = 0x0156,
    GameMessageWithActorX01 = 0x0157,
    GameMessageWithActorX02 = 0x0158,
    GameMessageWithActorX03 = 0x0159,
    GameMessageWithActorX04 = 0x015A,
    GameMessageWithActorX05 = 0x015B,
    GameMessageWithoutActorX01 = 0x0166,
    GameMessageWithoutActorX02 = 0x0167,
    GameMessageWithoutActorX03 = 0x0168,
    GameMessageWithoutActorX04 = 0x0169,
    GameMessageWithoutActorX05 = 0x016A,
    SetNoticeEventCondition = 0x016B,
    SetEmoteEventCondition = 0x016C,
    InventoryBeginChange = 0x016D,
    InventoryEndChange = 0x016E,
    SetPushEventConditionWithCircle = 0x016F,
    SetPushEventConditionWithFan = 0x0170,
    SetPushEventConditionWithTriggerBox = 0x0175,
    SetActorStatusAll = 0x0179,
    GroupWorkValues = 0x017A,
    SetActorIsZoning = 0x017B,
    GroupHeader = 0x017C,
    GroupMembersBegin = 0x017D,
    GroupMembersEnd = 0x017E,
    GroupMembersX08 = 0x017F,
    GroupMembersX16 = 0x0180,
    GroupMembersX32 = 0x0181,
    GroupMembersX64 = 0x0182,
    ContentMembersX08 = 0x0183,
    ContentMembersX16 = 0x0184,
    ContentMembersX32 = 0x0185,
    ContentMembersX64 = 0x0186,
    SetActiveLinkshell = 0x018A,
    CommandStateRow = 0x0190,
    GrandCompanyState = 0x0194,
    SpecialEventWorkState = 0x0196,
    SetCurrentMountChocobo = 0x0197,
    CompletedAchievementsState = 0x019A,
    LatestAchievementsState = 0x019B,
    AchievementPointsState = 0x019C,
    PlayerTitleState = 0x019D,
    CurrentJobState = 0x01A4,
    BlacklistState = 0x01CB,
    FriendListState = 0x01CE,
    FriendStatus = 0x01CF,
    GmTicketState = 0x01D3,
    WorldSessionBegin = 0x1000,
    WorldSessionEnd = 0x1001,
    WorldZoneChangeRequest = 0x1002,
    WorldRouteError = 0x100A,
    WorldPartyModify = 0x1020,
    WorldPartyLeave = 0x1021,
    WorldPartyInvite = 0x1022,
    WorldGroupInviteResult = 0x1023,
    WorldLinkshellCreate = 0x1025,
    WorldLinkshellModify = 0x1026,
    WorldLinkshellDelete = 0x1027,
    WorldLinkshellChange = 0x1028,
    WorldLinkshellInvite = 0x1029,
    WorldLinkshellInviteCancel = 0x1030,
    WorldLinkshellLeave = 0x1031,
    WorldLinkshellRankChange = 0x1032
}

public readonly record struct PacketHeader(PacketOpcode Opcode, uint SourceActorId, int PayloadLength);

public readonly record struct SubPacket(PacketHeader Header, ReadOnlyMemory<byte> Payload)
{
    public static SubPacket Create(PacketOpcode opcode, uint sourceActorId, ReadOnlyMemory<byte> payload)
    {
        return new SubPacket(new PacketHeader(opcode, sourceActorId, payload.Length), payload);
    }
}

public interface IPacketCodec
{
    PacketOpcode Opcode { get; }

    Type PacketType { get; }
}

public interface IPacketCodec<TPacket> : IPacketCodec
{
    TPacket Decode(SubPacket packet);

    SubPacket Encode(uint sourceActorId, TPacket packet);
}

public sealed class PacketRegistry
{
    private readonly Dictionary<(PacketDirection Direction, PacketOpcode Opcode, Type PacketType), IPacketCodec> codecs = new();

    public void Register<TPacket>(IPacketCodec<TPacket> codec)
    {
        Register(PacketDirection.ServerToClient, codec);
    }

    public void Register<TPacket>(PacketDirection direction, IPacketCodec<TPacket> codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        codecs[(direction, codec.Opcode, typeof(TPacket))] = codec;
    }

    public IPacketCodec<TPacket> Get<TPacket>(PacketOpcode opcode)
    {
        return Get<TPacket>(PacketDirection.ServerToClient, opcode);
    }

    public IPacketCodec<TPacket> Get<TPacket>(PacketDirection direction, PacketOpcode opcode)
    {
        if (codecs.TryGetValue((direction, opcode, typeof(TPacket)), out IPacketCodec? codec))
            return (IPacketCodec<TPacket>)codec;

        throw new KeyNotFoundException($"No packet codec registered for {direction} opcode 0x{(ushort)opcode:X4} and type {typeof(TPacket).Name}.");
    }
}

public static class PacketBinary
{
    public static ushort ReadUInt16LittleEndian(ReadOnlySpan<byte> buffer)
    {
        RequireLength(buffer, sizeof(ushort));
        return BinaryPrimitives.ReadUInt16LittleEndian(buffer);
    }

    public static uint ReadUInt32LittleEndian(ReadOnlySpan<byte> buffer)
    {
        RequireLength(buffer, sizeof(uint));
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer);
    }

    public static int ReadInt32LittleEndian(ReadOnlySpan<byte> buffer)
    {
        RequireLength(buffer, sizeof(int));
        return BinaryPrimitives.ReadInt32LittleEndian(buffer);
    }

    public static float ReadSingleLittleEndian(ReadOnlySpan<byte> buffer)
    {
        return BitConverter.Int32BitsToSingle(ReadInt32LittleEndian(buffer));
    }

    public static ulong ReadUInt64LittleEndian(ReadOnlySpan<byte> buffer)
    {
        RequireLength(buffer, sizeof(ulong));
        return BinaryPrimitives.ReadUInt64LittleEndian(buffer);
    }

    public static void WriteUInt16LittleEndian(Span<byte> buffer, ushort value)
    {
        RequireLength(buffer, sizeof(ushort));
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
    }

    public static void WriteUInt32LittleEndian(Span<byte> buffer, uint value)
    {
        RequireLength(buffer, sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
    }

    public static void WriteInt32LittleEndian(Span<byte> buffer, int value)
    {
        RequireLength(buffer, sizeof(int));
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
    }

    public static void WriteSingleLittleEndian(Span<byte> buffer, float value)
    {
        WriteInt32LittleEndian(buffer, BitConverter.SingleToInt32Bits(value));
    }

    public static void WriteUInt64LittleEndian(Span<byte> buffer, ulong value)
    {
        RequireLength(buffer, sizeof(ulong));
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
    }

    private static void RequireLength(ReadOnlySpan<byte> buffer, int required)
    {
        if (buffer.Length < required)
            throw new ArgumentException($"Expected at least {required} bytes but received {buffer.Length}.", nameof(buffer));
    }
}
