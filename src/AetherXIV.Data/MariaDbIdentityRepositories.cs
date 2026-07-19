using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbAccountRepository : IAccountRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbAccountRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<AccountRecord?> FindBySessionAsync(
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT a.account_id, a.login_name, a.created_at
FROM accounts a
JOIN account_sessions s ON s.account_id = a.account_id
WHERE s.session_token = @session_token;
""";
        command.Parameters.AddWithValue("@session_token", sessionToken);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return ReadAccount(reader);
    }

    private static AccountRecord ReadAccount(MySqlDataReader reader)
    {
        return new AccountRecord(
            new AccountId(reader.GetUInt32("account_id")),
            reader.GetString("login_name"),
            ReadUtcDateTimeOffset(reader, "created_at"));
    }

    private static DateTimeOffset ReadUtcDateTimeOffset(MySqlDataReader reader, string name)
    {
        DateTime value = reader.GetDateTime(name);
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}

public sealed class MariaDbSessionRepository : ISessionRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbSessionRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<SessionRecord?> GetActiveAsync(
        string sessionToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT session_token, account_id, expires_at
FROM account_sessions
WHERE session_token = @session_token
  AND expires_at > UTC_TIMESTAMP();
""";
        command.Parameters.AddWithValue("@session_token", sessionToken);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new SessionRecord(
            reader.GetString("session_token"),
            new AccountId(reader.GetUInt32("account_id")),
            ReadUtcDateTimeOffset(reader, "expires_at"));
    }

    private static DateTimeOffset ReadUtcDateTimeOffset(MySqlDataReader reader, string name)
    {
        DateTime value = reader.GetDateTime(name);
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}

public sealed class MariaDbCharacterRepository : ICharacterRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<CharacterRecord?> GetAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, account_id, world_id, slot, name, current_zone_id,
       position_x, position_y, position_z, rotation
FROM characters
WHERE character_id = @character_id
  AND creation_state = @active_state;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        command.Parameters.AddWithValue("@active_state", CharacterCreationStates.Active);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return ReadCharacter(reader);
    }

    public async Task<IReadOnlyList<CharacterRecord>> ListForAccountAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, account_id, world_id, slot, name, current_zone_id,
       position_x, position_y, position_z, rotation
FROM characters
WHERE account_id = @account_id
  AND creation_state = @active_state
ORDER BY slot, character_id;
""";
        command.Parameters.AddWithValue("@account_id", accountId.Value);
        command.Parameters.AddWithValue("@active_state", CharacterCreationStates.Active);

        List<CharacterRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(ReadCharacter(reader));

        return rows;
    }

    private static CharacterRecord ReadCharacter(MySqlDataReader reader)
    {
        return new CharacterRecord(
            new CharacterId(reader.GetUInt32("character_id")),
            new AccountId(reader.GetUInt32("account_id")),
            new WorldId(reader.GetUInt32("world_id")),
            reader.GetString("name"),
            new ZoneId(reader.GetUInt32("current_zone_id")),
            reader.GetFloat("position_x"),
            reader.GetFloat("position_y"),
            reader.GetFloat("position_z"),
            reader.GetFloat("rotation"),
            reader.GetUInt16("slot"));
    }
}

public sealed class MariaDbWorldRepository : IWorldRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbWorldRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<WorldRecord?> GetAsync(
        WorldId worldId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT world_id, name, host, port
FROM worlds
WHERE world_id = @world_id;
""";
        command.Parameters.AddWithValue("@world_id", worldId.Value);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return ReadWorld(reader);
    }

    public async Task<IReadOnlyList<WorldRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT world_id, name, host, port
