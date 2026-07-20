using AetherXIV.Data;
using MySqlConnector;
using System.Security.Cryptography;

namespace AetherXIV.Operator;

public enum AetherXivDatabasePreflightStatus
{
    Passed,
    Repaired,
    Missing,
    Failed,
    NeedsAdminCredentials,
    NeedsMigration,
    NeedsRepair,
    Blocked
}

public static class AetherXivDatabaseCompatibility
{
    public const string Key = "direct-core";
    public const uint SchemaGeneration = 2;
    public const uint SchemaVersion = 1;
    public const string CompatibilityId = "aetherxiv-direct-core-v2";
    public const string BaselineId = "20260716_000001_ffxiv_server_v2_baseline";
    public const string GuildleveContentMigration = "20260716_000005_guildleve_content_contract.sql";
    public const string LatestDirectCoreMigration = "20260720_000017_gridania_tutorial_spawn_contract.sql";
    public static readonly IReadOnlyList<string> RequiredDirectCoreMigrations =
    [
        "20260627_battlenpc_spawn_audit_pins.sql",
        "20260707_seed_level1_player_base_stats.sql",
        "20260716_000001_launcher_ui_contract.sql",
        "20260716_000002_remove_development_workbench.sql",
        "20260716_000003_launcher_local_identity.sql",
        "20260716_000004_database_compatibility.sql",
        "20260716_000005_guildleve_content_contract.sql",
        "20260717_000006_central_shroud_enemy_restore.sql",
        "20260717_000007_character_attribute_allocations.sql",
        "20260718_000008_zone_service_npcs.sql",
        "20260718_000009_chocobo_stop_runtime.sql",
        "20260718_000010_immortal_flames_company_shop.sql",
        "20260718_000011_uldah_company_office_entrance.sql",
        "20260718_000012_hall_of_flames_exit.sql",
        "20260718_000013_central_shroud_pinspawn_restore.sql",
        "20260718_000014_hall_of_flames_exit_runtime.sql",
        "20260719_000015_hall_of_flames_push_circle.sql",
        "20260720_000016_gridania_tutorial_actor_roles.sql",
        "20260720_000017_gridania_tutorial_spawn_contract.sql"
    ];
    public const string NpcServiceCatalogId = "zone-service-npcs-1.23b";
    public const string NpcServiceCatalogVersion = "2026.07.19.1";
    public const string NpcServiceCatalogHash = "f40276dea0ce6739b40d0dca3dc44f665ee525646851592a9439d5013f97b8de";
}

public sealed record AetherXivMariaDbAdminCredentials(string User, string Password);

public sealed record AetherXivDatabasePreflightStep(
    string Name,
    AetherXivDatabasePreflightStatus Status,
    string Message);

public sealed record AetherXivDatabasePreflightResult(
    IReadOnlyList<AetherXivDatabasePreflightStep> Steps)
{
    public bool NeedsAdminCredentials => Steps.Any(step =>
        step.Status is AetherXivDatabasePreflightStatus.NeedsAdminCredentials);

    public bool RequiresInPlaceMigration => Steps.Any(step =>
        step.Status is AetherXivDatabasePreflightStatus.NeedsMigration);

    public bool RequiresCanonicalRepair => Steps.Any(step =>
        step.Status is AetherXivDatabasePreflightStatus.NeedsRepair);

    public bool CanStartServices => Steps.All(step =>
        step.Status is AetherXivDatabasePreflightStatus.Passed or AetherXivDatabasePreflightStatus.Repaired);
}

public sealed class AetherXivDatabasePreflightService
{
    public Task<AetherXivDatabasePreflightResult> RunAsync(
        AetherXivOperatorConfig config,
        bool repair,
        IProgress<AetherXivDatabasePreflightStep>? progress = null,
        CancellationToken cancellationToken = default) =>
        RunAsync(config, repair, null, progress, cancellationToken);

