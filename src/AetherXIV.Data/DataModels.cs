using AetherXIV.Core;

namespace AetherXIV.Data;

public static class AetherXivDatabase
{
    public const string DefaultDatabaseName = "ffxiv_server";
}

public sealed record MariaDbOptions(
    string Host = "localhost",
    ushort Port = 3306,
    string Database = AetherXivDatabase.DefaultDatabaseName,
    string User = "aetherxiv",
    string Password = "aether_dev")
{
    public string ToConnectionString()
    {
        return $"Server={Host};Port={Port};Database={Database};User ID={User};Password={Password};TreatTinyAsBoolean=false;Allow User Variables=true";
    }
}

public sealed record AccountRecord(AccountId Id, string LoginName, DateTimeOffset CreatedAt);

public sealed record SessionRecord(string SessionToken, AccountId AccountId, DateTimeOffset ExpiresAt);

public sealed record CharacterRecord(
    CharacterId Id,
    AccountId AccountId,
    WorldId WorldId,
    string Name,
    ZoneId CurrentZoneId,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation,
    ushort Slot = 0);

public sealed record CharacterAppearanceRecord(
    CharacterId CharacterId,
    uint ModelId,
    byte Tribe,
    uint Size,
    uint HairStyle,
    uint HairHighlightColor,
    uint HairVariation,
    byte FaceType,
    byte Characteristics,
    byte CharacteristicsColor,
    byte FaceEyebrows,
    byte FaceIrisSize,
    byte FaceEyeShape,
    byte FaceNose,
    byte FaceFeatures,
    byte FaceMouth,
    byte Ears,
    uint HairColor,
    uint SkinColor,
    uint EyeColor,
    uint Voice,
    uint MainHand,
    uint OffHand,
    uint SpMainHand,
    uint SpOffHand,
    uint Throwing,
    uint Pack,
    uint Pouch,
    uint Head,
    uint Body,
    uint Legs,
    uint Hands,
    uint Feet,
    uint Waist,
    uint Neck,
    uint LeftEar,
    uint RightEar,
    uint LeftWrist,
    uint RightWrist,
    uint LeftIndex,
    uint RightIndex,
    uint LeftFinger,
    uint RightFinger);

public sealed record CharacterCreateRequest(
    AccountId AccountId,
    WorldId WorldId,
    ushort Slot,
    string Name,
    ZoneId StartingZoneId,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation,
    byte StartingTown,
    byte StartingClass,
    ReadOnlyMemory<byte> AppearancePayload,
    CharacterId? ReservedCharacterId = null);

public sealed record CharacterCreationResult(CharacterRecord Character);

public static class CharacterCreationStates
{
    public const string Reserved = "Reserved";
    public const string Active = "Active";
    public const string Deleted = "Deleted";
}

public enum CharacterRenameStatus
{
    Renamed,
    NameUnavailable,
    NotFound
}

public enum CharacterDeleteStatus
{
    Deleted,
    NotFound
}

public enum CharacterReservationStatus
{
    Reserved,
    NameUnavailable,
    SlotUnavailable
}

public sealed record CharacterReservationRequest(
    AccountId AccountId,
    WorldId WorldId,
    ushort Slot,
    string Name);

public sealed record CharacterReservationRecord(
    CharacterId CharacterId,
    AccountId AccountId,
    WorldId WorldId,
    ushort Slot,
    string Name);

public sealed record CharacterReservationResult(
    CharacterReservationStatus Status,
    CharacterReservationRecord? Reservation = null);

public sealed record CharacterClassStateRecord(
    CharacterId CharacterId,
    byte ClassId,
    ushort Level,
    uint Experience,
    bool IsCurrent);

public static class CharacterEquipmentInventoryLink
{
    public const byte MissingContainerId = Byte.MaxValue;
    public const ushort MissingSlotId = UInt16.MaxValue;
}

public sealed record CharacterEquipmentSlotRecord(
    CharacterId CharacterId,
    ushort SlotId,
    uint ItemId,
    uint DyeId,
    uint ServerItemId = 0,
    byte InventoryContainerId = CharacterEquipmentInventoryLink.MissingContainerId,
    ushort InventorySlotId = CharacterEquipmentInventoryLink.MissingSlotId);

