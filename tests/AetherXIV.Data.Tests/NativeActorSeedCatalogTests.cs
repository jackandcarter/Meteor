using AetherXIV.Data;
using System.Security.Cryptography;
using System.Text.Json;

namespace AetherXIV.Data.Tests;

public sealed class NativeActorSeedCatalogTests
{
    [Fact]
    public async Task PackagedCatalogIsCompleteForStaticRuntimeHydration()
    {
        NativeActorSeedCatalog catalog = await NativeActorSeedCatalog.LoadAsync(FindSeedRoot());

        Assert.Equal("static-actor-catalog", catalog.Manifest.SeedId);
        Assert.Equal(111, catalog.Zones.Count);
        Assert.Equal(852, catalog.ActorClasses.Count);
        Assert.Equal(835, catalog.ActorAppearances.Count);
        Assert.Equal(1007, catalog.StaticActorSpawns.Count);
        Assert.Equal(3, catalog.Manifest.ExcludedOrphanSpawnCount);
        Assert.Equal(3, catalog.Manifest.ExcludedInvalidActorClassSpawnCount);
        HashSet<uint> classIds = catalog.ActorClasses.Select(row => row.ActorClassId).ToHashSet();
        Assert.All(catalog.StaticActorSpawns, spawn => Assert.Contains(spawn.ActorClassId, classIds));
        Assert.All(catalog.ActorClasses, actorClass => JsonDocument.Parse(actorClass.EventConditions).Dispose());
    }

    [Theory]
    [InlineData(230u, 1u, 11)]
    [InlineData(155u, 1u, 8)]
    [InlineData(175u, 3u, 14)]
    [InlineData(155u, 0u, 10)]
    public async Task PackagedCatalogContainsOpeningCityPastActors(uint zoneId, uint level, int expectedCount)
    {
        NativeActorSeedCatalog catalog = await NativeActorSeedCatalog.LoadAsync(FindSeedRoot());

        StaticActorSpawnRecord[] actors = catalog.StaticActorSpawns
            .Where(row => row.ZoneId.Value == zoneId)
            .Where(row => String.Equals(row.PrivateAreaName, "PrivateAreaMasterPast", StringComparison.Ordinal))
            .Where(row => row.PrivateAreaLevel == level)
            .ToArray();

        Assert.Equal(expectedCount, actors.Length);
    }

