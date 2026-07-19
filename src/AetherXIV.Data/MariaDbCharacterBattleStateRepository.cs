using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbCharacterBattleStateRepository : ICharacterBattleStateRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterBattleStateRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CharacterCommandRecastRecord>> ListRecastsAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT command_id, ready_at
FROM character_command_recasts
WHERE character_id = @character_id
  AND ready_at > UTC_TIMESTAMP(3)
ORDER BY command_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);

        List<CharacterCommandRecastRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CharacterCommandRecastRecord(
                characterId,
                reader.GetUInt16("command_id"),
                ToUtcOffset(reader.GetDateTime("ready_at"))));
        }

        return rows;
    }

    public async Task<IReadOnlyList<CharacterStatusEffectRecord>> ListStatusEffectsAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT status_effect_id, source_actor_id, magnitude, tier, extra, expires_at, next_tick_at
FROM character_status_effects
WHERE character_id = @character_id
  AND (expires_at IS NULL OR expires_at > UTC_TIMESTAMP(3))
ORDER BY status_effect_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);

        List<CharacterStatusEffectRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CharacterStatusEffectRecord(
                characterId,
                reader.GetUInt32("status_effect_id"),
                new ActorId(reader.GetUInt32("source_actor_id")),
                reader.GetDouble("magnitude"),
                reader.GetByte("tier"),
                reader.GetInt32("extra"),
                ReadNullableTimestamp(reader, "expires_at"),
                ReadNullableTimestamp(reader, "next_tick_at")));
        }

        return rows;
    }

    public async Task SaveAsync(
        CharacterId characterId,
        IReadOnlyList<CharacterCommandRecastRecord> recasts,
        IReadOnlyList<CharacterStatusEffectRecord> statusEffects,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recasts);
        ArgumentNullException.ThrowIfNull(statusEffects);

        await using MySqlConnection connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await DeleteAsync(connection, transaction, "character_command_recasts", characterId, cancellationToken)
            .ConfigureAwait(false);
        await DeleteAsync(connection, transaction, "character_status_effects", characterId, cancellationToken)
            .ConfigureAwait(false);

        foreach (CharacterCommandRecastRecord recast in recasts)
        {
            await using MySqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
INSERT INTO character_command_recasts (character_id, command_id, ready_at)
VALUES (@character_id, @command_id, @ready_at);
""";
            command.Parameters.AddWithValue("@character_id", characterId.Value);
            command.Parameters.AddWithValue("@command_id", recast.CommandId);
            command.Parameters.AddWithValue("@ready_at", recast.ReadyAt.UtcDateTime);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (CharacterStatusEffectRecord effect in statusEffects)
        {
            await using MySqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
INSERT INTO character_status_effects (
  character_id, status_effect_id, source_actor_id, magnitude, tier, extra, expires_at, next_tick_at
)
VALUES (
  @character_id, @status_effect_id, @source_actor_id, @magnitude, @tier, @extra, @expires_at, @next_tick_at
);
""";
            command.Parameters.AddWithValue("@character_id", characterId.Value);
            command.Parameters.AddWithValue("@status_effect_id", effect.StatusEffectId);
            command.Parameters.AddWithValue("@source_actor_id", effect.SourceActorId.Value);
            command.Parameters.AddWithValue("@magnitude", effect.Magnitude);
            command.Parameters.AddWithValue("@tier", effect.Tier);
            command.Parameters.AddWithValue("@extra", effect.Extra);
            command.Parameters.AddWithValue("@expires_at", effect.ExpiresAt?.UtcDateTime);
            command.Parameters.AddWithValue("@next_tick_at", effect.NextTickAt?.UtcDateTime);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string table,
        CharacterId characterId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"DELETE FROM {table} WHERE character_id = @character_id;";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static DateTimeOffset? ReadNullableTimestamp(MySqlDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : ToUtcOffset(reader.GetDateTime(name));

    private static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