public sealed record ItemVisualRecord(
    uint ItemId,
    uint WeaponId,
    uint EquipmentId,
    uint VariantId,
    uint ColorId,
    uint OffHandWeaponId,
    uint OffHandEquipmentId,
    uint OffHandVariantId,
    ProvenanceRef Provenance);

public sealed record WeaponCombatProfileRecord(
    uint ItemId,
    string Name,
    byte ClassJob,
    ushort EquipPoint,
    byte HitCount,
    ushort DamageAttribute,
    ushort DamagePower,
    uint DamageIntervalMilliseconds,
    ushort AmmoVirtualDamagePower,
    ProvenanceRef Provenance);

public sealed record CharacterInventoryItemRecord(
    CharacterId CharacterId,
    byte ContainerId,
    ushort SlotId,
    uint ItemId,
    ushort Quantity,
    uint ServerItemId = 0,
    byte Quality = 1);

public sealed record PlayerStatRecord(
    CharacterId CharacterId,
    ushort StatId,
    int Value,
    string Source);

public static class PlayerStatIds
{
    public const ushort MaximumHitPoints = 0;
    public const ushort MaximumMagicPoints = 1;
    public const ushort MaximumTacticalPoints = 2;
    public const ushort Strength = 3;
    public const ushort Vitality = 4;
    public const ushort Dexterity = 5;
    public const ushort Intelligence = 6;
    public const ushort Mind = 7;
    public const ushort Piety = 8;
}

public sealed record PlayerBaseStatProfileRecord(
    byte ClassJob,
    byte Tribe,
    ushort Level,
    ushort BaseHitPoints,
    ushort BaseMagicPoints,
    ushort Strength,
    ushort Dexterity,
    ushort Vitality,
    ushort Intelligence,
    ushort Mind,
    ushort Piety,
    decimal HitPointVitalityFactor,
    decimal MagicPointPietyFactor,
    decimal HitPointMultiplier,
    decimal MagicPointMultiplier,
    ProvenanceRef Provenance);

public sealed record CharacterProfileRecord(
    CharacterId CharacterId,
    byte Guardian,
    byte BirthMonth,
    byte BirthDay,
    DateTimeOffset UpdatedAt);

public sealed record CharacterResourceStateRecord(
    CharacterId CharacterId,
    ushort CurrentHitPoints,
    ushort CurrentMagicPoints,
    ushort CurrentTacticalPoints,
    DateTimeOffset UpdatedAt);

public sealed record CharacterLoginStateRecord(
    CharacterId CharacterId,
    byte GrandCompanyCurrent,
    byte GrandCompanyLimsaRank,
    byte GrandCompanyGridaniaRank,
    byte GrandCompanyUldahRank,
    uint CurrentTitle,
    byte CurrentJob,
    ushort SpecialEventType,
    ushort SpecialEventId,
    ulong AchievementPoints,
    IReadOnlyList<ushort> CompletedAchievementOffsets,
    IReadOnlyList<uint> LatestAchievementIds)
{
    public static CharacterLoginStateRecord NewCharacter(CharacterId characterId) => new(
        characterId,
        GrandCompanyCurrent: 0,
        GrandCompanyLimsaRank: 0x7F,
        GrandCompanyGridaniaRank: 0x7F,
        GrandCompanyUldahRank: 0x7F,
        CurrentTitle: 0,
        CurrentJob: 0,
        SpecialEventType: 0,
        SpecialEventId: 18,
        AchievementPoints: 0,
        CompletedAchievementOffsets: [],
        LatestAchievementIds: []);
}

public sealed record CharacterHotbarSlotRecord(
    CharacterId CharacterId,
    byte ClassId,
    byte SlotIndex,
    uint CommandId,
    uint RecastEnd,
    ushort MaximumRecastSeconds);

public sealed record CharacterTimerStateRecord(
    CharacterId CharacterId,
    byte TimerIndex,
    uint Value);

