using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbRetainerRepository : IRetainerRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbRetainerRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyList<RetainerRecord>> ListForCharacterAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        return ListAsync("cr.character_id = @owner_id", characterId.Value, cancellationToken);
    }

    public Task<IReadOnlyList<RetainerRecord>> ListForAccountAsync(
        AccountId accountId,
        CancellationToken cancellationToken = default)
    {
        return ListAsync("c.account_id = @owner_id", accountId.Value, cancellationToken);
    }

    private async Task<IReadOnlyList<RetainerRecord>> ListAsync(
        string ownerPredicate,
        uint ownerId,
        CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = $"""
SELECT r.retainer_id,
       cr.character_id,
       cr.slot,
       r.name,
       r.actor_class_id,
       cr.content_id_offset,
       cr.place_name_id,
       cr.condition_code,
       cr.level,
       cr.requires_rename
FROM character_retainers cr
JOIN retainers r ON r.retainer_id = cr.retainer_id
JOIN characters c ON c.character_id = cr.character_id
WHERE {ownerPredicate}
  AND c.creation_state = 'Active'
ORDER BY cr.character_id, cr.slot;
""";
        command.Parameters.AddWithValue("@owner_id", ownerId);
        List<RetainerRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new RetainerRecord(
                reader.GetUInt32("retainer_id"),
                new CharacterId(reader.GetUInt32("character_id")),
                reader.GetByte("slot"),
                reader.GetString("name"),
                reader.GetUInt32("actor_class_id"),
                reader.GetByte("content_id_offset"),
                reader.GetUInt16("place_name_id"),
                reader.GetByte("condition_code"),
                reader.GetByte("level"),
                reader.GetBoolean("requires_rename")));
        }
        return rows;
    }
}

public sealed class MariaDbLinkshellRepository : ILinkshellRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbLinkshellRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<LinkshellRecord?> FindByNameAsync(
        WorldId worldId,
        string name,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT linkshell_id, world_id, name, crest_id, master_character_id
FROM linkshells
WHERE world_id = @world_id AND name = @name;
""";
        command.Parameters.AddWithValue("@world_id", worldId.Value);
        command.Parameters.AddWithValue("@name", name);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadLinkshell(reader)
            : null;
    }

    public async Task<IReadOnlyList<(LinkshellRecord Linkshell, LinkshellMemberRecord Membership)>> ListForCharacterAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT ls.linkshell_id, ls.world_id, ls.name, ls.crest_id, ls.master_character_id, lm.rank
FROM linkshell_members lm
JOIN linkshells ls ON ls.linkshell_id = lm.linkshell_id
WHERE lm.character_id = @character_id
ORDER BY lm.joined_at, ls.linkshell_id
LIMIT 8;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        List<(LinkshellRecord, LinkshellMemberRecord)> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ulong linkshellId = reader.GetUInt64("linkshell_id");
            rows.Add((
                new LinkshellRecord(
                    linkshellId,
                    new WorldId(reader.GetUInt32("world_id")),
                    reader.GetString("name"),
                    reader.GetUInt16("crest_id"),
                    new CharacterId(reader.GetUInt32("master_character_id"))),
                new LinkshellMemberRecord(linkshellId, characterId, reader.GetByte("rank"))));
        }
        return rows;
    }

    public async Task<IReadOnlyList<LinkshellMemberRecord>> ListMembersAsync(
        ulong linkshellId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id, rank
FROM linkshell_members
WHERE linkshell_id = @linkshell_id
ORDER BY joined_at, character_id
LIMIT 128;
""";
        command.Parameters.AddWithValue("@linkshell_id", linkshellId);
        List<LinkshellMemberRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new LinkshellMemberRecord(
                linkshellId,
                new CharacterId(reader.GetUInt32("character_id")),
                reader.GetByte("rank")));
        }
        return rows;
    }

    public async Task<ulong?> GetActiveLinkshellIdAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT css.active_linkshell_id
FROM character_social_state css
JOIN linkshell_members lm
  ON lm.linkshell_id = css.active_linkshell_id
 AND lm.character_id = css.character_id
