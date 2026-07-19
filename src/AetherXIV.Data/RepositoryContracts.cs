using AetherXIV.Core;

namespace AetherXIV.Data;

public interface IAccountRepository
{
    Task<AccountRecord?> FindBySessionAsync(string sessionToken, CancellationToken cancellationToken = default);
}

public interface ISessionRepository
{
    Task<SessionRecord?> GetActiveAsync(string sessionToken, CancellationToken cancellationToken = default);
}

public interface ICharacterRepository
{
    Task<CharacterRecord?> GetAsync(CharacterId characterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CharacterRecord>> ListForAccountAsync(AccountId accountId, CancellationToken cancellationToken = default);
}

public interface ICharacterCreationRepository
{
    Task<CharacterReservationResult> ReserveAsync(CharacterReservationRequest request, CancellationToken cancellationToken = default);

    Task<CharacterReservationRecord?> GetReservationAsync(AccountId accountId, CharacterId characterId, CancellationToken cancellationToken = default);

    Task<CharacterReservationRecord?> GetReservationForAccountAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);

    Task<CharacterCreationResult> CreateAsync(CharacterCreateRequest request, CancellationToken cancellationToken = default);

    Task<CharacterRenameStatus> RenameAsync(
        AccountId accountId,
        CharacterId characterId,
        WorldId worldId,
        string newName,
        CancellationToken cancellationToken = default);

    Task<CharacterDeleteStatus> DeleteAsync(
        AccountId accountId,
        CharacterId characterId,
        string expectedName,
        CancellationToken cancellationToken = default);
}

public interface ICharacterAppearanceRepository
{
    Task<ReadOnlyMemory<byte>?> GetLobbyPayloadAsync(CharacterId characterId, CancellationToken cancellationToken = default);

    Task<CharacterAppearanceRecord?> GetAppearanceAsync(CharacterId characterId, CancellationToken cancellationToken = default);
}

public interface ICharacterLoadoutRepository
{
    Task<IReadOnlyList<CharacterClassStateRecord>> ListClassStatesAsync(CharacterId characterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CharacterEquipmentSlotRecord>> ListEquipmentAsync(CharacterId characterId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CharacterInventoryItemRecord>> ListInventoryAsync(CharacterId characterId, CancellationToken cancellationToken = default);
}

public interface ICharacterLoadoutMutationRepository
{
    Task SaveClassStatesAsync(
        IReadOnlyList<CharacterClassStateRecord> states,
        CancellationToken cancellationToken = default);

    Task SaveInventoryItemsAsync(
        IReadOnlyList<CharacterInventoryItemRecord> items,
        CancellationToken cancellationToken = default);

    Task SaveEquipmentSlotsAsync(
        IReadOnlyList<CharacterEquipmentSlotRecord> slots,
        CancellationToken cancellationToken = default);
}

public interface IItemVisualRepository
{
    Task<ItemVisualRecord?> GetAsync(uint itemId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<uint, ItemVisualRecord>> ListAsync(
        IEnumerable<uint> itemIds,
        CancellationToken cancellationToken = default);
}

public interface IWeaponCombatProfileRepository
{
    Task<WeaponCombatProfileRecord?> GetAsync(
        uint itemId,
        CancellationToken cancellationToken = default);
}

public interface IPlayerStatRepository
{
    Task<IReadOnlyList<PlayerStatRecord>> ListStatsAsync(CharacterId characterId, CancellationToken cancellationToken = default);
}

public interface IPlayerBaseStatProfileRepository
{
    Task<PlayerBaseStatProfileRecord?> GetAsync(
        byte classJob,
        byte tribe,
        ushort level,
        CancellationToken cancellationToken = default);
}

public interface ICharacterProfileRepository
{
    Task<CharacterProfileRecord?> GetAsync(CharacterId characterId, CancellationToken cancellationToken = default);
}

public interface ICharacterResourceStateRepository
{
    Task<CharacterResourceStateRecord?> GetAsync(CharacterId characterId, CancellationToken cancellationToken = default);

    Task SaveAsync(CharacterResourceStateRecord state, CancellationToken cancellationToken = default);
}

public interface ICharacterLoginStateRepository
{
    Task<CharacterLoginStateRecord> GetAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default);
}

public interface ICharacterActionStateRepository
{
    Task<CharacterActionStateRecord> GetAsync(
        CharacterId characterId,
        byte currentClassId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        CharacterId characterId,
        byte currentClassId,
        CharacterActionStateRecord state,
        CancellationToken cancellationToken = default);
}

public interface ICharacterNpcLinkshellStateRepository
{
    Task<IReadOnlyList<CharacterNpcLinkshellStateRecord>> ListAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        CharacterNpcLinkshellStateRecord state,
        CancellationToken cancellationToken = default);
}

public interface IWorldRepository
{
    Task<WorldRecord?> GetAsync(WorldId worldId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorldRecord>> ListAsync(CancellationToken cancellationToken = default);
}

public interface IWorldHandoffTicketRepository
{
    Task<MapHandoffTicketRecord> CreateAsync(CharacterId characterId, WorldId worldId, ZoneId zoneId, ServerEndpoint mapEndpoint, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    Task<MapHandoffTicketRecord?> ConsumeAsync(string ticket, CancellationToken cancellationToken = default);
}

public interface IWorldLoginHandoffRepository
{
    Task<WorldLoginHandoffRecord> CreateOrRefreshAsync(
        CharacterId characterId,
        string sessionToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);

