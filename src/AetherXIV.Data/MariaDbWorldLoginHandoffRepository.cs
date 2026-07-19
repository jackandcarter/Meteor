using System.Security.Cryptography;
using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbWorldLoginHandoffRepository : IWorldLoginHandoffRepository
{
    public const uint PlayerActorIdPrefix = 0x02000000;
    public const uint PlayerActorIdMask = 0x00FFFFFF;

    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbWorldLoginHandoffRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<WorldLoginHandoffRecord> CreateOrRefreshAsync(
        CharacterId characterId,
        string sessionToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionToken);
        await using MySqlConnection connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);

        await DeleteExpiredForCharacterAsync(connection, characterId, cancellationToken).ConfigureAwait(false);
        WorldLoginHandoffRecord? existing = await GetForCharacterAsync(
            connection,
            characterId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            await UpdateAsync(connection, existing.RuntimeActorId, sessionToken, expiresAt, cancellationToken)
                .ConfigureAwait(false);
            return existing with { SessionToken = sessionToken, ExpiresAt = expiresAt };
        }

        for (int attempt = 0; attempt < 16; attempt++)
        {
            uint runtimeActorId = PlayerActorIdPrefix
                | (uint)RandomNumberGenerator.GetInt32(1, checked((int)PlayerActorIdMask + 1));
            try
            {
                await InsertAsync(
                    connection,
                    runtimeActorId,
                    characterId,
                    sessionToken,
                    expiresAt,
                    cancellationToken).ConfigureAwait(false);
                return new WorldLoginHandoffRecord(runtimeActorId, characterId, sessionToken, expiresAt);
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                existing = await GetForCharacterAsync(connection, characterId, cancellationToken).ConfigureAwait(false);
                if (existing is not null)
                    return existing;
            }
        }

        throw new InvalidOperationException("Unable to allocate an unused World player actor ID.");
    }

    public async Task<WorldLoginHandoffRecord?> GetActiveAsync(
        uint runtimeActorId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT runtime_actor_id, character_id, session_token, expires_at
FROM world_login_handoffs
WHERE runtime_actor_id = @runtime_actor_id
  AND expires_at > UTC_TIMESTAMP(6);
""";
        command.Parameters.AddWithValue("@runtime_actor_id", runtimeActorId);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    private static async Task DeleteExpiredForCharacterAsync(
        MySqlConnection connection,
        CharacterId characterId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM world_login_handoffs WHERE character_id = @character_id AND expires_at <= UTC_TIMESTAMP(6);";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<WorldLoginHandoffRecord?> GetForCharacterAsync(
        MySqlConnection connection,
        CharacterId characterId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT runtime_actor_id, character_id, session_token, expires_at
FROM world_login_handoffs
WHERE character_id = @character_id
  AND expires_at > UTC_TIMESTAMP(6);
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? Read(reader) : null;
    }

    private static async Task InsertAsync(
        MySqlConnection connection,
        uint runtimeActorId,
        CharacterId characterId,
        string sessionToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO world_login_handoffs (runtime_actor_id, character_id, session_token, expires_at)
VALUES (@runtime_actor_id, @character_id, @session_token, @expires_at);
""";
        command.Parameters.AddWithValue("@runtime_actor_id", runtimeActorId);
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        command.Parameters.AddWithValue("@session_token", sessionToken);
        command.Parameters.AddWithValue("@expires_at", expiresAt.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpdateAsync(
        MySqlConnection connection,
        uint runtimeActorId,
        string sessionToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
UPDATE world_login_handoffs
SET session_token = @session_token,
    expires_at = @expires_at,
    updated_at = UTC_TIMESTAMP(6)
WHERE runtime_actor_id = @runtime_actor_id;
""";
        command.Parameters.AddWithValue("@runtime_actor_id", runtimeActorId);
        command.Parameters.AddWithValue("@session_token", sessionToken);
        command.Parameters.AddWithValue("@expires_at", expiresAt.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static WorldLoginHandoffRecord Read(MySqlDataReader reader)
    {
        DateTime expiresAt = DateTime.SpecifyKind(reader.GetDateTime("expires_at"), DateTimeKind.Utc);
        return new WorldLoginHandoffRecord(
            reader.GetUInt32("runtime_actor_id"),
            new CharacterId(reader.GetUInt32("character_id")),
            reader.GetString("session_token"),
            new DateTimeOffset(expiresAt));
    }
}