    public async Task<AetherXivDatabasePreflightResult> RunAsync(
        AetherXivOperatorConfig config,
        bool repair,
        AetherXivMariaDbAdminCredentials? adminCredentials,
        IProgress<AetherXivDatabasePreflightStep>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        List<AetherXivDatabasePreflightStep> steps = new();
        void Add(string name, AetherXivDatabasePreflightStatus status, string message)
        {
            AetherXivDatabasePreflightStep step = new(name, status, message);
            steps.Add(step);
            progress?.Report(step);
        }

        AetherXivOperatorConfig normalized = config.Normalize();
        MariaDbOptions options = new(
            normalized.Database.Host,
            normalized.Database.Port,
            normalized.Database.Name,
            normalized.Database.User,
            normalized.Database.Password);

        try
        {
            await RunDirectCorePreflightAsync(normalized, options, repair, Add, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsBootstrapCandidate(ex))
        {
            Add(
                "database.bootstrap",
                AetherXivDatabasePreflightStatus.NeedsAdminCredentials,
                "The direct-core database or app account does not exist or is not accessible. MariaDB administrator credentials are needed once so startup can create the canonical AetherXIV 2 database and application account.");
        }
        catch (Exception ex)
        {
            Add("database", AetherXivDatabasePreflightStatus.Blocked, ex.Message);
        }

        return new AetherXivDatabasePreflightResult(steps);
    }

    private static async Task RunDirectCorePreflightAsync(
        AetherXivOperatorConfig config,
        MariaDbOptions options,
        bool repair,
        Action<string, AetherXivDatabasePreflightStatus, string> add,
        CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = new(options.ToConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        add("connect", AetherXivDatabasePreflightStatus.Passed,
            $"Connected to the configured database at {options.User}@{options.Host}:{options.Port}/{options.Database}.");

        int tableCount = await CountAsync(connection,
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE();",
            cancellationToken).ConfigureAwait(false);
        if (tableCount == 0)
        {
            add("database.bootstrap", AetherXivDatabasePreflightStatus.NeedsAdminCredentials,
                "The configured database exists but is empty. MariaDB administrator credentials are needed once to install the AetherXIV 2 database and application account.");
            return;
        }

        if (!await TableExistsAsync(connection, "aether_database_compatibility", cancellationToken).ConfigureAwait(false))
        {
            add(
                "database.version",
                AetherXivDatabasePreflightStatus.NeedsRepair,
                "This database predates AetherXIV 2 or is incomplete. Setup will keep a full backup, install a clean AetherXIV 2 database, and restore compatible account and character data when possible.");
            return;
        }

        await using (MySqlCommand compatibility = connection.CreateCommand())
        {
            compatibility.CommandText = """
SELECT schema_generation, schema_version, compatibility_id, baseline_id
FROM aether_database_compatibility
WHERE compatibility_key=@key
LIMIT 1;
""";
            compatibility.Parameters.AddWithValue("@key", AetherXivDatabaseCompatibility.Key);
            await using MySqlDataReader compatibilityReader = await compatibility.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await compatibilityReader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                await compatibilityReader.DisposeAsync().ConfigureAwait(false);
                add(
                    "database.version",
                    AetherXivDatabasePreflightStatus.NeedsRepair,
                    "The AetherXIV 2 database version record is missing. Setup will preserve a full backup and rebuild the canonical schema before services start.");
                return;
            }

            uint generation = compatibilityReader.GetUInt32(0);
            uint version = compatibilityReader.GetUInt32(1);
            string compatibilityId = compatibilityReader.GetString(2);
            string baselineId = compatibilityReader.GetString(3);
            bool sameContractFamily = generation == AetherXivDatabaseCompatibility.SchemaGeneration
                && String.Equals(compatibilityId, AetherXivDatabaseCompatibility.CompatibilityId, StringComparison.Ordinal)
                && String.Equals(baselineId, AetherXivDatabaseCompatibility.BaselineId, StringComparison.Ordinal);
            if (!sameContractFamily || version != AetherXivDatabaseCompatibility.SchemaVersion)
            {
                add("database.version", AetherXivDatabasePreflightStatus.NeedsRepair,
                    $"The installed database is not the AetherXIV 2 schema (generation {generation}, version {version}). "
                    + "Setup will preserve a full backup and rebuild it before services start.");
                return;
            }
        }
        add("database.version", AetherXivDatabasePreflightStatus.Passed,
            $"Verified AetherXIV 2 database schema {AetherXivDatabaseCompatibility.SchemaGeneration}.{AetherXivDatabaseCompatibility.SchemaVersion}.");

        if (!await TableExistsAsync(connection, "aether_schema_migrations", cancellationToken).ConfigureAwait(false))
        {
            add("database.migrations", AetherXivDatabasePreflightStatus.NeedsRepair,
                "The AetherXIV 2 migration ledger is missing, so this database cannot be safely advanced in place. "
                + "Setup will retain a backup and offer a fresh canonical install.");
            return;
        }

        string? databasePackage = AetherXivDatabaseInstaller.FindPackageDirectory(
            config.WorkspaceRoot,
            AppContext.BaseDirectory,
            Environment.CurrentDirectory);
        if (databasePackage is null)
        {
            add("database.package", AetherXivDatabasePreflightStatus.Blocked,
                "The packaged Database installer is missing or incomplete, so schema and migration checksums cannot be verified.");
            return;
        }

        string baselineHistoryPath = Path.Combine(databasePackage, "baseline-history.sha256");
        string migrationDirectory = Path.Combine(databasePackage, "migrations");
        if (!File.Exists(baselineHistoryPath) || !Directory.Exists(migrationDirectory))
        {
            add("database.package", AetherXivDatabasePreflightStatus.Blocked,
                "The packaged Database installer is missing its trusted baseline history or migrations directory.");
            return;
        }

        HashSet<string> trustedBaselines = File.ReadLines(baselineHistoryPath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string? recordedBaseline = await ScalarStringAsync(connection, """
SELECT checksum_sha256
FROM aether_schema_migrations
WHERE migration_name='baseline/20260716_000001_ffxiv_server_v2'
LIMIT 1;
""", cancellationToken).ConfigureAwait(false);
        if (String.IsNullOrWhiteSpace(recordedBaseline) || !trustedBaselines.Contains(recordedBaseline))
        {
            add("database.baseline", AetherXivDatabasePreflightStatus.NeedsRepair,
                "The recorded baseline is missing or is not a known canonical AetherXIV baseline. "
                + "It cannot be safely migrated in place; setup will retain a backup and offer a fresh install.");
            return;
        }

        Dictionary<string, string> expectedMigrations = new(StringComparer.Ordinal);
        foreach (string migrationName in AetherXivDatabaseCompatibility.RequiredDirectCoreMigrations)
        {
            string migrationPath = Path.Combine(migrationDirectory, migrationName);
            if (!File.Exists(migrationPath))
            {
                add("database.package", AetherXivDatabasePreflightStatus.Blocked,
                    $"The packaged Database installer is missing required migration {migrationName}.");
                return;
            }
            expectedMigrations[migrationName] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(migrationPath))).ToLowerInvariant();
        }

        Dictionary<string, string> recordedMigrations = new(StringComparer.Ordinal);
        await using (MySqlCommand migrationLedger = connection.CreateCommand())
        {
            string[] parameterNames = expectedMigrations.Keys.Select((_, index) => $"@migration{index}").ToArray();
            migrationLedger.CommandText = $"""
SELECT migration_name, checksum_sha256
FROM aether_schema_migrations
WHERE migration_name IN ({String.Join(",", parameterNames)});
""";
            int index = 0;
            foreach (string migrationName in expectedMigrations.Keys)
                migrationLedger.Parameters.AddWithValue(parameterNames[index++], migrationName);
            await using MySqlDataReader migrationReader = await migrationLedger.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await migrationReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                recordedMigrations[migrationReader.GetString(0)] = migrationReader.GetString(1);
        }

        string[] changedMigrations = expectedMigrations
            .Where(pair => recordedMigrations.TryGetValue(pair.Key, out string? checksum)
                && !String.Equals(pair.Value, checksum, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Key)
            .ToArray();
        if (changedMigrations.Length > 0)
        {
            add("database.migrations", AetherXivDatabasePreflightStatus.NeedsRepair,
                $"Recorded migration checksums do not match the packaged canonical files: {String.Join(", ", changedMigrations)}. "
                + "The database cannot be safely advanced in place; setup will offer a backed-up fresh install.");
            return;
        }

        string[] missingMigrations = expectedMigrations.Keys
            .Where(name => !recordedMigrations.ContainsKey(name))
            .ToArray();
        if (missingMigrations.Length > 0)
        {
            add("database.migrations", AetherXivDatabasePreflightStatus.NeedsMigration,
                $"Pending direct-core migrations: {missingMigrations.Length}/{expectedMigrations.Count}; "
                + $"latest required is {AetherXivDatabaseCompatibility.LatestDirectCoreMigration}. "
                + "The packaged updater will back up the database, apply every missing migration, and verify the result in place.");
            return;
        }
        add("database.migrations", AetherXivDatabasePreflightStatus.Passed,
            $"Verified names and SHA-256 checksums for all {recordedMigrations.Count} required migrations through {AetherXivDatabaseCompatibility.LatestDirectCoreMigration}.");

        string[] requiredTables =
        [
            "users", "sessions", "servers", "characters", "characters_appearance",
            "characters_quest_scenario", "characters_quest_completed", "characters_hotbar",
            "server_sessions", "server_zones", "server_zones_privateareas",
            "server_battlenpc_spawn_locations", "server_battlenpc_spawn_audit_pins", "server_battlenpc_groups", "server_battlenpc_pools",
            "server_battle_commands", "server_player_base_stats", "characters_class_attributes", "server_spawn_locations",
            "gamedata_actor_class", "gamedata_actor_appearance", "server_items_modifiers",
            "characters_inventory", "characters_chocobo", "server_npc_spawn_evidence",
            "server_npc_spawn_evidence_catalog",
            "aether_database_compatibility",
            "launcher_config", "launcher_config_plugin_catalogs", "launcher_status", "launcher_news", "launcher_patch_files",
            "launcher_presentation", "launcher_reel_text", "launcher_runtime_artifacts", "launcher_umbra_framework_artifacts",
            "launcher_umbra_plugin_repositories", "launcher_umbra_plugins", "launcher_umbra_plugin_blocks"
        ];
        List<string> missing = new();
        foreach (string table in requiredTables)
        {
            if (!await TableExistsAsync(connection, table, cancellationToken).ConfigureAwait(false))
                missing.Add(table);
        }

        if (missing.Count > 0)
        {
            add("database.schema", AetherXivDatabasePreflightStatus.NeedsRepair,
                $"The AetherXIV 2 database is missing required tables: {String.Join(", ", missing)}. Setup will back it up and rebuild the canonical schema.");
            return;
        }
        add("direct-core.schema", AetherXivDatabasePreflightStatus.Passed,
            $"Verified {requiredTables.Length} direct-core and modern launcher tables.");

        int obsoleteTables = await CountAsync(connection, """
SELECT COUNT(*)
FROM information_schema.tables
WHERE table_schema=DATABASE()
  AND table_name IN (
    'server_battlenpc_appearance_audit',
    'server_battlenpc_restoration_evidence',
    'client_decoded_display_name_stage',
    'client_decoded_actor_graphic_stage',
    'client_decoded_actor_class_stage',
    'client_decode_import_batches');
""", cancellationToken).ConfigureAwait(false);
        if (obsoleteTables != 0)
        {
            add("direct-core.obsolete-schema", AetherXivDatabasePreflightStatus.NeedsRepair,
                $"The database still contains {obsoleteTables} obsolete development tables. A canonical rebuild is required.");
            return;
        }
        add("direct-core.obsolete-schema", AetherXivDatabasePreflightStatus.Passed,
            "Verified that obsolete development/import tables are absent.");

        int gridaniaTutorialActorRoles = await CountAsync(connection, """
SELECT COUNT(*)
FROM server_battlenpc_spawn_locations s
JOIN server_battlenpc_groups g ON g.groupId=s.groupId
JOIN server_battlenpc_pools p ON p.poolId=g.poolId
WHERE (s.bnpcId=6 AND s.customDisplayName='yda' AND g.scriptName='yda' AND p.name='yda' AND p.actorClassId=2290006)
   OR (s.bnpcId=7 AND s.customDisplayName='papalymo' AND g.scriptName='papalymo' AND p.name='papalymo' AND p.actorClassId=2290005);
""", cancellationToken).ConfigureAwait(false);
        if (gridaniaTutorialActorRoles != 2)
        {
            add("gridania-tutorial.actor-contract", AetherXivDatabasePreflightStatus.NeedsRepair,
                "The Gridania tutorial spawn IDs 6 and 7 are missing, reversed, or stale. Run the packaged Database setup tool.");
            return;
        }
        add("gridania-tutorial.actor-contract", AetherXivDatabasePreflightStatus.Passed,
            "Verified Gridania tutorial spawn 6 as Yda and spawn 7 as Papalymo.");

        int guildleveSearchPointContract = await CountAsync(connection, """
SELECT COUNT(*)
FROM gamedata_actor_class c
JOIN gamedata_actor_pushcommand p ON p.id=c.id
WHERE c.id=1200036
  AND c.classPath='/Chara/Npc/Object/GuildleveSearchPoint'
  AND c.propertyFlags=3
  AND p.pushCommand=10003
  AND p.pushCommandSub=0
  AND p.pushCommandPriority=12;
""", cancellationToken).ConfigureAwait(false);
        if (guildleveSearchPointContract != 1)
        {
            add("guildleve.actor-contract", AetherXivDatabasePreflightStatus.NeedsRepair,
                "Guildleve search-point actor data is missing or stale. Run the packaged Database setup tool.");
            return;
        }
        add("guildleve.actor-contract", AetherXivDatabasePreflightStatus.Passed,
            "Verified the guildleve search-point actor and push-command contract.");

        int chocoboStopContract = await CountAsync(connection, """
SELECT COUNT(*)
FROM gamedata_actor_class
WHERE id=1090464
  AND classPath='/Chara/Npc/Object/ChocoboStop'
  AND propertyFlags=1
  AND JSON_VALID(eventConditions)=1
  AND eventConditions LIKE '%"pushDefault"%'
  AND eventConditions LIKE '%"_!pushRequest"%'
  AND eventConditions LIKE '%"secondaryRadius":6.0%'
  AND eventConditions LIKE '%"secondaryRadius":10.0%';
""", cancellationToken).ConfigureAwait(false);
        if (chocoboStopContract != 1)
        {
            add("chocobo-stop.actor-contract", AetherXivDatabasePreflightStatus.NeedsRepair,
                "The trace-backed ChocoboStop actor conditions are missing or stale. Run the packaged Database setup tool.");
            return;
        }
        add("chocobo-stop.actor-contract", AetherXivDatabasePreflightStatus.Passed,
            "Verified the trace-backed ChocoboStop push-condition contract.");

        int npcServiceCatalog = await CountAsync(connection, $"""
SELECT COUNT(*) FROM server_npc_spawn_evidence_catalog
WHERE catalogId='{AetherXivDatabaseCompatibility.NpcServiceCatalogId}'
  AND version='{AetherXivDatabaseCompatibility.NpcServiceCatalogVersion}'
  AND contentHashSha256='{AetherXivDatabaseCompatibility.NpcServiceCatalogHash}'
  AND clientBuild='2012.09.19.0001'
  AND recordCount=23;
""", cancellationToken).ConfigureAwait(false);
        int npcServiceReferences = await CountAsync(connection, """
SELECT COUNT(*)
FROM server_npc_spawn_evidence e
JOIN server_spawn_locations s ON s.id=e.spawnId
JOIN server_zones z ON z.id=e.zoneId
JOIN gamedata_actor_class c ON c.id=e.actorClassId
JOIN gamedata_actor_appearance a ON a.id=e.appearanceId
WHERE s.actorClassId=e.actorClassId
  AND s.zoneId=e.zoneId
  AND s.privateAreaName=e.privateAreaName
  AND ABS(s.positionX-e.positionX)<0.001
  AND ABS(s.positionY-e.positionY)<0.001
  AND ABS(s.positionZ-e.positionZ)<0.001
  AND ABS(s.rotation-e.rotation)<0.001
  AND c.classPath=e.classPath
  AND c.eventConditions IS NOT NULL
  AND JSON_VALID(c.eventConditions)=1;
""", cancellationToken).ConfigureAwait(false);
        int stablemasters = await CountAsync(connection, """
SELECT COUNT(*) FROM (
  SELECT e.actorClassId
  FROM server_npc_spawn_evidence e
  JOIN server_spawn_locations s ON s.id=e.spawnId
  WHERE e.service='stablemaster'
    AND s.actorClassId=e.actorClassId
    AND s.zoneId=e.zoneId
    AND s.privateAreaName=e.privateAreaName
  GROUP BY e.actorClassId
  HAVING COUNT(*)=1
) approved_stablemasters;
""", cancellationToken).ConfigureAwait(false);
        int repairers = await CountAsync(connection, """
SELECT COUNT(*) FROM (
  SELECT e.actorClassId
  FROM server_npc_spawn_evidence e
  JOIN server_spawn_locations s ON s.id=e.spawnId
  WHERE e.service='repair'
    AND s.actorClassId=e.actorClassId
    AND s.zoneId=e.zoneId
    AND s.privateAreaName=e.privateAreaName
  GROUP BY e.actorClassId
  HAVING COUNT(*)=1
) approved_repairers;
""", cancellationToken).ConfigureAwait(false);
        int chocoboNamingActors = await CountAsync(connection, """
SELECT COUNT(*)
FROM server_npc_spawn_evidence e
JOIN server_spawn_locations s ON s.id=e.spawnId
WHERE e.service='chocobo-required-actor'
  AND s.actorClassId=1080101
  AND s.actorClassId=e.actorClassId
  AND s.zoneId=e.zoneId
  AND s.privateAreaName=e.privateAreaName;
""", cancellationToken).ConfigureAwait(false);
        int immortalFlamesCompanyShop = await CountAsync(connection, """
SELECT COUNT(*)
FROM server_npc_spawn_evidence e
JOIN server_spawn_locations s ON s.id=e.spawnId
JOIN gamedata_actor_class c ON c.id=e.actorClassId
JOIN gamedata_actor_appearance a ON a.id=e.appearanceId
WHERE e.service='grand-company-shop'
  AND e.actorClassId=1500201
  AND e.zoneId=233
  AND s.actorClassId=e.actorClassId
  AND s.zoneId=e.zoneId
  AND ABS(s.positionX-169)<0.001
  AND ABS(s.positionY)<0.001
  AND ABS(s.positionZ+177.5)<0.001
  AND ABS(s.rotation+1.5)<0.001
  AND c.classPath='/Chara/Npc/Populace/PopulaceCompanyShop';
""", cancellationToken).ConfigureAwait(false);
        int hallOfFlamesExit = await CountAsync(connection, """
SELECT COUNT(*)
FROM server_npc_spawn_evidence e
JOIN server_spawn_locations s ON s.id=e.spawnId
JOIN gamedata_actor_class c ON c.id=e.actorClassId
WHERE e.service='grand-company-office-exit'
  AND e.actorClassId=1090264
  AND e.zoneId=233
  AND s.actorClassId=e.actorClassId
  AND s.zoneId=e.zoneId
  AND ABS(s.positionX-160)<0.001
  AND ABS(s.positionY)<0.001
  AND ABS(s.positionZ+142.76)<0.001
  AND c.classPath='/Chara/Npc/Object/MarketEntrance'
  AND c.eventConditions LIKE '%\"pushWithCircleEventConditions\"%'
  AND c.eventConditions LIKE '%\"conditionName\":\"pushDefault\"%'
  AND c.eventConditions LIKE '%\"radius\":4.0%'
  AND c.eventConditions LIKE '%\"secondaryRadius\":10.0%'
  AND c.eventConditions LIKE '%\"silent\":false%'
  AND c.eventConditions LIKE '%\"useSourceActorId\":true%';
""", cancellationToken).ConfigureAwait(false);
        int hallOfFlamesEntrance = await CountAsync(connection, """
SELECT COUNT(*)
FROM server_npc_spawn_evidence e
JOIN server_spawn_locations s ON s.id=e.spawnId
JOIN gamedata_actor_class c ON c.id=e.actorClassId
WHERE e.service='grand-company-office-entrance'
  AND e.actorClassId=1090265
  AND e.zoneId=175
  AND s.actorClassId=e.actorClassId
  AND s.zoneId=e.zoneId
  AND ABS(s.positionX+235)<0.001
  AND ABS(s.positionY-189)<0.001
  AND ABS(s.positionZ-50.5)<0.001
  AND c.classPath='/Chara/Npc/Object/MarketEntrance'
  AND c.eventConditions LIKE '%\"bgObj\":4143%'
  AND c.eventConditions LIKE '%\"layout\":421%'
  AND c.eventConditions LIKE '%\"reactName\":\"dtwi\"%';
""", cancellationToken).ConfigureAwait(false);
        if (npcServiceCatalog != 1 || npcServiceReferences != 23 || stablemasters != 3
            || repairers != 8 || chocoboNamingActors != 3
            || immortalFlamesCompanyShop != 1 || hallOfFlamesExit != 1 || hallOfFlamesEntrance != 1)
        {
            add("npc-services.seed-contract", AetherXivDatabasePreflightStatus.NeedsRepair,
                $"NPC service seed is missing or stale: catalog={npcServiceCatalog}/1, references={npcServiceReferences}/23, "
                + $"stablemasters={stablemasters}/3, repairers={repairers}/8, namingActors={chocoboNamingActors}/3, "
                + $"flameShop={immortalFlamesCompanyShop}/1, hallEntrance={hallOfFlamesEntrance}/1, hallExit={hallOfFlamesExit}/1.");
            return;
        }
        add("npc-services.seed-contract", AetherXivDatabasePreflightStatus.Passed,
            $"Verified NPC service catalog {AetherXivDatabaseCompatibility.NpcServiceCatalogVersion} ({AetherXivDatabaseCompatibility.NpcServiceCatalogHash}).");

        int centralShroudActorClasses = await CountAsync(connection, """
SELECT COUNT(*)
FROM gamedata_actor_class
WHERE id IN (2100504,2101424,2102708,2102721,2103905,2104007,2104017,2104105,2107606)
  AND classPath <> ''
  AND propertyFlags = 23
  AND eventConditions LIKE '%noticeEvent%';
""", cancellationToken).ConfigureAwait(false);
        int centralShroudSpawns = await CountAsync(connection, """
SELECT COUNT(*)
FROM server_battlenpc_spawn_locations s
JOIN server_battlenpc_groups g ON g.groupId=s.groupId
JOIN server_battlenpc_pools p ON p.poolId=g.poolId
WHERE s.bnpcId BETWEEN 1620001 AND 1620023
  AND g.zoneId=162
  AND p.actorClassId IN (2100504,2101424,2102708,2102721,2103905,2104007,2104017,2104105,2107606);
""", cancellationToken).ConfigureAwait(false);
        int removedDesertRats = await CountAsync(connection, """
SELECT COUNT(*) FROM server_battlenpc_spawn_locations
WHERE bnpcId IN (10001,10002,10003,10004);
""", cancellationToken).ConfigureAwait(false);
        int retainedTestEnemies = await CountAsync(connection, """
SELECT COUNT(*) FROM server_battlenpc_spawn_locations
WHERE bnpcId IN (1,2);
""", cancellationToken).ConfigureAwait(false);
        if (centralShroudActorClasses != 9 || centralShroudSpawns != 23
            || removedDesertRats != 0 || retainedTestEnemies != 2)
        {
            add("battle-npc.zone162-contract", AetherXivDatabasePreflightStatus.NeedsRepair,
                $"Central Shroud enemy data is incomplete: actorClasses={centralShroudActorClasses}/9, "
                + $"spawns={centralShroudSpawns}/23, removedDesertRats={removedDesertRats}, "
                + $"retainedTestEnemies={retainedTestEnemies}/2.");
            return;
        }
        add("battle-npc.zone162-contract", AetherXivDatabasePreflightStatus.Passed,
            "Verified 23 captured Central Shroud ambient enemies, removed four development Desert Rats, and retained the two original test enemies.");

        int zone150PinRows = await CountAsync(connection, """
SELECT COUNT(*)
FROM server_battlenpc_spawn_audit_pins
WHERE zoneId=150
  AND createdByCharacterName='Akhebica Loha'
  AND promotionNote LIKE 'Source dump pin #%';
""", cancellationToken).ConfigureAwait(false);
        int zone150PromotedPins = await CountAsync(connection, """
SELECT COUNT(*)
FROM server_battlenpc_spawn_audit_pins
WHERE zoneId=150
  AND createdByCharacterName='Akhebica Loha'
  AND isPromoted=1
  AND enemyName='Star Marmot'
  AND promotionMigration='20260718_000013_central_shroud_pinspawn_restore';
""", cancellationToken).ConfigureAwait(false);
        int zone150InvalidPromotions = await CountAsync(connection, """
SELECT COUNT(*)
FROM server_battlenpc_spawn_audit_pins
WHERE zoneId=150
  AND promotionNote LIKE 'Source dump pin #%'
  AND isPromoted=1
  AND enemyName<>'Star Marmot';
""", cancellationToken).ConfigureAwait(false);
        int zone150StarMarmotSpawns = await CountAsync(connection, """
SELECT COUNT(*)
FROM server_battlenpc_spawn_locations s
JOIN server_battlenpc_groups g ON g.groupId=s.groupId
JOIN server_battlenpc_pools p ON p.poolId=g.poolId
WHERE s.bnpcId IN (1500001,1500002,1500034,1500035,1500039,1500041,1500042,1500051,1500055,1500056,1500057,1500058,1500060)
  AND g.zoneId=150
  AND g.minLevel=3 AND g.maxLevel=4 AND g.hp=99 AND g.mp=130
  AND p.actorClassId IN (2104009,2104028)
  AND p.genusId=12;
""", cancellationToken).ConfigureAwait(false);
        int zone150ActorClasses = await CountAsync(connection, """
SELECT COUNT(*)
FROM gamedata_actor_class
WHERE (id=2104009 AND classPath='/Chara/Npc/Monster/Lemming/HareStandard' AND displayNameId=3104009)
   OR (id=2104028 AND classPath='/Chara/Npc/Monster/Lemming/HareStandard' AND displayNameId=3104028);
""", cancellationToken).ConfigureAwait(false);
        if (zone150PinRows != 60 || zone150PromotedPins != 11 || zone150InvalidPromotions != 0
            || zone150StarMarmotSpawns != 13 || zone150ActorClasses != 2)
        {
            add("battle-npc.zone150-pinspawn-contract", AetherXivDatabasePreflightStatus.NeedsRepair,
                $"Central Shroud pinspawn restoration is incomplete: pins={zone150PinRows}/60, "
                + $"promoted={zone150PromotedPins}/11, invalidPromotions={zone150InvalidPromotions}, "
                + $"spawns={zone150StarMarmotSpawns}/13, actorClasses={zone150ActorClasses}/2.");
            return;
        }
        add("battle-npc.zone150-pinspawn-contract", AetherXivDatabasePreflightStatus.Passed,
            "Verified 60 imported pinspawn observations, 11 corroborated Bentbranch Star Marmot promotions, and two exact retail-trace spawns.");

        int launcherColumns = await CountAsync(connection, """
SELECT COUNT(*) FROM information_schema.columns
WHERE table_schema=DATABASE() AND (
  (table_name='launcher_config' AND column_name IN ('service_version','server_name','is_active')) OR
  (table_name='launcher_news' AND column_name IN ('news_id','is_active','title_color','summary_color','body_color')) OR
  (table_name='launcher_patch_files' AND column_name IN ('patch_file_id','target_boot_version','target_game_version'))
);
""", cancellationToken).ConfigureAwait(false);
        if (launcherColumns != 11)
        {
            add("launcher.storage-contract", AetherXivDatabasePreflightStatus.NeedsRepair,
                $"Modern Launcher storage bridge is incomplete: columns={launcherColumns}/11.");
            return;
        }
        add("launcher.storage-contract", AetherXivDatabasePreflightStatus.Passed,
            "Modern Launcher/UI storage is available without changing gameplay tables.");

        int zones = await CountAsync(connection, "SELECT COUNT(*) FROM server_zones;", cancellationToken).ConfigureAwait(false);
        int commands = await CountAsync(connection, "SELECT COUNT(*) FROM server_battle_commands;", cancellationToken).ConfigureAwait(false);
        int baseStats = await CountAsync(connection, "SELECT COUNT(*) FROM server_player_base_stats;", cancellationToken).ConfigureAwait(false);
        if (zones == 0 || commands == 0 || baseStats == 0)
        {
            add("direct-core.seed", AetherXivDatabasePreflightStatus.NeedsRepair,
                $"Direct-core seed data is incomplete: zones={zones}, commands={commands}, baseStats={baseStats}.");
            return;
        }
        add("direct-core.seed", AetherXivDatabasePreflightStatus.Passed,
            $"Direct-core seed data is present: zones={zones}, commands={commands}, baseStats={baseStats}.");

        ServerEndpoint world = ParseEndpoint(config.World.Advertise, "World advertise endpoint");
        await using MySqlCommand readWorld = connection.CreateCommand();
        readWorld.CommandText = "SELECT address, port, isActive FROM servers WHERE id=1 LIMIT 1;";
        await using MySqlDataReader reader = await readWorld.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            add("direct-core.world", AetherXivDatabasePreflightStatus.NeedsRepair, "World id 1 is missing from servers; canonical database repair is required.");
            return;
        }
        string address = reader.GetString(0);
        ushort port = reader.GetUInt16(1);
        bool active = reader.GetBoolean(2);
        await reader.DisposeAsync().ConfigureAwait(false);

        if (String.Equals(address, world.Host, StringComparison.OrdinalIgnoreCase) && port == world.Port && active)
        {
            add("direct-core.world", AetherXivDatabasePreflightStatus.Passed,
                $"World id 1 advertises {world.Host}:{world.Port}.");
            return;
        }
        if (!repair)
        {
            add("direct-core.world", AetherXivDatabasePreflightStatus.Missing,
                $"World id 1 advertises {address}:{port} active={active}; expected {world.Host}:{world.Port}.");
            return;
        }

        await using MySqlCommand updateWorld = connection.CreateCommand();
        updateWorld.CommandText = "UPDATE servers SET address=@address, port=@port, isActive=1 WHERE id=1;";
        updateWorld.Parameters.AddWithValue("@address", world.Host);
        updateWorld.Parameters.AddWithValue("@port", world.Port);
        await updateWorld.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        add("direct-core.world", AetherXivDatabasePreflightStatus.Repaired,
            $"World id 1 now advertises {world.Host}:{world.Port}.");
    }

    private static async Task<int> CountAsync(
        MySqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
    }

    private static async Task<string?> ScalarStringAsync(
        MySqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = sql;
        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is null or DBNull ? null : Convert.ToString(result);
    }

    private static ServerEndpoint ParseEndpoint(string raw, string label)
    {
        string[] parts = raw.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !UInt16.TryParse(parts[1], out ushort port))
            throw new InvalidOperationException($"{label} must use host:port syntax: {raw}");
        return new ServerEndpoint(parts[0], port);
    }

    private static bool IsBootstrapCandidate(Exception exception)
    {
        if (exception is MySqlException mysql)
            return mysql.Number is 1044 or 1045 or 1049 or 1142;
        return exception.InnerException is not null && IsBootstrapCandidate(exception.InnerException);
    }

    private static async Task<bool> TableExistsAsync(
        MySqlConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT COUNT(*) FROM information_schema.tables
WHERE table_schema=DATABASE() AND table_name=@tableName;
""";
        command.Parameters.AddWithValue("@tableName", tableName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
    }

    private sealed record ServerEndpoint(string Host, ushort Port);
}