WHERE css.character_id = @character_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToUInt64(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<LinkshellRecord?> CreateAsync(
        WorldId worldId,
        string name,
        ushort crestId,
        CharacterId masterCharacterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using MySqlCommand create = connection.CreateCommand();
            create.Transaction = transaction;
            create.CommandText = """
INSERT INTO linkshells (world_id, name, crest_id, master_character_id)
VALUES (@world_id, @name, @crest_id, @master_character_id);
""";
            create.Parameters.AddWithValue("@world_id", worldId.Value);
            create.Parameters.AddWithValue("@name", name);
            create.Parameters.AddWithValue("@crest_id", crestId);
            create.Parameters.AddWithValue("@master_character_id", masterCharacterId.Value);
            await create.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            ulong linkshellId = checked((ulong)create.LastInsertedId);

            await using MySqlCommand membership = connection.CreateCommand();
            membership.Transaction = transaction;
            membership.CommandText = """
INSERT INTO linkshell_members (linkshell_id, character_id, rank)
VALUES (@linkshell_id, @character_id, 10);
""";
            membership.Parameters.AddWithValue("@linkshell_id", linkshellId);
            membership.Parameters.AddWithValue("@character_id", masterCharacterId.Value);
            await membership.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new LinkshellRecord(linkshellId, worldId, name, crestId, masterCharacterId);
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    public Task<bool> RenameAsync(ulong linkshellId, string name, CancellationToken cancellationToken = default) =>
        ExecuteUpdateAsync(
            "UPDATE linkshells SET name = @value WHERE linkshell_id = @linkshell_id;",
            linkshellId,
            "@value",
            name,
            cancellationToken);

    public Task<bool> UpdateCrestAsync(ulong linkshellId, ushort crestId, CancellationToken cancellationToken = default) =>
        ExecuteUpdateAsync(
            "UPDATE linkshells SET crest_id = @value WHERE linkshell_id = @linkshell_id;",
            linkshellId,
            "@value",
            crestId,
            cancellationToken);

    public async Task<bool> TransferMasterAsync(
        ulong linkshellId,
        CharacterId masterCharacterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand membership = connection.CreateCommand();
        membership.Transaction = transaction;
        membership.CommandText = """
UPDATE linkshell_members
SET rank = 10
WHERE linkshell_id = @linkshell_id AND character_id = @character_id;
""";
        membership.Parameters.AddWithValue("@linkshell_id", linkshellId);
        membership.Parameters.AddWithValue("@character_id", masterCharacterId.Value);
        if (await membership.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        await using MySqlCommand update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = """
UPDATE linkshells
SET master_character_id = @character_id
WHERE linkshell_id = @linkshell_id;
""";
        update.Parameters.AddWithValue("@linkshell_id", linkshellId);
        update.Parameters.AddWithValue("@character_id", masterCharacterId.Value);
        bool changed = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        if (changed)
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        else
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return changed;
    }

    public async Task<bool> DeleteAsync(ulong linkshellId, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM linkshells WHERE linkshell_id = @linkshell_id;";
        command.Parameters.AddWithValue("@linkshell_id", linkshellId);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async Task<bool> AddMemberAsync(
        ulong linkshellId,
        CharacterId characterId,
        byte rank,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
INSERT IGNORE INTO linkshell_members (linkshell_id, character_id, rank)
SELECT @linkshell_id, @character_id, @rank
WHERE (SELECT COUNT(*) FROM linkshell_members WHERE linkshell_id = @linkshell_id) < 128
  AND (SELECT COUNT(*) FROM linkshell_members WHERE character_id = @character_id) < 8;
""";
        command.Parameters.AddWithValue("@linkshell_id", linkshellId);
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        command.Parameters.AddWithValue("@rank", rank);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async Task<bool> RemoveMemberAsync(
        ulong linkshellId,
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
DELETE FROM linkshell_members
WHERE linkshell_id = @linkshell_id AND character_id = @character_id;
""";
        command.Parameters.AddWithValue("@linkshell_id", linkshellId);
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async Task<bool> SetMemberRankAsync(
        ulong linkshellId,
        CharacterId characterId,
        byte rank,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
UPDATE linkshell_members
SET rank = @rank
WHERE linkshell_id = @linkshell_id AND character_id = @character_id;
""";
        command.Parameters.AddWithValue("@linkshell_id", linkshellId);
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        command.Parameters.AddWithValue("@rank", rank);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    public async Task<bool> SetActiveLinkshellAsync(
        CharacterId characterId,
        ulong? linkshellId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO character_social_state (character_id, active_linkshell_id)
SELECT @character_id, @linkshell_id
WHERE @linkshell_id IS NULL
   OR EXISTS (
       SELECT 1 FROM linkshell_members
       WHERE linkshell_id = @linkshell_id AND character_id = @character_id)
ON DUPLICATE KEY UPDATE active_linkshell_id = VALUES(active_linkshell_id);
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        command.Parameters.AddWithValue("@linkshell_id", linkshellId.HasValue ? linkshellId.Value : DBNull.Value);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    private async Task<bool> ExecuteUpdateAsync(
        string sql,
        ulong linkshellId,
        string valueParameter,
        object value,
        CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@linkshell_id", linkshellId);
        command.Parameters.AddWithValue(valueParameter, value);
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
    }

    private static LinkshellRecord ReadLinkshell(MySqlDataReader reader)
    {
        return new LinkshellRecord(
            reader.GetUInt64("linkshell_id"),
            new WorldId(reader.GetUInt32("world_id")),
            reader.GetString("name"),
            reader.GetUInt16("crest_id"),
            new CharacterId(reader.GetUInt32("master_character_id")));
    }
}