    Task<WorldLoginHandoffRecord?> GetActiveAsync(
        uint runtimeActorId,
        CancellationToken cancellationToken = default);
}

public interface IMapCharacterStateRepository
{
    Task<CharacterMapStateRecord?> GetAsync(CharacterId characterId, CancellationToken cancellationToken = default);

    Task SaveAsync(CharacterMapStateRecord state, CancellationToken cancellationToken = default);
}

public interface ICharacterProgressionRepository
{
    Task<CharacterProgressionStateRecord?> GetAsync(CharacterId characterId, CancellationToken cancellationToken = default);

    Task SaveAsync(CharacterProgressionStateRecord state, CancellationToken cancellationToken = default);
}

public interface ICharacterQuestStateRepository
{
    Task<IReadOnlyList<CharacterQuestStateRecord>> ListAsync(CharacterId characterId, CancellationToken cancellationToken = default);

    Task SaveAsync(CharacterQuestStateRecord quest, CancellationToken cancellationToken = default);
}

public interface ICharacterQuestSnapshotRepository : ICharacterQuestStateRepository
{
    Task ReplaceAllAsync(
        CharacterId characterId,
        IReadOnlyList<CharacterQuestStateRecord> quests,
        CancellationToken cancellationToken = default);
}

public interface IQuestDefinitionRepository
{
    Task<IReadOnlyList<QuestDefinitionRecord>> ListAsync(CancellationToken cancellationToken = default);

    Task ReplaceCatalogAsync(
        IReadOnlyList<QuestDefinitionRecord> definitions,
        string seedVersion,
        string contentHash,
        CancellationToken cancellationToken = default);
}

public interface ICharacterQuestProgressionRepository
{
    Task CommitAsync(
        CharacterQuestProgressionCommit commit,
        CancellationToken cancellationToken = default);
}

public interface ITutorialCheckpointRepository
{
    Task<TutorialCheckpointRecord?> GetAsync(CharacterId characterId, string directorName, CancellationToken cancellationToken = default);

    Task SaveAsync(TutorialCheckpointRecord checkpoint, CancellationToken cancellationToken = default);
}

public interface IGuildleveDefinitionRepository
{
    Task<GuildleveDefinitionRecord?> GetAsync(uint guildleveId, CancellationToken cancellationToken = default);
}

public interface ICharacterGuildleveStateRepository
{
    Task<IReadOnlyList<CharacterGuildleveStateRecord>> ListAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default);

    Task ReplaceAllAsync(
        CharacterId characterId,
        IReadOnlyList<CharacterGuildleveStateRecord> guildleves,
        CancellationToken cancellationToken = default);
}

public interface ICharacterClassAttributeAllocationRepository
{
    Task<IReadOnlyList<CharacterClassAttributeAllocationRecord>> ListAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default);