FROM worlds
ORDER BY world_id;
""";

        List<WorldRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(ReadWorld(reader));

        return rows;
    }

    private static WorldRecord ReadWorld(MySqlDataReader reader)
    {
        return new WorldRecord(
            new WorldId(reader.GetUInt32("world_id")),
            reader.GetString("name"),
            new ServerEndpoint(reader.GetString("host"), reader.GetUInt16("port")));
    }
}

public sealed class MariaDbRepositorySet : IAetherXivRepositorySet
{
    public MariaDbRepositorySet(IDatabaseConnectionFactory connectionFactory)
    {
        Accounts = new MariaDbAccountRepository(connectionFactory);
        Sessions = new MariaDbSessionRepository(connectionFactory);
        Characters = new MariaDbCharacterRepository(connectionFactory);
        CharacterCreation = new MariaDbCharacterCreationRepository(connectionFactory);
        CharacterAppearances = new MariaDbCharacterAppearancePayloadRepository(connectionFactory);
        CharacterLoadouts = new MariaDbCharacterLoadoutRepository(connectionFactory);
        ItemVisuals = new MariaDbItemVisualRepository(connectionFactory);
        WeaponCombatProfiles = new MariaDbWeaponCombatProfileRepository(connectionFactory);
        PlayerStats = new MariaDbPlayerStatRepository(connectionFactory);
        PlayerBaseStatProfiles = new MariaDbPlayerBaseStatProfileRepository(connectionFactory);
        CharacterProfiles = new MariaDbCharacterProfileRepository(connectionFactory);
        CharacterResources = new MariaDbCharacterResourceStateRepository(connectionFactory);
        CharacterLoginStates = new MariaDbCharacterLoginStateRepository(connectionFactory);
        CharacterActionStates = new MariaDbCharacterActionStateRepository(connectionFactory);
        CharacterNpcLinkshellStates = new MariaDbCharacterNpcLinkshellStateRepository(connectionFactory);
        Retainers = new MariaDbRetainerRepository(connectionFactory);
        Linkshells = new MariaDbLinkshellRepository(connectionFactory);
        CharacterFriends = new MariaDbCharacterFriendRepository(connectionFactory);
        Worlds = new MariaDbWorldRepository(connectionFactory);
        HandoffTickets = new MariaDbWorldHandoffTicketRepository(connectionFactory);
        WorldLoginHandoffs = new MariaDbWorldLoginHandoffRepository(connectionFactory);
        MapCharacterStates = new MariaDbMapCharacterStateRepository(connectionFactory);
        CharacterProgression = new MariaDbCharacterProgressionRepository(connectionFactory);
        CharacterQuests = new MariaDbCharacterQuestStateRepository(connectionFactory);
        QuestDefinitions = new MariaDbQuestDefinitionRepository(connectionFactory);
        TutorialCheckpoints = new MariaDbTutorialCheckpointRepository(connectionFactory);
        GuildleveDefinitions = new MariaDbGuildleveDefinitionRepository(connectionFactory);
        CharacterGuildleves = new MariaDbCharacterGuildleveStateRepository(connectionFactory);
        CharacterClassAttributeAllocations = new MariaDbCharacterClassAttributeAllocationRepository(connectionFactory);
        Zones = new MariaDbZoneRepository(connectionFactory);
        ZoneSeamlessBoundaries = new MariaDbZoneSeamlessBoundaryRepository(connectionFactory);
        ZoneEntrances = new MariaDbZoneEntranceRepository(connectionFactory);
        PrivateAreas = new MariaDbPrivateAreaRepository(connectionFactory);
        ActorClasses = new MariaDbActorClassRepository(connectionFactory);
        ActorAppearances = new MariaDbActorAppearanceRepository(connectionFactory);
        ActorSpawns = new MariaDbActorSpawnRepository(connectionFactory);
        BattleCommands = new MariaDbBattleCommandRepository(connectionFactory);
        BattleCommandScripts = new MariaDbBattleCommandScriptRepository(connectionFactory);
        BattleTraits = new MariaDbBattleTraitRepository(connectionFactory);
        StatusEffects = new MariaDbStatusEffectDefinitionRepository(connectionFactory);
        CharacterBattleState = new MariaDbCharacterBattleStateRepository(connectionFactory);
        BattleNpcStats = new MariaDbBattleNpcStatRepository(connectionFactory);
        BattleNpcActions = new MariaDbBattleNpcActionRepository(connectionFactory);
    }

    public IAccountRepository Accounts { get; }

    public ISessionRepository Sessions { get; }

    public ICharacterRepository Characters { get; }

    public ICharacterCreationRepository CharacterCreation { get; }

    public ICharacterAppearanceRepository CharacterAppearances { get; }

    public ICharacterLoadoutRepository CharacterLoadouts { get; }

    public IItemVisualRepository ItemVisuals { get; }

    public IWeaponCombatProfileRepository WeaponCombatProfiles { get; }

    public IPlayerStatRepository PlayerStats { get; }

    public IPlayerBaseStatProfileRepository PlayerBaseStatProfiles { get; }

    public ICharacterProfileRepository CharacterProfiles { get; }

    public ICharacterResourceStateRepository CharacterResources { get; }

    public ICharacterLoginStateRepository CharacterLoginStates { get; }

    public ICharacterActionStateRepository CharacterActionStates { get; }

    public ICharacterNpcLinkshellStateRepository CharacterNpcLinkshellStates { get; }

    public IRetainerRepository Retainers { get; }

    public ILinkshellRepository Linkshells { get; }

    public ICharacterFriendRepository CharacterFriends { get; }

    public IWorldRepository Worlds { get; }

    public IWorldHandoffTicketRepository HandoffTickets { get; }

    public IWorldLoginHandoffRepository WorldLoginHandoffs { get; }

    public IMapCharacterStateRepository MapCharacterStates { get; }

    public ICharacterProgressionRepository CharacterProgression { get; }

    public ICharacterQuestStateRepository CharacterQuests { get; }

    public IQuestDefinitionRepository QuestDefinitions { get; }

    public ITutorialCheckpointRepository TutorialCheckpoints { get; }

    public IGuildleveDefinitionRepository GuildleveDefinitions { get; }

    public ICharacterGuildleveStateRepository CharacterGuildleves { get; }

    public ICharacterClassAttributeAllocationRepository CharacterClassAttributeAllocations { get; }

    public IZoneRepository Zones { get; }

    public IZoneSeamlessBoundaryRepository ZoneSeamlessBoundaries { get; }

    public IZoneEntranceRepository ZoneEntrances { get; }

    public IPrivateAreaRepository PrivateAreas { get; }

    public IActorClassRepository ActorClasses { get; }

    public IActorAppearanceRepository ActorAppearances { get; }

    public IActorSpawnRepository ActorSpawns { get; }

    public IBattleCommandRepository BattleCommands { get; }

    public IBattleCommandScriptRepository BattleCommandScripts { get; }

    public IBattleTraitRepository BattleTraits { get; }

    public IStatusEffectDefinitionRepository StatusEffects { get; }

    public ICharacterBattleStateRepository CharacterBattleState { get; }

    public IBattleNpcStatRepository BattleNpcStats { get; }

    public IBattleNpcActionRepository BattleNpcActions { get; }
}