public sealed record CharacterActionStateRecord(
    IReadOnlyList<CharacterHotbarSlotRecord> HotbarSlots,
    IReadOnlyList<CharacterTimerStateRecord> Timers)
{
    public static CharacterActionStateRecord Empty { get; } = new([], []);
}

public sealed record CharacterNpcLinkshellStateRecord(
    CharacterId CharacterId,
    byte LinkshellId,
    bool IsCalling,
    bool IsExtra);

public sealed record CharacterMapStateRecord(
    CharacterId CharacterId,
    ZoneId ZoneId,
    string? PrivateAreaName,
    uint PrivateAreaLevel,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation,
    DateTimeOffset UpdatedAt);

public sealed record CharacterProgressionStateRecord(
    CharacterId CharacterId,
    byte InitialTown,
    uint PlayTimeSeconds,
    uint HomePoint,
    byte HomePointInn,
    DateTimeOffset UpdatedAt,
    decimal RestBonusExpRate = 1.5m);

public sealed record CharacterQuestStateRecord(
    CharacterId CharacterId,
    uint QuestId,
    string QuestName,
    uint Phase,
    uint Flags,
    bool Completed,
    DateTimeOffset UpdatedAt,
    byte? SlotIndex = null,
    string QuestDataJson = "{}");

public sealed record QuestDefinitionRecord(
    uint QuestId,
    uint ActorId,
    string QuestName,
    string StaticPath,
    string Family,
    string? ScriptModulePath);

public sealed record CharacterQuestProgressionCommit(
    CharacterId CharacterId,
    CharacterProgressionStateRecord Progression,
    IReadOnlyList<CharacterQuestStateRecord> Quests);

public sealed record GuildleveObjectiveRecord(
    byte ObjectiveIndex,
    sbyte RequiredCount,
    uint ItemTargetId,
    uint BattleNpcTargetId);

public sealed record GuildleveDefinitionRecord(
    uint GuildleveId,
    uint ClassType,
    uint Location,
    ushort FactionCreditRequired,
    ushort Level,
    uint AetheryteId,
    uint PlateId,
    uint BorderId,
    uint ObjectiveId,
    uint PartyRecommended,
    uint TargetLocation,
    uint AuthorityId,
    byte TimeLimitMinutes,
    uint SkillId,
    byte FavorCount,
    IReadOnlyList<GuildleveObjectiveRecord> Objectives,
    ProvenanceRef Provenance);

public sealed record CharacterGuildleveStateRecord(
    CharacterId CharacterId,
    byte SlotIndex,
    uint GuildleveId,
    bool Abandoned,
    bool Completed,
    DateTimeOffset UpdatedAt);

public sealed record CharacterClassAttributeAllocationRecord(
    CharacterId CharacterId,
    byte ClassId,
    ushort PointsRemaining,
    ushort Strength,
    ushort Vitality,
    ushort Dexterity,
    ushort Intelligence,
    ushort Mind,
    ushort Piety,
    DateTimeOffset UpdatedAt)
{
    public uint SpentPoints => (uint)Strength + Vitality + Dexterity + Intelligence + Mind + Piety;
}

public sealed record WorldRecord(WorldId Id, string Name, ServerEndpoint Endpoint);

public sealed record ZoneRecord(
    ZoneId Id,
    string Name,
    uint RegionId,
    bool IsPrivate,
    bool LoadNavMesh,
    string ClassPath = "",
    ushort DayMusic = 0,
    ushort NightMusic = 0,
    ushort BattleMusic = 0,
    bool IsInn = false,
    bool CanRideChocobo = false,
    bool CanStealth = false,
    bool IsInstanceRaid = false);

public sealed record ZoneSeamlessBoundaryRecord(
    uint BoundaryId,
    uint RegionId,
    ZoneId ZoneAId,
    ZoneId ZoneBId,
    float ZoneAMinX,
    float ZoneAMaxX,
    float ZoneAMinZ,
    float ZoneAMaxZ,
    float ZoneBMinX,
    float ZoneBMaxX,
    float ZoneBMinZ,
    float ZoneBMaxZ,
    float MergeMinX,
    float MergeMaxX,
    float MergeMinZ,
    float MergeMaxZ,
    ProvenanceRef Provenance);

