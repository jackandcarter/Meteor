using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbGuildleveDefinitionRepository : IGuildleveDefinitionRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbGuildleveDefinitionRepository(IDatabaseConnectionFactory connectionFactory) =>
        this.connectionFactory = connectionFactory;

    public async Task<GuildleveDefinitionRecord?> GetAsync(
        uint guildleveId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT gd.guildleve_id, gd.class_type, gd.location, gd.faction_credit_required, gd.level,
       gd.aetheryte_id, gd.plate_id, gd.border_id, gd.objective_id, gd.party_recommended,
       gd.target_location, gd.authority_id, gd.time_limit_minutes, gd.skill_id, gd.favor_count,
       go.objective_index, go.required_count, go.item_target_id, go.battle_npc_target_id,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM guildleve_definitions gd
JOIN provenance_refs p ON p.provenance_id = gd.provenance_id
LEFT JOIN guildleve_objectives go ON go.guildleve_id = gd.guildleve_id
WHERE gd.guildleve_id = @guildleve_id
ORDER BY go.objective_index;
""";
        command.Parameters.AddWithValue("@guildleve_id", guildleveId);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        GuildleveDefinitionRecord? definition = null;
        List<GuildleveObjectiveRecord> objectives = [];
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (!reader.IsDBNull(reader.GetOrdinal("objective_index")))
            {
                objectives.Add(new GuildleveObjectiveRecord(
                    reader.GetByte("objective_index"),
                    reader.GetSByte("required_count"),
                    reader.GetUInt32("item_target_id"),
                    reader.GetUInt32("battle_npc_target_id")));
            }

            definition ??= new GuildleveDefinitionRecord(
                reader.GetUInt32("guildleve_id"),
                reader.GetUInt32("class_type"),
                reader.GetUInt32("location"),
                reader.GetUInt16("faction_credit_required"),
                reader.GetUInt16("level"),
                reader.GetUInt32("aetheryte_id"),
                reader.GetUInt32("plate_id"),
                reader.GetUInt32("border_id"),
                reader.GetUInt32("objective_id"),
                reader.GetUInt32("party_recommended"),
                reader.GetUInt32("target_location"),
                reader.GetUInt32("authority_id"),
                reader.GetByte("time_limit_minutes"),
                reader.GetUInt32("skill_id"),
                reader.GetByte("favor_count"),
                objectives,
                new ProvenanceRef(
                    Enum.Parse<EvidenceStatus>(reader.GetString("evidence_status")),
                    reader.GetString("source_type"),
                    reader.GetString("source_ref"),
                    reader.GetString("notes")));
        }

        return definition;
    }
}

public sealed class MariaDbCharacterGuildleveStateRepository : ICharacterGuildleveStateRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterGuildleveStateRepository(IDatabaseConnectionFactory connectionFactory) =>
        this.connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<CharacterGuildleveStateRecord>> ListAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, slot_index, guildleve_id, abandoned, completed, updated_at
FROM character_guildleve_state
WHERE character_id = @character_id
ORDER BY slot_index;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        List<CharacterGuildleveStateRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CharacterGuildleveStateRecord(
                new CharacterId(reader.GetUInt32("character_id")),
                reader.GetByte("slot_index"),
                reader.GetUInt32("guildleve_id"),
                reader.GetBoolean("abandoned"),
                reader.GetBoolean("completed"),
                new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime("updated_at"), DateTimeKind.Utc))));
        }
        return rows;
    }

    public async Task ReplaceAllAsync(
        CharacterId characterId,
        IReadOnlyList<CharacterGuildleveStateRecord> guildleves,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(guildleves);
        if (guildleves.Any(row => row.CharacterId != characterId || row.SlotIndex >= 16))
            throw new ArgumentException("Guildleve snapshot contains an invalid character or slot.", nameof(guildleves));
        if (guildleves.GroupBy(row => row.SlotIndex).Any(group => group.Count() > 1)
            || guildleves.GroupBy(row => row.GuildleveId).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Guildleve snapshot contains duplicate slots or definitions.", nameof(guildleves));
        }

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (MySqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM character_guildleve_state WHERE character_id = @character_id;";
                delete.Parameters.AddWithValue("@character_id", characterId.Value);
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (CharacterGuildleveStateRecord row in guildleves.OrderBy(row => row.SlotIndex))
            {
                await using MySqlCommand insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
INSERT INTO character_guildleve_state
    (character_id, slot_index, guildleve_id, abandoned, completed)
VALUES
    (@character_id, @slot_index, @guildleve_id, @abandoned, @completed);
""";
                insert.Parameters.AddWithValue("@character_id", row.CharacterId.Value);
                insert.Parameters.AddWithValue("@slot_index", row.SlotIndex);
                insert.Parameters.AddWithValue("@guildleve_id", row.GuildleveId);
                insert.Parameters.AddWithValue("@abandoned", row.Abandoned);
                insert.Parameters.AddWithValue("@completed", row.Completed);
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