    Task ReplaceAllAsync(
        CharacterId characterId,
        IReadOnlyList<CharacterClassAttributeAllocationRecord> allocations,
        CancellationToken cancellationToken = default);
}

public interface IZoneRepository
{
    Task<ZoneRecord?> GetAsync(ZoneId zoneId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZoneRecord>> ListForWorldAsync(WorldId worldId, CancellationToken cancellationToken = default);
}

public interface IZoneSeamlessBoundaryRepository
{
    Task<IReadOnlyList<ZoneSeamlessBoundaryRecord>> ListAllAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ZoneSeamlessBoundaryRecord>> ListByRegionAsync(
        uint regionId,
        CancellationToken cancellationToken = default);
}

public interface IZoneEntranceRepository
{
    Task<ZoneEntranceRecord?> GetAsync(uint entranceId, CancellationToken cancellationToken = default);
}

public interface IPrivateAreaRepository
{
    Task<PrivateAreaRecord?> GetAsync(
        ZoneId parentZoneId,
        string name,
        uint level,
        CancellationToken cancellationToken = default);
}

public interface IActorClassRepository
{
    Task<ActorClassRecord?> GetAsync(uint actorClassId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActorClassRecord>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ActorClassRecord>>([]);
}

public interface IActorAppearanceRepository
{
    Task<ActorAppearanceRecord?> GetAsync(uint actorClassId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActorAppearanceRecord>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ActorAppearanceRecord>>([]);
}

public interface IActorSpawnRepository
{
    Task<IReadOnlyList<StaticActorSpawnRecord>> ListStaticSpawnsAsync(ZoneId zoneId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BattleNpcSpawnRecord>> ListBattleNpcSpawnsAsync(ZoneId zoneId, CancellationToken cancellationToken = default);
}

public interface IBattleCommandRepository
{
    Task<BattleCommandRecord?> GetAsync(ushort commandId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BattleCommandRecord>> ListForClassLevelAsync(byte classJob, ushort level, CancellationToken cancellationToken = default);
}

public interface IBattleCommandScriptRepository
{
    Task<BattleCommandScriptRecord?> GetAsync(ushort commandId, CancellationToken cancellationToken = default);
}

public interface IBattleTraitRepository
{
    Task<IReadOnlyList<BattleTraitRecord>> ListForClassLevelAsync(
        byte classJob,
        ushort level,
        CancellationToken cancellationToken = default);
}

public interface IStatusEffectDefinitionRepository
{
    Task<IReadOnlyList<StatusEffectDefinitionRecord>> ListAsync(CancellationToken cancellationToken = default);
}

public interface ICharacterBattleStateRepository
{
    Task<IReadOnlyList<CharacterCommandRecastRecord>> ListRecastsAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CharacterStatusEffectRecord>> ListStatusEffectsAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        CharacterId characterId,
        IReadOnlyList<CharacterCommandRecastRecord> recasts,
        IReadOnlyList<CharacterStatusEffectRecord> statusEffects,
        CancellationToken cancellationToken = default);
}

public interface IBattleNpcStatRepository
{
    Task<IReadOnlyList<BattleNpcStatRecord>> ListForBattleNpcAsync(BattleNpcId battleNpcId, CancellationToken cancellationToken = default);
}

public interface IBattleNpcActionRepository
{
    Task<IReadOnlyList<BattleNpcActionRecord>> ListForActionListAsync(uint listId, CancellationToken cancellationToken = default);
}

public interface IRetainerRepository
{
    Task<IReadOnlyList<RetainerRecord>> ListForCharacterAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RetainerRecord>> ListForAccountAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default);
}

public interface ILinkshellRepository
{
    Task<LinkshellRecord?> FindByNameAsync(
        WorldId worldId,
        string name,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(LinkshellRecord Linkshell, LinkshellMemberRecord Membership)>> ListForCharacterAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LinkshellMemberRecord>> ListMembersAsync(
        ulong linkshellId,
        CancellationToken cancellationToken = default);

    Task<ulong?> GetActiveLinkshellIdAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default);