public sealed record ZoneEntranceRecord(
    uint EntranceId,
    ZoneId ZoneId,
    string? PrivateAreaName,
    uint PrivateAreaLevel,
    ushort SpawnType,
    float X,
    float Y,
    float Z,
    float Rotation,
    ProvenanceRef Provenance);

public sealed record PrivateAreaRecord(
    uint AreaId,
    ZoneId ParentZoneId,
    string ClassPath,
    string Name,
    uint Level,
    ushort DayMusic,
    ushort NightMusic,
    ushort BattleMusic,
    ProvenanceRef Provenance);

public sealed record MapHandoffTicketRecord(
    string Ticket,
    CharacterId CharacterId,
    WorldId WorldId,
    ZoneId ZoneId,
    ServerEndpoint MapEndpoint,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConsumedAt);

public sealed record WorldLoginHandoffRecord(
    uint RuntimeActorId,
    CharacterId CharacterId,
    string SessionToken,
    DateTimeOffset ExpiresAt);

public enum TutorialCheckpointState
{
    Stable,
    InProgress,
    RolledBack,
    Complete
}

public sealed record TutorialCheckpointRecord(
    CharacterId CharacterId,
    string DirectorName,
    string CheckpointName,
    TutorialCheckpointState State,
    string PayloadJson,
    DateTimeOffset UpdatedAt);

public sealed record ActorClassRecord(
    uint ActorClassId,
    string ClassPath,
    uint DisplayNameId,
    uint PropertyFlags,
    string EventConditions,
    ushort PushCommand,
    ushort PushCommandSub,
    byte PushCommandPriority,
    ProvenanceRef Provenance);

public sealed record ActorAppearanceRecord(
    uint ActorClassId,
    uint Base,
    uint Size,
    uint HairStyle,
    uint HairHighlightColor,
    uint HairVariation,
    byte FaceType,
    byte Characteristics,
    byte CharacteristicsColor,
    byte FaceEyebrows,
    byte FaceIrisSize,
    byte FaceEyeShape,
    byte FaceNose,
    byte FaceFeatures,
    byte FaceMouth,
    byte Ears,
    uint HairColor,
    uint SkinColor,
    uint EyeColor,
    uint Voice,
    uint MainHand,
    uint OffHand,
    uint SpMainHand,
    uint SpOffHand,
    uint Throwing,
    uint Pack,
    uint Pouch,
    uint Head,
    uint Body,
    uint Legs,
    uint Hands,
    uint Feet,
    uint Waist,
    uint Neck,
    uint LeftEar,
    uint RightEar,
    uint LeftIndex,
    uint RightIndex,
    uint LeftFinger,
    uint RightFinger,
    ProvenanceRef Provenance);

public sealed record StaticActorSpawnRecord(
    uint SpawnId,
    uint ActorClassId,
    string UniqueId,
    ZoneId ZoneId,
    string? PrivateAreaName,
    uint PrivateAreaLevel,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation,
    ushort ActorState,
    uint AnimationId,
    string? CustomDisplayName,
    ProvenanceRef Provenance,
    uint? MapObjectLayoutId = null,
    uint? MapObjectInstanceId = null);

public sealed record BattleNpcSpawnRecord(
    BattleNpcId BattleNpcId,
    uint GroupId,
    uint PoolId,
    ZoneId ZoneId,
    string ScriptName,
    byte MinLevel,
    byte MaxLevel,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation,
    ProvenanceRef Provenance,
    string? CustomDisplayName = null,
    uint GenusId = 0,
    byte CurrentJob = 0,
    byte CombatSkill = 0,
    ushort CombatDelay = 0,
    float CombatDamageMultiplier = 1,
    byte AggroType = 0,
    uint Immunity = 0,
    byte LinkType = 0,
    uint SkillListId = 0,
    uint SpellListId = 0,
    uint RespawnSeconds = 0,
    uint HitPoints = 0,
    uint MagicPoints = 0,
    uint DropListId = 0,
    byte Allegiance = 0,
    ushort SpawnType = 0,
    uint AnimationId = 0,
    ushort ActorState = 0,
    string? PrivateAreaName = null,
    uint PrivateAreaLevel = 0,
    uint ActorClassId = 0);

