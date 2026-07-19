using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed record QuestActorSeedManifest(
    string Schema,
    string SeedId,
    string Version,
    int QuestCount,
    string SourceHash,
    IReadOnlyDictionary<string, string> Files,
    string Notes);

public sealed record QuestActorSeedCatalog(
    QuestActorSeedManifest Manifest,
    IReadOnlyList<QuestDefinitionRecord> Definitions,
    string ContentHash)
{
    public const string ExpectedSchema = "aetherxiv.quest-actor-seed.v1";

    public static async Task<QuestActorSeedCatalog> LoadAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(rootPath);
        JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
        QuestActorSeedManifest manifest = JsonSerializer.Deserialize<QuestActorSeedManifest>(
            await File.ReadAllTextAsync(Path.Combine(root, "manifest.json"), cancellationToken).ConfigureAwait(false),
            json) ?? throw new InvalidDataException("Quest actor manifest is empty.");
        if (!String.Equals(manifest.Schema, ExpectedSchema, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported quest actor seed schema '{manifest.Schema}'.");

        foreach ((string fileName, string expectedHash) in manifest.Files)
        {
            await using FileStream stream = File.OpenRead(Path.Combine(root, fileName));
            string actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken))
                .ToLowerInvariant();
            if (!String.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Quest actor seed hash mismatch for {fileName}.");
        }

        IReadOnlyList<QuestDefinitionRecord> definitions = JsonSerializer.Deserialize<IReadOnlyList<QuestDefinitionRecord>>(
            await File.ReadAllTextAsync(Path.Combine(root, "quest-actors.json"), cancellationToken).ConfigureAwait(false),
            json) ?? throw new InvalidDataException("Quest actor catalog is empty.");
        if (definitions.Count != 734 || definitions.Count != manifest.QuestCount)
            throw new InvalidDataException($"Quest actor catalog must contain exactly 734 records; found {definitions.Count}.");
        if (definitions.Select(row => row.QuestId).Distinct().Count() != definitions.Count
            || definitions.Select(row => row.ActorId).Distinct().Count() != definitions.Count)
            throw new InvalidDataException("Quest actor catalog contains duplicate quest or actor IDs.");
        QuestDefinitionRecord? invalid = definitions.FirstOrDefault(row =>
            row.ActorId != (0xA0F00000u | row.QuestId)
            || String.IsNullOrWhiteSpace(row.QuestName)
            || String.IsNullOrWhiteSpace(row.StaticPath)
            || String.IsNullOrWhiteSpace(row.Family));
        if (invalid is not null)
            throw new InvalidDataException($"Quest actor {invalid.QuestId} has an invalid canonical identity.");

        string contentHashInput = String.Join("\n", manifest.Files.OrderBy(row => row.Key).Select(row => $"{row.Key}:{row.Value}"));
        string contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contentHashInput))).ToLowerInvariant();
        return new QuestActorSeedCatalog(manifest, definitions, contentHash);
    }
}

public sealed class MariaDbQuestDefinitionRepository : IQuestDefinitionRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbQuestDefinitionRepository(IDatabaseConnectionFactory connectionFactory) =>
        this.connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<QuestDefinitionRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT quest_id, actor_id, quest_name, static_path, family, script_module_path FROM quest_definitions ORDER BY quest_id;";
        List<QuestDefinitionRecord> result = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(new QuestDefinitionRecord(
                reader.GetUInt32("quest_id"),
                reader.GetUInt32("actor_id"),
                reader.GetString("quest_name"),
                reader.GetString("static_path"),
                reader.GetString("family"),
                reader.IsDBNull(reader.GetOrdinal("script_module_path")) ? null : reader.GetString("script_module_path")));
        }
        return result;
    }

    public async Task ReplaceCatalogAsync(
        IReadOnlyList<QuestDefinitionRecord> definitions,
        string seedVersion,
        string contentHash,
        CancellationToken cancellationToken = default)
    {
        if (definitions.Count != 734)
            throw new ArgumentException("The canonical quest catalog must contain exactly 734 definitions.", nameof(definitions));
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (QuestDefinitionRecord row in definitions)
            {
                await using MySqlCommand command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = """
INSERT INTO quest_definitions (quest_id, actor_id, quest_name, static_path, family, script_module_path)
VALUES (@quest_id, @actor_id, @quest_name, @static_path, @family, @script_module_path)
ON DUPLICATE KEY UPDATE actor_id=VALUES(actor_id), quest_name=VALUES(quest_name), static_path=VALUES(static_path),
  family=VALUES(family), script_module_path=VALUES(script_module_path);
""";
                command.Parameters.AddWithValue("@quest_id", row.QuestId);
                command.Parameters.AddWithValue("@actor_id", row.ActorId);
                command.Parameters.AddWithValue("@quest_name", row.QuestName);
                command.Parameters.AddWithValue("@static_path", row.StaticPath);
                command.Parameters.AddWithValue("@family", row.Family);
                command.Parameters.AddWithValue("@script_module_path", row.ScriptModulePath is null ? DBNull.Value : row.ScriptModulePath);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            await using MySqlCommand version = connection.CreateCommand();
            version.Transaction = transaction;
            version.CommandText = """
INSERT INTO quest_catalog_versions (seed_id, seed_version, content_hash, quest_count)
VALUES ('aetherxiv-legacy-quest-actors', @version, @hash, 734)
ON DUPLICATE KEY UPDATE seed_version=VALUES(seed_version), content_hash=VALUES(content_hash), quest_count=734, installed_at=CURRENT_TIMESTAMP;
""";
            version.Parameters.AddWithValue("@version", seedVersion);
            version.Parameters.AddWithValue("@hash", contentHash);
            await version.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
