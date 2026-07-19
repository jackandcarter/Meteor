using AetherXIV.Core;

namespace AetherXIV.Data;

public sealed record V1CharacterRow(
    uint Id,
    uint UserId,
    uint ServerId,
    string Name,
    uint CurrentZoneId,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation,
    ushort Slot = 0);

public sealed record V1BattleNpcSpawnRow(
    uint BattleNpcId,
    uint GroupId,
    uint PoolId,
    uint ZoneId,
    string ScriptName,
    byte MinLevel,
    byte MaxLevel,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation,
    string SourceRef,
    EvidenceStatus EvidenceStatus = EvidenceStatus.Provisional);

public sealed record V1ActorClassRow(
    uint ActorClassId,
    string ClassPath,
    uint DisplayNameId,
    uint PropertyFlags,
    string? EventConditions,
    ushort PushCommand,
    ushort PushCommandSub,
    byte PushCommandPriority,
    string SourceRef,
    EvidenceStatus EvidenceStatus = EvidenceStatus.RepoConfirmed);

public sealed record V1ActorAppearanceRow(
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
    string SourceRef,
    EvidenceStatus EvidenceStatus = EvidenceStatus.RepoConfirmed);

public sealed record V1StaticActorSpawnRow(
    uint SpawnId,
    uint ActorClassId,
    string UniqueId,
    uint ZoneId,
    string? PrivateAreaName,
    uint PrivateAreaLevel,
    float PositionX,
    float PositionY,
    float PositionZ,
    float Rotation,
    ushort ActorState,
    uint AnimationId,
    string? CustomDisplayName,
    string SourceRef,
    EvidenceStatus EvidenceStatus = EvidenceStatus.RepoConfirmed);

public static class V1CompatibilityMappings
{
    public static CharacterRecord ToCharacterRecord(V1CharacterRow row)
    {
        return new CharacterRecord(
            new CharacterId(row.Id),
            new AccountId(row.UserId),
            new WorldId(row.ServerId),
            row.Name,
            new ZoneId(row.CurrentZoneId),
            row.PositionX,
            row.PositionY,
            row.PositionZ,
            row.Rotation,
            row.Slot);
    }

    public static BattleNpcSpawnRecord ToBattleNpcSpawnRecord(V1BattleNpcSpawnRow row)
    {
        return new BattleNpcSpawnRecord(
            new BattleNpcId(row.BattleNpcId),
            row.GroupId,
            row.PoolId,
            new ZoneId(row.ZoneId),
            row.ScriptName,
            row.MinLevel,
            row.MaxLevel,
            row.PositionX,
            row.PositionY,
            row.PositionZ,
            row.Rotation,
            new ProvenanceRef(row.EvidenceStatus, "v1-sql", row.SourceRef, "Imported mapping candidate; not promoted as canonical retail data."));
    }

    public static ActorClassRecord ToActorClassRecord(V1ActorClassRow row)
    {
        return new ActorClassRecord(
            row.ActorClassId,
            row.ClassPath,
            row.DisplayNameId,
            row.PropertyFlags,
            string.IsNullOrWhiteSpace(row.EventConditions) ? "{}" : row.EventConditions,
            row.PushCommand,
            row.PushCommandSub,
            row.PushCommandPriority,
            new ProvenanceRef(row.EvidenceStatus, "v1-sql", row.SourceRef, "Ported from legacy v1 gamedata_actor_class joined to gamedata_actor_pushcommand."));
    }

    public static ActorAppearanceRecord ToActorAppearanceRecord(V1ActorAppearanceRow row)
    {
        return new ActorAppearanceRecord(
            row.ActorClassId,
            row.Base,
            row.Size,
            row.HairStyle,
            row.HairHighlightColor,
            row.HairVariation,
            row.FaceType,
            row.Characteristics,
            row.CharacteristicsColor,
            row.FaceEyebrows,
            row.FaceIrisSize,
            row.FaceEyeShape,
            row.FaceNose,
            row.FaceFeatures,
            row.FaceMouth,
            row.Ears,
            row.HairColor,
            row.SkinColor,
            row.EyeColor,
            row.Voice,
            row.MainHand,
            row.OffHand,
            row.SpMainHand,
            row.SpOffHand,
            row.Throwing,
            row.Pack,
            row.Pouch,
            row.Head,
            row.Body,
            row.Legs,
            row.Hands,
            row.Feet,
            row.Waist,
            row.Neck,
            row.LeftEar,
            row.RightEar,
            row.LeftIndex,
            row.RightIndex,
            row.LeftFinger,
            row.RightFinger,
            new ProvenanceRef(row.EvidenceStatus, "v1-sql", row.SourceRef, "Ported from legacy v1 gamedata_actor_appearance."));
    }

    public static StaticActorSpawnRecord ToStaticActorSpawnRecord(V1StaticActorSpawnRow row)
    {
        return new StaticActorSpawnRecord(
            row.SpawnId,
            row.ActorClassId,
            row.UniqueId,
            new ZoneId(row.ZoneId),
            row.PrivateAreaName,
            row.PrivateAreaLevel,
            row.PositionX,
            row.PositionY,
            row.PositionZ,
            row.Rotation,
            row.ActorState,
            row.AnimationId,
            row.CustomDisplayName,
            new ProvenanceRef(row.EvidenceStatus, "v1-sql", row.SourceRef, "Ported from legacy v1 server_spawn_locations."));
    }
}