public enum BattleCommandType : ushort
{
    None = 0,
    AutoAttack = 1,
    WeaponSkill = 2,
    Ability = 3,
    Spell = 4
}

public sealed record BattleCommandRecord(
    ushort CommandId,
    string Name,
    byte ClassJob,
    byte Level,
    ushort Requirements,
    ushort MainTarget,
    ushort ValidTarget,
    byte AoeType,
    byte AoeTarget,
    ushort BasePotency,
    byte NumHits,
    float Range,
    float MinRange,
    int RangeHeight,
    int RangeWidth,
    uint BattleAnimation,
    ushort WorldMasterTextId,
    BattleCommandType CommandType,
    short MpCost,
    short TpCost,
    uint RecastTimeMs,
    ushort ActionType,
    ushort ActionProperty,
    ProvenanceRef Provenance,
    float AoeRange = 0,
    float AoeMinRange = 0,
    float AoeConeAngle = 0,
    float AoeRotateAngle = 0,
    byte PositionBonus = 0,
    byte ProcRequirement = 0,
    float BestRange = 0,
    uint StatusId = 0,
    uint StatusDurationSeconds = 0,
    float StatusChance = 0,
    byte CastType = 0,
    uint CastTimeMs = 0,
    byte AnimationType = 0,
    ushort EffectAnimation = 0,
    ushort ModelAnimation = 0,
    uint AnimationDurationSeconds = 0,
    byte ValidUser = 0,
    ushort ComboCommandId1 = 0,
    ushort ComboCommandId2 = 0,
    byte ComboStep = 0,
    float AccuracyModifier = 1,
    bool IsRanged = false);

public sealed record BattleCommandScriptRecord(
    ushort CommandId,
    string ScriptFolder,
    string ScriptName,
    ProvenanceRef Provenance);

public sealed record BattleTraitRecord(
    ushort TraitId,
    string Name,
    byte ClassJob,
    byte Level,
    uint ModifierId,
    short Bonus,
    ProvenanceRef Provenance);

public sealed record StatusEffectDefinitionRecord(
    uint StatusEffectId,
    string Name,
    uint Flags,
    byte OverwriteTier,
    uint TickMs,
    bool Hidden,
    bool SilentOnGain,
    bool SilentOnLoss,
    ushort GainTextId,
    ushort LossTextId,
    ProvenanceRef Provenance);

public sealed record CharacterCommandRecastRecord(
    CharacterId CharacterId,
    ushort CommandId,
    DateTimeOffset ReadyAt);

public sealed record CharacterStatusEffectRecord(
    CharacterId CharacterId,
    uint StatusEffectId,
    ActorId SourceActorId,
    double Magnitude,
    byte Tier,
    int Extra,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? NextTickAt);

public sealed record BattleNpcStatRecord(
    BattleNpcId BattleNpcId,
    ushort StatId,
    int Value,
    ProvenanceRef Provenance);

public sealed record BattleNpcActionRecord(
    uint ListId,
    ushort CommandId,
    BattleCommandType CommandType,
    byte Priority,
    ProvenanceRef Provenance);

public sealed record RetainerRecord(
    uint RetainerId,
    CharacterId CharacterId,
    byte Slot,
    string Name,
    uint ActorClassId,
    byte ContentIdOffset,
    ushort PlaceNameId,
    byte ConditionCode,
    byte Level,
    bool RequiresRename);

public sealed record LinkshellRecord(
    ulong LinkshellId,
    WorldId WorldId,
    string Name,
    ushort CrestId,
    CharacterId MasterCharacterId);

public sealed record LinkshellMemberRecord(
    ulong LinkshellId,
    CharacterId CharacterId,
    byte Rank);

public sealed record CharacterFriendRecord(
    CharacterId CharacterId,
    CharacterId FriendCharacterId,
    byte Slot,
    string FriendName);