    [Fact]
    public async Task ReviewedNpcServiceEvidenceMatchesNativeActorMirror()
    {
        string dataRoot = FindDataRoot();
        NativeActorSeedCatalog catalog = await NativeActorSeedCatalog.LoadAsync(Path.Combine(dataRoot, "seeds", "actor-catalog"));
        string evidencePath = Path.Combine(dataRoot, "seeds", "npc-services", "spawn-evidence.json");
        string manifestPath = Path.Combine(dataRoot, "seeds", "npc-services", "manifest.json");
        using JsonDocument evidence = JsonDocument.Parse(await File.ReadAllTextAsync(evidencePath));
        using JsonDocument manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        JsonElement records = evidence.RootElement.GetProperty("records");

        Assert.Equal(23, records.GetArrayLength());
        Assert.Equal("2012.09.19.0001", evidence.RootElement.GetProperty("clientBuild").GetString());
        Assert.Equal(23, manifest.RootElement.GetProperty("recordCount").GetInt32());
        string expectedHash = manifest.RootElement.GetProperty("files").GetProperty("spawn-evidence.json").GetString()!;
        string actualHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(evidencePath))).ToLowerInvariant();
        Assert.Equal(expectedHash, actualHash);

        HashSet<uint> evidenceSpawns = new();
        foreach (JsonElement record in records.EnumerateArray())
        {
            uint spawnId = record.GetProperty("spawnId").GetUInt32();
            uint actorClassId = record.GetProperty("actorClassId").GetUInt32();
            uint zoneId = record.GetProperty("zoneId").GetUInt32();
            Assert.True(evidenceSpawns.Add(spawnId), $"Duplicate reviewed spawn {spawnId}.");

            StaticActorSpawnRecord spawn = Assert.Single(catalog.StaticActorSpawns, row => row.SpawnId == spawnId);
            Assert.Equal(actorClassId, spawn.ActorClassId);
            Assert.Equal(zoneId, spawn.ZoneId.Value);
            Assert.Equal(record.GetProperty("uniqueId").GetString(), spawn.UniqueId);
            Assert.Equal(record.GetProperty("privateAreaName").GetString(), spawn.PrivateAreaName);
            Assert.Equal(record.GetProperty("animationId").GetUInt32(), spawn.AnimationId);
            JsonElement transform = record.GetProperty("transform");
            Assert.Equal(transform.GetProperty("x").GetSingle(), spawn.PositionX, 3);
            Assert.Equal(transform.GetProperty("y").GetSingle(), spawn.PositionY, 3);
            Assert.Equal(transform.GetProperty("z").GetSingle(), spawn.PositionZ, 3);
            Assert.Equal(transform.GetProperty("rotation").GetSingle(), spawn.Rotation, 3);

            ActorClassRecord actorClass = Assert.Single(catalog.ActorClasses, row => row.ActorClassId == actorClassId);
            Assert.Equal(record.GetProperty("classPath").GetString(), actorClass.ClassPath);
            Assert.Equal(record.GetProperty("displayNameId").GetUInt32(), actorClass.DisplayNameId);
            using (JsonDocument.Parse(actorClass.EventConditions)) { }
            ActorAppearanceRecord appearance = Assert.Single(
                catalog.ActorAppearances,
                row => row.ActorClassId == actorClassId);
            JsonElement expectedAppearance = record.GetProperty("appearance");
            Assert.Equal(expectedAppearance.GetProperty("base").GetUInt32(), appearance.Base);
            Assert.Equal(expectedAppearance.GetProperty("size").GetUInt32(), appearance.Size);
            Assert.True(File.Exists(Path.Combine(dataRoot, "scripts", record.GetProperty("script").GetString()!)));
        }

        JsonElement deferred = Assert.Single(evidence.RootElement.GetProperty("deferred").EnumerateArray());
        Assert.Contains("exact actor class and transform", deferred.GetProperty("reason").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ZoneServiceMigrationUpsertsEveryRuntimeCompatibilityTableIdempotently()
    {
        string migrationPath = Path.Combine(
            Directory.GetParent(FindDataRoot())!.FullName,
            "db",
            "direct-core",
            "migrations",
            "20260718_000008_zone_service_npcs.sql");
        string sql = File.ReadAllText(migrationPath);

        Assert.Contains("INSERT INTO `gamedata_actor_class`", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `gamedata_actor_appearance`", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `server_spawn_locations`", sql, StringComparison.Ordinal);
        Assert.True(
            sql.Split("ON DUPLICATE KEY UPDATE", StringSplitOptions.None).Length >= 6,
            "Every canonical catalog/spawn insert must converge when the migration is reapplied.");
        foreach (uint actorClassId in new uint[]
                 {
                     1000840, 1080101, 1200022, 1200044, 1500006, 1500061, 1500114,
                     1500115, 1500116, 1500238, 1500252, 1500255, 1500261, 1500428
                 })
        {
            Assert.Contains(actorClassId.ToString(), sql, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ChocoboStopMigrationPinsTraceBackedRuntimeConditionsIdempotently()
    {
        string migrationPath = Path.Combine(
            Directory.GetParent(FindDataRoot())!.FullName,
            "db",
            "direct-core",
            "migrations",
            "20260718_000009_chocobo_stop_runtime.sql");
        string sql = File.ReadAllText(migrationPath);

        Assert.Contains("1090464", sql, StringComparison.Ordinal);
        Assert.Contains("/Chara/Npc/Object/ChocoboStop", sql, StringComparison.Ordinal);
        Assert.Contains("pushDefault", sql, StringComparison.Ordinal);
        Assert.Contains("_!pushRequest", sql, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ImmortalFlamesShopMigrationPinsTheReviewedHallPlacementIdempotently()
    {
        string migrationPath = Path.Combine(
            Directory.GetParent(FindDataRoot())!.FullName,
            "db",
            "direct-core",
            "migrations",
            "20260718_000010_immortal_flames_company_shop.sql");
        string sql = File.ReadAllText(migrationPath);

        Assert.Contains("1500201", sql, StringComparison.Ordinal);
        Assert.Contains("/Chara/Npc/Populace/PopulaceCompanyShop", sql, StringComparison.Ordinal);
        Assert.Contains("(1019,1500201,'flame_company_shop',233,'',0,169,0,-177.5,-1.5", sql, StringComparison.Ordinal);
        Assert.Contains("(1020,1090264,'hall_of_flames_exit',233,'',0,160,0,-142.76", sql, StringComparison.Ordinal);
        Assert.Contains("49169ae25b034ed65bcec9e4abdc68a6fb229f52", sql, StringComparison.Ordinal);
        Assert.True(sql.Split("ON DUPLICATE KEY UPDATE", StringSplitOptions.None).Length >= 5);
    }

    [Fact]
    public void UldahCompanyOfficeEntranceMigrationPinsTheTraceBackedTriggerIdempotently()
    {
        string migrationPath = Path.Combine(
            Directory.GetParent(FindDataRoot())!.FullName,
            "db",
            "direct-core",
            "migrations",
            "20260718_000011_uldah_company_office_entrance.sql");
        string sql = File.ReadAllText(migrationPath);

        Assert.Contains("1090265", sql, StringComparison.Ordinal);
        Assert.Contains("/Chara/Npc/Object/MarketEntrance", sql, StringComparison.Ordinal);
        Assert.Contains("\"bgObj\":4143", sql, StringComparison.Ordinal);
        Assert.Contains("\"layout\":421", sql, StringComparison.Ordinal);
        Assert.Contains("\"reactName\":\"dtwi\"", sql, StringComparison.Ordinal);
        Assert.Contains("company-office-entrance-immortal-flames", sql, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void HallOfFlamesExitMigrationPinsTheTraceBackedActorClassTriggerIdempotently()
    {
        string migrationPath = Path.Combine(
            Directory.GetParent(FindDataRoot())!.FullName,
            "db",
            "direct-core",
            "migrations",
            "20260718_000012_hall_of_flames_exit.sql");
        string sql = File.ReadAllText(migrationPath);

        Assert.Contains("1090264", sql, StringComparison.Ordinal);
        Assert.Contains("\"bgObj\":3322", sql, StringComparison.Ordinal);
        Assert.Contains("\"layout\":321", sql, StringComparison.Ordinal);
        Assert.Contains("\"conditionName\":\"in\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"reactName\":\"dtwi\"", sql, StringComparison.Ordinal);
        Assert.Contains("company-office-exit-immortal-flames", sql, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void HallOfFlamesRuntimeCorrectionUsesTheUldahObjectPairIdempotently()
    {
        string migrationPath = Path.Combine(
            Directory.GetParent(FindDataRoot())!.FullName,
            "db",
            "direct-core",
            "migrations",
            "20260718_000014_hall_of_flames_exit_runtime.sql");
        string sql = File.ReadAllText(migrationPath);

        Assert.Contains("1090264", sql, StringComparison.Ordinal);
        Assert.Contains("\"bgObj\":4143", sql, StringComparison.Ordinal);
        Assert.Contains("\"layout\":421", sql, StringComparison.Ordinal);
        Assert.Contains("\"conditionName\":\"in\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"reactName\":\"dtwi\"", sql, StringComparison.Ordinal);
        Assert.Contains("159.57256/0/-144.57875", sql, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void HallOfFlamesPushCircleSupersedesTheDisprovedTriggerBoxesIdempotently()
    {
        string migrationPath = Path.Combine(
            Directory.GetParent(FindDataRoot())!.FullName,
            "db",
            "direct-core",
            "migrations",
            "20260719_000015_hall_of_flames_push_circle.sql");
        string sql = File.ReadAllText(migrationPath);

        Assert.Contains("1090264", sql, StringComparison.Ordinal);
        Assert.Contains("pushWithCircleEventConditions", sql, StringComparison.Ordinal);
        Assert.Contains("\"conditionName\":\"pushDefault\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"radius\":4.0", sql, StringComparison.Ordinal);
        Assert.Contains("\"secondaryRadius\":10.0", sql, StringComparison.Ordinal);
        Assert.Contains("\"silent\":false", sql, StringComparison.Ordinal);
        Assert.Contains("\"useSourceActorId\":true", sql, StringComparison.Ordinal);
        Assert.Contains("MarketEntrance.cs", sql, StringComparison.Ordinal);
        Assert.Contains("ON DUPLICATE KEY UPDATE", sql, StringComparison.Ordinal);
    }

    private static string FindSeedRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Data", "seeds", "actor-catalog");
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
                return candidate;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate Data/seeds/actor-catalog.");
    }

    private static string FindDataRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Data");
            if (File.Exists(Path.Combine(candidate, "seeds", "npc-services", "manifest.json")))
                return candidate;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate Data/seeds/npc-services.");
    }
}