    Task<LinkshellRecord?> CreateAsync(
        WorldId worldId,
        string name,
        ushort crestId,
        CharacterId masterCharacterId,
        CancellationToken cancellationToken = default);

    Task<bool> RenameAsync(ulong linkshellId, string name, CancellationToken cancellationToken = default);

    Task<bool> UpdateCrestAsync(ulong linkshellId, ushort crestId, CancellationToken cancellationToken = default);

    Task<bool> TransferMasterAsync(ulong linkshellId, CharacterId masterCharacterId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(ulong linkshellId, CancellationToken cancellationToken = default);

    Task<bool> AddMemberAsync(ulong linkshellId, CharacterId characterId, byte rank, CancellationToken cancellationToken = default);

    Task<bool> RemoveMemberAsync(ulong linkshellId, CharacterId characterId, CancellationToken cancellationToken = default);

    Task<bool> SetMemberRankAsync(ulong linkshellId, CharacterId characterId, byte rank, CancellationToken cancellationToken = default);

    Task<bool> SetActiveLinkshellAsync(CharacterId characterId, ulong? linkshellId, CancellationToken cancellationToken = default);
}

public interface ICharacterFriendRepository
{
    Task<IReadOnlyList<CharacterFriendRecord>> ListAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default);
}

public interface IAetherXivRepositorySet
{
    IAccountRepository Accounts { get; }

    ISessionRepository Sessions { get; }

    ICharacterRepository Characters { get; }

    ICharacterCreationRepository CharacterCreation { get; }

    ICharacterAppearanceRepository CharacterAppearances { get; }

    ICharacterLoadoutRepository CharacterLoadouts { get; }

    IItemVisualRepository ItemVisuals { get; }

    IWeaponCombatProfileRepository WeaponCombatProfiles { get; }

    IPlayerStatRepository PlayerStats { get; }

    IPlayerBaseStatProfileRepository PlayerBaseStatProfiles { get; }

    ICharacterProfileRepository CharacterProfiles { get; }

    ICharacterResourceStateRepository CharacterResources { get; }

    ICharacterLoginStateRepository CharacterLoginStates { get; }

    ICharacterActionStateRepository CharacterActionStates { get; }

    ICharacterNpcLinkshellStateRepository CharacterNpcLinkshellStates { get; }

    IRetainerRepository Retainers { get; }

    ILinkshellRepository Linkshells { get; }

    ICharacterFriendRepository CharacterFriends { get; }

    IWorldRepository Worlds { get; }

    IWorldHandoffTicketRepository HandoffTickets { get; }

    IWorldLoginHandoffRepository WorldLoginHandoffs { get; }

    IMapCharacterStateRepository MapCharacterStates { get; }

    ICharacterProgressionRepository CharacterProgression { get; }

    ICharacterQuestStateRepository CharacterQuests { get; }

    IQuestDefinitionRepository QuestDefinitions { get; }

    ITutorialCheckpointRepository TutorialCheckpoints { get; }

    IGuildleveDefinitionRepository GuildleveDefinitions { get; }

    ICharacterGuildleveStateRepository CharacterGuildleves { get; }

    ICharacterClassAttributeAllocationRepository CharacterClassAttributeAllocations { get; }

    IZoneRepository Zones { get; }

    IZoneSeamlessBoundaryRepository ZoneSeamlessBoundaries { get; }

    IZoneEntranceRepository ZoneEntrances { get; }

    IPrivateAreaRepository PrivateAreas { get; }

    IActorClassRepository ActorClasses { get; }

    IActorAppearanceRepository ActorAppearances { get; }

    IActorSpawnRepository ActorSpawns { get; }

    IBattleCommandRepository BattleCommands { get; }

    IBattleCommandScriptRepository BattleCommandScripts { get; }

    IBattleTraitRepository BattleTraits { get; }

    IStatusEffectDefinitionRepository StatusEffects { get; }

    ICharacterBattleStateRepository CharacterBattleState { get; }

    IBattleNpcStatRepository BattleNpcStats { get; }

    IBattleNpcActionRepository BattleNpcActions { get; }
}
