using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbCharacterActionStateRepository : ICharacterActionStateRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterActionStateRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<CharacterActionStateRecord> GetAsync(
        CharacterId characterId,
        byte currentClassId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        List<CharacterHotbarSlotRecord> hotbar = [];
        await using (MySqlCommand command = connection.CreateCommand())
        {
            command.CommandText = """
SELECT slots.slot_index, slots.command_id, slots.recast_end,
       COALESCE(commands.recast_time_ms, 1000) AS recast_time_ms
FROM character_hotbar_slots AS slots
LEFT JOIN battle_commands AS commands ON commands.command_id = slots.command_id
WHERE slots.character_id = @character_id AND slots.class_id = @class_id
ORDER BY slots.slot_index;
""";
            command.Parameters.AddWithValue("@character_id", characterId.Value);
            command.Parameters.AddWithValue("@class_id", currentClassId);
            await using MySqlDataReader reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                uint recastMilliseconds = reader.GetUInt32("recast_time_ms");
                ushort maximumRecastSeconds = checked((ushort)Math.Clamp(
                    (recastMilliseconds + 999u) / 1000u,
                    1u,
                    UInt16.MaxValue));
                hotbar.Add(new CharacterHotbarSlotRecord(
                    characterId,
                    currentClassId,
                    reader.GetByte("slot_index"),
                    reader.GetUInt32("command_id"),
                    reader.GetUInt32("recast_end"),
                    maximumRecastSeconds));
            }
        }

        List<CharacterTimerStateRecord> timers = [];
        await using (MySqlCommand command = connection.CreateCommand())
        {
            command.CommandText = """
SELECT timer_index, timer_value
FROM character_timers
WHERE character_id = @character_id
ORDER BY timer_index;
""";
            command.Parameters.AddWithValue("@character_id", characterId.Value);
            await using MySqlDataReader reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                timers.Add(new CharacterTimerStateRecord(
                    characterId,
                    reader.GetByte("timer_index"),
                    reader.GetUInt32("timer_value")));
            }
        }

        return new CharacterActionStateRecord(hotbar, timers);
    }

    public async Task SaveAsync(
        CharacterId characterId,
        byte currentClassId,
        CharacterActionStateRecord state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.HotbarSlots.Any(slot => slot.CharacterId != characterId
            || slot.ClassId != currentClassId
            || slot.SlotIndex >= 30))
        {
            throw new ArgumentException("Hotbar state contains a row outside the requested character, class, or slot range.", nameof(state));
        }
        if (state.Timers.Any(timer => timer.CharacterId != characterId || timer.TimerIndex >= 20))
            throw new ArgumentException("Timer state contains a row outside the requested character or timer range.", nameof(state));

        await using MySqlConnection connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await DeleteClassHotbarAsync(connection, transaction, characterId, currentClassId, cancellationToken)
                .ConfigureAwait(false);
            foreach (CharacterHotbarSlotRecord slot in state.HotbarSlots.Where(slot => slot.CommandId != 0))
            {
                await InsertHotbarSlotAsync(connection, transaction, slot, cancellationToken)
                    .ConfigureAwait(false);
            }

            await DeleteTimersAsync(connection, transaction, characterId, cancellationToken).ConfigureAwait(false);
            foreach (CharacterTimerStateRecord timer in state.Timers.Where(timer => timer.Value != 0))
            {
                await InsertTimerAsync(connection, transaction, timer, cancellationToken)
                    .ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task DeleteClassHotbarAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterId characterId,
        byte currentClassId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
DELETE FROM character_hotbar_slots
WHERE character_id = @character_id AND class_id = @class_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        command.Parameters.AddWithValue("@class_id", currentClassId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertHotbarSlotAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterHotbarSlotRecord slot,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_hotbar_slots
    (character_id, class_id, slot_index, command_id, recast_end)
VALUES
    (@character_id, @class_id, @slot_index, @command_id, @recast_end);
""";
        command.Parameters.AddWithValue("@character_id", slot.CharacterId.Value);
        command.Parameters.AddWithValue("@class_id", slot.ClassId);
        command.Parameters.AddWithValue("@slot_index", slot.SlotIndex);
        command.Parameters.AddWithValue("@command_id", slot.CommandId);
        command.Parameters.AddWithValue("@recast_end", slot.RecastEnd);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteTimersAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterId characterId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM character_timers WHERE character_id = @character_id;";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertTimerAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterTimerStateRecord timer,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_timers (character_id, timer_index, timer_value)
VALUES (@character_id, @timer_index, @timer_value);
""";
        command.Parameters.AddWithValue("@character_id", timer.CharacterId.Value);
        command.Parameters.AddWithValue("@timer_index", timer.TimerIndex);
        command.Parameters.AddWithValue("@timer_value", timer.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
