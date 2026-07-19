using System.Security.Cryptography;
using AetherXIV.Core;
using AetherXIV.Data;
using MySqlConnector;

namespace AetherXIV.Launcher.Host;

public interface ILauncherAccountSessionRepository
{
    ValueTask<LauncherAccountRecord?> FindAccountByLoginAsync(
        string loginName,
        CancellationToken cancellationToken = default);

    ValueTask<LauncherAccountRecord?> CreateAccountAsync(
        string loginName,
        string passwordHashSha224,
        string passwordSalt,
        string email,
        CancellationToken cancellationToken = default);

    ValueTask<string> RefreshOrCreateSessionAsync(
        AccountId accountId,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);
}

public sealed record LauncherAccountRecord(
    AccountId Id,
    string LoginName,
    DateTimeOffset CreatedAt,
    string? PasswordHashSha224,
    string? PasswordSalt,
    string? Email);

public sealed class MariaDbLauncherAccountSessionRepository : ILauncherAccountSessionRepository
{
    private const int DuplicateKeyError = 1062;
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbLauncherAccountSessionRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async ValueTask<LauncherAccountRecord?> FindAccountByLoginAsync(
        string loginName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loginName);

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await FindAccountByLoginAsync(connection, loginName, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<LauncherAccountRecord?> CreateAccountAsync(
        string loginName,
        string passwordHashSha224,
        string passwordSalt,
        string email,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loginName);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHashSha224);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordSalt);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using MySqlCommand command = connection.CreateCommand();
            command.CommandText = """
INSERT INTO users (name, passhash, salt, email)
VALUES (@login_name, @password_hash_sha224, @password_salt, @email);
""";
            command.Parameters.AddWithValue("@login_name", loginName);
            command.Parameters.AddWithValue("@password_hash_sha224", passwordHashSha224);
            command.Parameters.AddWithValue("@password_salt", passwordSalt);
            command.Parameters.AddWithValue("@email", email);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MySqlException ex) when (ex.Number == DuplicateKeyError)
        {
            return null;
        }

        return await FindAccountByLoginAsync(connection, loginName, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<string> RefreshOrCreateSessionAsync(
        AccountId accountId,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? existing = await FindActiveSessionForAccountAsync(connection, transaction, accountId, cancellationToken)
                .ConfigureAwait(false);
            if (!String.IsNullOrWhiteSpace(existing))
            {
                await RefreshSessionAsync(connection, transaction, existing, expiresAt, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return existing;
            }

            await DeleteSessionsForAccountAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false);
            string sessionToken = GenerateSessionToken();
            await InsertSessionAsync(connection, transaction, sessionToken, accountId, expiresAt, cancellationToken)
                .ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return sessionToken;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<LauncherAccountRecord?> FindAccountByLoginAsync(
        MySqlConnection connection,
        string loginName,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT id, name, passhash, salt, email
FROM users
WHERE name = @login_name
LIMIT 1;
""";
        command.Parameters.AddWithValue("@login_name", loginName);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new LauncherAccountRecord(
            new AccountId(reader.GetUInt32("id")),
            reader.GetString("name"),
            DateTimeOffset.UnixEpoch,
            ReadNullableString(reader, "passhash"),
            ReadNullableString(reader, "salt"),
            ReadNullableString(reader, "email"));
    }

    private static async Task<string?> FindActiveSessionForAccountAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        AccountId accountId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
SELECT id
FROM sessions
WHERE userId = @account_id
  AND expiration > UTC_TIMESTAMP()
ORDER BY expiration DESC
LIMIT 1;
""";
        command.Parameters.AddWithValue("@account_id", accountId.Value);
        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result as string;
    }

    private static async Task RefreshSessionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string sessionToken,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
UPDATE sessions
SET expiration = @expires_at
WHERE id = @session_token;
""";
        command.Parameters.AddWithValue("@session_token", sessionToken);
        command.Parameters.AddWithValue("@expires_at", expiresAt.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteSessionsForAccountAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        AccountId accountId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
DELETE FROM sessions
WHERE userId = @account_id;
""";
        command.Parameters.AddWithValue("@account_id", accountId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertSessionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string sessionToken,
        AccountId accountId,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO sessions (id, userId, expiration)
VALUES (@session_token, @account_id, @expires_at);
""";
        command.Parameters.AddWithValue("@session_token", sessionToken);
        command.Parameters.AddWithValue("@account_id", accountId.Value);
        command.Parameters.AddWithValue("@expires_at", expiresAt.UtcDateTime);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string GenerateSessionToken()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(28);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string? ReadNullableString(MySqlDataReader reader, string name)
    {
        int ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

}
