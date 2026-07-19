using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbCharacterClassAttributeAllocationRepository
    : ICharacterClassAttributeAllocationRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterClassAttributeAllocationRepository(IDatabaseConnectionFactory connectionFactory) =>
        this.connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<CharacterClassAttributeAllocationRecord>> ListAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT character_id,class_id,points_remaining,strength,vitality,dexterity,intelligence,mind,piety,updated_at
FROM character_class_attribute_allocations
WHERE character_id=@character_id
ORDER BY class_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        List<CharacterClassAttributeAllocationRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CharacterClassAttributeAllocationRecord(
                new CharacterId(reader.GetUInt32("character_id")), reader.GetByte("class_id"),
                reader.GetUInt16("points_remaining"), reader.GetUInt16("strength"), reader.GetUInt16("vitality"),
                reader.GetUInt16("dexterity"), reader.GetUInt16("intelligence"), reader.GetUInt16("mind"),
                reader.GetUInt16("piety"),
                new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime("updated_at"), DateTimeKind.Utc))));
        }
        return rows;
    }

    public async Task ReplaceAllAsync(
        CharacterId characterId,
        IReadOnlyList<CharacterClassAttributeAllocationRecord> allocations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(allocations);
        if (allocations.Any(row => row.CharacterId != characterId || row.ClassId is < 1 or > 52)
            || allocations.GroupBy(row => row.ClassId).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Class attribute allocation snapshot has an invalid owner or class.", nameof(allocations));
        }
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (MySqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM character_class_attribute_allocations WHERE character_id=@character_id;";
                delete.Parameters.AddWithValue("@character_id", characterId.Value);
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            foreach (CharacterClassAttributeAllocationRecord row in allocations.OrderBy(row => row.ClassId))
            {
                await using MySqlCommand insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
INSERT INTO character_class_attribute_allocations
(character_id,class_id,points_remaining,strength,vitality,dexterity,intelligence,mind,piety)
VALUES (@character_id,@class_id,@points_remaining,@strength,@vitality,@dexterity,@intelligence,@mind,@piety);
""";
                insert.Parameters.AddWithValue("@character_id", row.CharacterId.Value);
                insert.Parameters.AddWithValue("@class_id", row.ClassId);
                insert.Parameters.AddWithValue("@points_remaining", row.PointsRemaining);
                insert.Parameters.AddWithValue("@strength", row.Strength);
                insert.Parameters.AddWithValue("@vitality", row.Vitality);
                insert.Parameters.AddWithValue("@dexterity", row.Dexterity);
                insert.Parameters.AddWithValue("@intelligence", row.Intelligence);
                insert.Parameters.AddWithValue("@mind", row.Mind);
                insert.Parameters.AddWithValue("@piety", row.Piety);
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
