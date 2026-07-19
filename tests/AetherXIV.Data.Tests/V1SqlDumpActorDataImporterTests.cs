using AetherXIV.Core;
using AetherXIV.Data;
using System.Text.Json;

namespace AetherXIV.Data.Tests;

public sealed class V1SqlDumpActorDataImporterTests
{
    [Fact]
    public async Task ImporterLoadsActorClassAppearanceAndStaticSpawnRowsFromV1SqlDumps()
    {
        string root = CreateTempDirectory();
        try
        {
            string actorClassPath = Path.Combine(root, "gamedata_actor_class.sql");
            string pushCommandPath = Path.Combine(root, "gamedata_actor_pushcommand.sql");
            string appearancePath = Path.Combine(root, "gamedata_actor_appearance.sql");
            string spawnPath = Path.Combine(root, "server_spawn_locations.sql");
            await File.WriteAllTextAsync(
                actorClassPath,
                "INSERT INTO `gamedata_actor_class` VALUES ('1000001', '/Chara/Npc/Populace/PopulaceStandard', '1900006', '19', '{\\r\\n  \\\"talkEventConditions\\\": []\\r\\n}');\n");
            await File.WriteAllTextAsync(
                pushCommandPath,
                "INSERT INTO `gamedata_actor_pushcommand` VALUES ('1000001', '2', '1', '4');\n");
            await File.WriteAllTextAsync(
                appearancePath,
                "INSERT INTO `gamedata_actor_appearance` VALUES (1000001,8,2,4,0,0,32,0,0,0,0,0,0,0,0,0,17,11,9,0,331351046,0,0,0,0,0,0,0,4387,5347,1024,5443,0,0,0,0,0,0,0,0);\n");
            await File.WriteAllTextAsync(
                spawnPath,
                "INSERT INTO `server_spawn_locations` VALUES ('77', '1000001', 'gogofu', '209', '', '0', '10', '20', '30', '1.5', '7', '99', 'Gogofu');\n");

            V1SqlDumpActorDataImporter importer = new();
            V1SqlDumpActorDataSet dataSet = await importer.ImportAsync(
                actorClassPath,
                appearancePath,
                spawnPath,
                pushCommandPath);

            ActorClassRecord actorClass = Assert.Single(dataSet.ActorClasses);
            ActorAppearanceRecord appearance = Assert.Single(dataSet.ActorAppearances);
            StaticActorSpawnRecord spawn = Assert.Single(dataSet.StaticActorSpawns);

            Assert.Empty(dataSet.Warnings);
            Assert.Equal(1000001u, actorClass.ActorClassId);
            Assert.Equal("/Chara/Npc/Populace/PopulaceStandard", actorClass.ClassPath);
            Assert.Equal(2, actorClass.PushCommand);
            Assert.Equal(1, actorClass.PushCommandSub);
            Assert.Equal(4, actorClass.PushCommandPriority);
            Assert.Contains("\"talkEventConditions\"", actorClass.EventConditions);
            Assert.Equal(8u, appearance.Base);
            Assert.Equal(331351046u, appearance.MainHand);
            Assert.Equal(77u, spawn.SpawnId);
            Assert.Equal(new ZoneId(209), spawn.ZoneId);
            Assert.Equal("Gogofu", spawn.CustomDisplayName);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ImportedRepositoriesFeedHydratorWithLoadedAppearanceRows()
    {
        ActorClassRecord actorClass = new(
            1000001,
            "/Chara/Npc/Populace/PopulaceStandard",
            1900006,
            19,
            """{"talkEventConditions":[]}""",
            2,
            1,
            4,
            Provenance("gamedata_actor_class:1000001"));
        ActorAppearanceRecord appearance = new(
            1000001,
            8,
            2,
            4,
            0,
            0,
            32,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            17,
            11,
            9,
            0,
            331351046,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            4387,
            5347,
            1024,
            5443,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            Provenance("gamedata_actor_appearance:1000001"));
        StaticActorSpawnRecord spawn = new(
            77,
            1000001,
            "gogofu",
            new ZoneId(209),
            "",
            0,
            10,
            20,
            30,
            1.5f,
            7,
            99,
            "Gogofu",
            Provenance("server_spawn_locations:77"));
        ImportedActorDataRepositories repositories = new([actorClass], [appearance], [spawn]);

        Assert.Same(actorClass, await repositories.GetAsync(1000001));
        Assert.Same(appearance, await ((IActorAppearanceRepository)repositories).GetAsync(1000001));
        IReadOnlyList<StaticActorSpawnRecord> spawns = await repositories.ListStaticSpawnsAsync(new ZoneId(209));

        Assert.Equal([spawn], spawns);
        Assert.Empty(await repositories.ListBattleNpcSpawnsAsync(new ZoneId(209)));
    }

    [Fact]
    public async Task ActorDataImportArtifactsTrackMissingRelationshipsWithoutPromotingFallbacks()
    {
        string root = CreateTempDirectory();
        string output = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, "gamedata_actor_class.sql"),
                "INSERT INTO `gamedata_actor_class` VALUES ('1000001', '/Chara/Npc/Populace/PopulaceStandard', '1900006', '19', '{bad json');\n");
            await File.WriteAllTextAsync(
                Path.Combine(root, "gamedata_actor_pushcommand.sql"),
                "INSERT INTO `gamedata_actor_pushcommand` VALUES ('1000001', '2', '1', '4');\n");
            await File.WriteAllTextAsync(
                Path.Combine(root, "gamedata_actor_appearance.sql"),
                "INSERT INTO `gamedata_actor_appearance` VALUES (1000002,8,2,4,0,0,32,0,0,0,0,0,0,0,0,0,17,11,9,0,331351046,0,0,0,0,0,0,0,4387,5347,1024,5443,0,0,0,0,0,0,0,0);\n");
            await File.WriteAllTextAsync(
                Path.Combine(root, "server_spawn_locations.sql"),
                "INSERT INTO `server_spawn_locations` VALUES ('77', '1000003', 'gogofu', '209', '', '0', '10', '20', '30', '1.5', '7', '99', 'Gogofu');\n");
            await File.WriteAllTextAsync(
                Path.Combine(root, "server_zones.sql"),
                "INSERT INTO `server_zones` VALUES (209,102,'wil0Town01','Ul\\'dah','127.0.0.1',1989,'/Area/Zone/ZoneMasterWilS0',57,57,0,0,0,0,0,0,1);\n");

            ActorDataImportArtifactWriter writer = new();
            ActorDataImportArtifactResult result = await writer.WriteAsync(new ActorDataImportArtifactRequest(root, output));

            Assert.Equal(1, result.Summary.ActorClassCount);
            Assert.Equal(1, result.Summary.ActorAppearanceCount);
            Assert.Equal(1, result.Summary.StaticActorSpawnCount);
            Assert.Contains(result.Summary.MissingRelationships, item => item.Kind == "StaticSpawnMissingActorClass" && item.ActorClassId == 1000003);
            Assert.Contains(result.Summary.MissingRelationships, item => item.Kind == "ActorClassMissingAppearance" && item.ActorClassId == 1000001);
            Assert.Contains(result.Summary.MissingRelationships, item => item.Kind == "AppearanceMissingActorClass" && item.ActorClassId == 1000002);
            Assert.Contains(result.Summary.MissingRelationships, item => item.Kind == "ActorClassInvalidEventConditions" && item.ActorClassId == 1000001);
            Assert.Contains(result.Summary.MissingRelationshipKindCounts, item => item.Kind == "ActorClassMissingAppearance" && item.Count == 1);

            Assert.True(File.Exists(result.Paths.SummaryJsonPath));
            Assert.True(File.Exists(result.Paths.SummaryMarkdownPath));
            Assert.True(File.Exists(result.Paths.ZonesJsonPath));
            Assert.True(File.Exists(result.Paths.ActorClassesJsonPath));
            Assert.True(File.Exists(result.Paths.ActorAppearancesJsonPath));
            Assert.True(File.Exists(result.Paths.StaticActorSpawnsJsonPath));
            Assert.True(File.Exists(result.Paths.MissingRelationshipsJsonPath));

            string summaryJson = await File.ReadAllTextAsync(result.Paths.SummaryJsonPath);
            using JsonDocument document = JsonDocument.Parse(summaryJson);
            Assert.Equal(4, document.RootElement.GetProperty("MissingRelationshipCount").GetInt32());

            string markdown = await File.ReadAllTextAsync(result.Paths.SummaryMarkdownPath);
            Assert.Contains("This report is a reviewed artifact from v1 SQL dump data.", markdown);
            Assert.Contains("## Missing Relationship Counts", markdown);
            Assert.Contains("StaticSpawnMissingActorClass", markdown);
        }
        finally
        {
            DeleteDirectory(root);
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task ZoneImporterLoadsRepoConfirmedZoneRowsForSpawnForeignKeys()
    {
        string root = CreateTempDirectory();
        try
        {
            string zonesPath = Path.Combine(root, "server_zones.sql");
            await File.WriteAllTextAsync(
                zonesPath,
                "INSERT INTO `server_zones` VALUES (209,102,'wil0Town01','Ul\\'dah','127.0.0.1',1989,'/Area/Zone/ZoneMasterWilS0',57,57,0,0,0,0,0,0,1);\n");

            V1SqlDumpZoneDataImporter importer = new();
            IReadOnlyList<ZoneRecord> zones = await importer.ImportAsync(zonesPath);

            ZoneRecord zone = Assert.Single(zones);
            Assert.Equal(new ZoneId(209), zone.Id);
            Assert.Equal("wil0Town01", zone.Name);
            Assert.Equal(102u, zone.RegionId);
            Assert.True(zone.LoadNavMesh);
            Assert.False(zone.IsPrivate);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static ProvenanceRef Provenance(string sourceRef)
    {
        return new ProvenanceRef(EvidenceStatus.RepoConfirmed, "v1-sql", sourceRef, "test");
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetherxiv-data-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
