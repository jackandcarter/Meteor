using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbCharacterLoginStateRepository : ICharacterLoginStateRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterLoginStateRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<CharacterLoginStateRecord> GetAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        CharacterLoginStateRecord state = CharacterLoginStateRecord.NewCharacter(characterId);
        await using MySqlConnection connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using (MySqlCommand command = connection.CreateCommand())
        {
            command.CommandText = """
SELECT grand_company_current, grand_company_limsa_rank, grand_company_gridania_rank,
       grand_company_uldah_rank, current_title, current_job, special_event_type,
       special_event_id, achievement_points
FROM character_login_state
WHERE character_id = @character_id;
""";
            command.Parameters.AddWithValue("@character_id", characterId.Value);
            await using MySqlDataReader reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                state = state with
                {
                    GrandCompanyCurrent = reader.GetByte("grand_company_current"),
                    GrandCompanyLimsaRank = reader.GetByte("grand_company_limsa_rank"),
                    GrandCompanyGridaniaRank = reader.GetByte("grand_company_gridania_rank"),
                    GrandCompanyUldahRank = reader.GetByte("grand_company_uldah_rank"),
                    CurrentTitle = reader.GetUInt32("current_title"),
                    CurrentJob = reader.GetByte("current_job"),
                    SpecialEventType = reader.GetUInt16("special_event_type"),
                    SpecialEventId = reader.GetUInt16("special_event_id"),
                    AchievementPoints = reader.GetUInt32("achievement_points")
                };
            }
        }

        ushort[] completedOffsets;
        await using (MySqlCommand command = connection.CreateCommand())
        {
            command.CommandText = """
SELECT catalog.packet_offset
FROM character_achievements AS earned
INNER JOIN achievement_catalog AS catalog ON catalog.achievement_id = earned.achievement_id
WHERE earned.character_id = @character_id AND earned.completed_at IS NOT NULL
ORDER BY catalog.packet_offset;
""";
            command.Parameters.AddWithValue("@character_id", characterId.Value);
            List<ushort> offsets = [];
            await using MySqlDataReader reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                offsets.Add(reader.GetUInt16("packet_offset"));
            completedOffsets = [.. offsets];
        }

        uint[] latestAchievementIds;
        await using (MySqlCommand command = connection.CreateCommand())
        {
            command.CommandText = """
SELECT earned.achievement_id
FROM character_achievements AS earned
INNER JOIN achievement_catalog AS catalog ON catalog.achievement_id = earned.achievement_id
WHERE earned.character_id = @character_id
  AND earned.completed_at IS NOT NULL
  AND catalog.reward_points <> 0
ORDER BY earned.completed_at DESC, earned.achievement_id DESC
LIMIT 8;
""";
            command.Parameters.AddWithValue("@character_id", characterId.Value);
            List<uint> ids = [];
            await using MySqlDataReader reader = await command
                .ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                ids.Add(reader.GetUInt32("achievement_id"));
            latestAchievementIds = [.. ids];
        }

        return state with
        {
            CompletedAchievementOffsets = completedOffsets,
            LatestAchievementIds = latestAchievementIds
        };
    }
}
