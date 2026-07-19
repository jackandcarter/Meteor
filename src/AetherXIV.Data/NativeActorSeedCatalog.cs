using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed record NativeActorSeedManifest(
    string Schema,
    string SeedId,
    string Version,
    int ZoneCount,
    int ActorClassCount,
    int ActorAppearanceCount,
    int StaticActorSpawnCount,
    int ExcludedOrphanSpawnCount,
    int ExcludedInvalidActorClassSpawnCount,
    IReadOnlyDictionary<string, string> Files,
    string Notes);

public sealed record NativeActorSeedCatalog(
    NativeActorSeedManifest Manifest,
    IReadOnlyList<ZoneRecord> Zones,
    IReadOnlyList<ActorClassRecord> ActorClasses,
    IReadOnlyList<ActorAppearanceRecord> ActorAppearances,
    IReadOnlyList<StaticActorSpawnRecord> StaticActorSpawns,
    string ContentHash)
{
    public const string ExpectedSchema = "aetherxiv.native-actor-seed.v1";

    public static async Task<NativeActorSeedCatalog> LoadAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(rootPath);
        NativeActorSeedManifest manifest = await ActorDataDatabaseLoader
            .ReadJsonAsync<NativeActorSeedManifest>(Path.Combine(root, "manifest.json"), cancellationToken)
            .ConfigureAwait(false);
        if (!String.Equals(manifest.Schema, ExpectedSchema, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported native actor seed schema '{manifest.Schema}'.");
        if (String.IsNullOrWhiteSpace(manifest.SeedId) || String.IsNullOrWhiteSpace(manifest.Version))
            throw new InvalidDataException("Native actor seed identity and version are required.");

        foreach ((string fileName, string expectedHash) in manifest.Files.OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            string path = Path.Combine(root, fileName);
            await using FileStream stream = File.OpenRead(path);
            string actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
            if (!String.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Native actor seed hash mismatch for {fileName}.");
        }

        IReadOnlyList<ZoneRecord> zones = await ActorDataDatabaseLoader
            .ReadJsonAsync<IReadOnlyList<ZoneRecord>>(Path.Combine(root, "zones.json"), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<ActorClassRecord> actorClasses = await ActorDataDatabaseLoader
            .ReadJsonAsync<IReadOnlyList<ActorClassRecord>>(Path.Combine(root, "actor-classes.json"), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<ActorAppearanceRecord> appearances = await ActorDataDatabaseLoader
            .ReadJsonAsync<IReadOnlyList<ActorAppearanceRecord>>(Path.Combine(root, "actor-appearances.json"), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<StaticActorSpawnRecord> spawns = await ActorDataDatabaseLoader
            .ReadJsonAsync<IReadOnlyList<StaticActorSpawnRecord>>(Path.Combine(root, "static-actor-spawns.json"), cancellationToken)
            .ConfigureAwait(false);

        if (zones.Count != manifest.ZoneCount
            || actorClasses.Count != manifest.ActorClassCount
            || appearances.Count != manifest.ActorAppearanceCount
            || spawns.Count != manifest.StaticActorSpawnCount)
        {
            throw new InvalidDataException("Native actor seed manifest counts do not match its data files.");
        }
        HashSet<uint> classIds = actorClasses.Select(row => row.ActorClassId).ToHashSet();
        StaticActorSpawnRecord? missingClassSpawn = spawns.FirstOrDefault(row => !classIds.Contains(row.ActorClassId));
        if (missingClassSpawn is not null)
        {
            throw new InvalidDataException(
                $"Native static actor spawn {missingClassSpawn.SpawnId} references missing class {missingClassSpawn.ActorClassId}.");
        }
        if (actorClasses.Any(row => !ActorDataDatabaseLoader.IsValidJson(row.EventConditions)))
            throw new InvalidDataException("Native actor seed contains invalid event-condition JSON.");
        if (actorClasses.Any(row => String.IsNullOrWhiteSpace(row.ClassPath)))
            throw new InvalidDataException("Native actor seed contains an actor class without a runtime class path.");

        string contentHashInput = String.Join(
            "\n",
            manifest.Files.OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => $"{row.Key}:{row.Value}"));
        string contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contentHashInput))).ToLowerInvariant();
        return new NativeActorSeedCatalog(manifest, zones, actorClasses, appearances, spawns, contentHash);
    }
}

public sealed record NativeActorSeedDatabaseLoadRequest(
    string SeedRootPath,
    MariaDbOptions DatabaseOptions,
    WorldRecord? World = null);

public sealed record NativeActorSeedDatabaseLoadResult(
    int ZoneCount,
    int ActorClassCount,
    int ActorAppearanceCount,
    int StaticActorSpawnCount,
    string SeedId,
    string Version,
    string ContentHash);

public sealed class NativeActorSeedDatabaseLoader
{
    public async Task<NativeActorSeedDatabaseLoadResult> LoadAsync(
        NativeActorSeedDatabaseLoadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        NativeActorSeedCatalog catalog = await NativeActorSeedCatalog
            .LoadAsync(request.SeedRootPath, cancellationToken)
            .ConfigureAwait(false);
        WorldRecord world = request.World ?? new WorldRecord(
            new WorldId(1),
            "AetherXIV 2.0 Local",
            new ServerEndpoint("127.0.0.1", 54992));

        await using MySqlConnection connection = new(request.DatabaseOptions.ToConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ActorDataDatabaseLoader.UpsertWorldAsync(connection, transaction, world, cancellationToken).ConfigureAwait(false);
            foreach (ZoneRecord zone in catalog.Zones.OrderBy(row => row.Id.Value))
                await ActorDataDatabaseLoader.UpsertZoneAsync(connection, transaction, zone, world.Id, cancellationToken).ConfigureAwait(false);

            foreach (ActorClassRecord actorClass in catalog.ActorClasses.OrderBy(row => row.ActorClassId))
            {
                ulong provenanceId = await ActorDataDatabaseLoader
                    .GetOrInsertProvenanceAsync(connection, transaction, actorClass.Provenance, cancellationToken)
                    .ConfigureAwait(false);
                await ActorDataDatabaseLoader
                    .UpsertActorClassAsync(connection, transaction, actorClass, provenanceId, cancellationToken)
                    .ConfigureAwait(false);
            }
            foreach (ActorAppearanceRecord appearance in catalog.ActorAppearances.OrderBy(row => row.ActorClassId))
            {
                ulong provenanceId = await ActorDataDatabaseLoader
                    .GetOrInsertProvenanceAsync(connection, transaction, appearance.Provenance, cancellationToken)
                    .ConfigureAwait(false);
                await ActorDataDatabaseLoader
                    .UpsertActorAppearanceAsync(connection, transaction, appearance, provenanceId, cancellationToken)
                    .ConfigureAwait(false);
            }
            foreach (StaticActorSpawnRecord spawn in catalog.StaticActorSpawns.OrderBy(row => row.SpawnId))
            {
                ulong provenanceId = await ActorDataDatabaseLoader
                    .GetOrInsertProvenanceAsync(connection, transaction, spawn.Provenance, cancellationToken)
                    .ConfigureAwait(false);
                await ActorDataDatabaseLoader
                    .UpsertStaticActorSpawnAsync(connection, transaction, spawn, provenanceId, cancellationToken)
                    .ConfigureAwait(false);
            }

            await DeleteStaleStaticActorSpawnsAsync(
                connection,
                transaction,
                catalog.StaticActorSpawns.Select(row => row.SpawnId).ToArray(),
                cancellationToken).ConfigureAwait(false);
            await UpsertSeedVersionAsync(connection, transaction, catalog, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return new NativeActorSeedDatabaseLoadResult(
            catalog.Zones.Count,
            catalog.ActorClasses.Count,
            catalog.ActorAppearances.Count,
            catalog.StaticActorSpawns.Count,
            catalog.Manifest.SeedId,
            catalog.Manifest.Version,
            catalog.ContentHash);
    }

    private static async Task DeleteStaleStaticActorSpawnsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        IReadOnlyList<uint> retainedSpawnIds,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        string[] parameters = new string[retainedSpawnIds.Count];
        for (int index = 0; index < retainedSpawnIds.Count; index++)
        {
            string parameter = $"@spawn_id_{index}";
            parameters[index] = parameter;
            command.Parameters.AddWithValue(parameter, retainedSpawnIds[index]);
        }

        command.CommandText = $"""
DELETE sas
FROM static_actor_spawns sas
INNER JOIN provenance_refs provenance ON provenance.provenance_id = sas.provenance_id
WHERE provenance.source_type = 'v1-sql'
  AND provenance.source_ref LIKE 'server_spawn_locations:%'
  AND sas.spawn_id NOT IN ({String.Join(",", parameters)});
""";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertSeedVersionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        NativeActorSeedCatalog catalog,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO runtime_seed_versions (
  seed_id, seed_version, content_hash, zone_count, actor_class_count,
  actor_appearance_count, static_actor_spawn_count)
VALUES (
  @seed_id, @seed_version, @content_hash, @zone_count, @actor_class_count,
  @actor_appearance_count, @static_actor_spawn_count)
ON DUPLICATE KEY UPDATE
  seed_version = VALUES(seed_version),
  content_hash = VALUES(content_hash),
  zone_count = VALUES(zone_count),
  actor_class_count = VALUES(actor_class_count),
  actor_appearance_count = VALUES(actor_appearance_count),
  static_actor_spawn_count = VALUES(static_actor_spawn_count),
  installed_at = CURRENT_TIMESTAMP;
""";
        command.Parameters.AddWithValue("@seed_id", catalog.Manifest.SeedId);
        command.Parameters.AddWithValue("@seed_version", catalog.Manifest.Version);
        command.Parameters.AddWithValue("@content_hash", catalog.ContentHash);
        command.Parameters.AddWithValue("@zone_count", catalog.Zones.Count);
        command.Parameters.AddWithValue("@actor_class_count", catalog.ActorClasses.Count);
        command.Parameters.AddWithValue("@actor_appearance_count", catalog.ActorAppearances.Count);
        command.Parameters.AddWithValue("@static_actor_spawn_count", catalog.StaticActorSpawns.Count);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
