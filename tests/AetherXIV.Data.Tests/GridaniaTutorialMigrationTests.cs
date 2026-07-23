namespace AetherXIV.Data.Tests;

public sealed class GridaniaTutorialMigrationTests
{
    [Fact]
    public void LatestMigrationConvergesTheCompleteSpawnLookupChain()
    {
        string sql = LoadMigration();

        Assert.StartsWith("START TRANSACTION;", sql.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `server_battlenpc_pools`", sql, StringComparison.Ordinal);
        Assert.Contains("(3, 2290006, 'yda'", sql, StringComparison.Ordinal);
        Assert.Contains("(4, 2290005, 'papalymo'", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `server_battlenpc_groups`", sql, StringComparison.Ordinal);
        Assert.Contains("(3, 3, 'yda'", sql, StringComparison.Ordinal);
        Assert.Contains("(4, 4, 'papalymo'", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO `server_battlenpc_spawn_locations`", sql, StringComparison.Ordinal);
        Assert.Contains("(6, 'yda',      3", sql, StringComparison.Ordinal);
        Assert.Contains("(7, 'papalymo', 4", sql, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(sql, "ON DUPLICATE KEY UPDATE"));
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void NameplateMigrationRemovesInternalScriptLabels()
    {
        string sql = LoadMigration("20260720_000018_gridania_tutorial_nameplates.sql");
        string baseSeed = LoadRepositoryFile("Data", "sql", "server_battlenpc_spawn_locations.sql");

        Assert.StartsWith("START TRANSACTION;", sql.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("SET `actorClassId` = 2290006", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE `poolId` = 3", sql, StringComparison.Ordinal);
        Assert.Contains("SET `actorClassId` = 2290005", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE `poolId` = 4", sql, StringComparison.Ordinal);
        Assert.Contains("SET `customDisplayName` = ''", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE `bnpcId` IN (3, 4, 5, 6, 7)", sql, StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
        foreach (int id in new[] { 3, 4, 5, 6, 7 })
            Assert.Contains($"VALUES ({id},''", baseSeed, StringComparison.Ordinal);
        Assert.DoesNotContain("VALUES (3,'bloodthirsty_wolf'", baseSeed, StringComparison.Ordinal);
    }

    [Fact]
    public void GuildMigrationRestoresTheReviewedPhaseFifteenLayout()
    {
        string sql = LoadMigration("20260722_000019_gridania_man0g1_guilds.sql");

        Assert.StartsWith("-- Restore the retail Man0g1", sql.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("(11,155,'/Area/PrivateArea/PrivateAreaMasterPast','PrivateAreaMasterPast',0", sql, StringComparison.Ordinal);
        foreach (int actorClassId in new[] { 1000028, 1000033, 1000372, 1000460, 1000513, 1000737, 1001072, 1700030 })
            Assert.Contains($"({actorClassId},'/Chara/Npc/Populace/PopulaceStandard'", sql, StringComparison.Ordinal);
        Assert.Contains("(1021,1700030,'soileine_man0g1',206", sql, StringComparison.Ordinal);
        Assert.Contains("(1022,1099046,'conjurers_guild_scene_entry',206", sql, StringComparison.Ordinal);
        Assert.Contains("(1030,1000028,'man0g1_swethyna',155,'PrivateAreaMasterPast',0,-352.5,6.24,-1694,1", sql, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(sql, "ON DUPLICATE KEY UPDATE"));
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void GroweryMigrationRestoresTheReviewedInstanceFourteenAndFifteenContracts()
    {
        string sql = LoadMigration("20260722_000020_gridania_man0g1_growery.sql");

        foreach (int actorClassId in new[] { 1000237, 1000238, 1000239, 1000409, 1000410, 1000411, 1000412 })
            Assert.Contains($"({actorClassId},'/Chara/Npc/Populace/PopulaceStandard'", sql, StringComparison.Ordinal);
        foreach (int emoteId in new[] { 101, 105, 106, 107, 108, 122 })
            Assert.Contains($"\"emoteId\":{emoteId}", sql, StringComparison.Ordinal);
        Assert.Contains("(1033,1090384,'man0g1_instance14_boundary',155,'PrivateAreaMasterPast',1,-223.08,12,-1498.546,-1.64", sql, StringComparison.Ordinal);
        Assert.Contains("(1035,1000410,'man0g1_instance14_aunille',155,'PrivateAreaMasterPast',1,-217.52,12,-1497.75,-1.64", sql, StringComparison.Ordinal);
        Assert.Contains("(1042,1000237,'man0g1_instance15_fufucha',155,'PrivateAreaMasterPast',2,-228.37,12,-1498.185,1.62", sql, StringComparison.Ordinal);
        Assert.Contains("(1049,1090201,'man0g1_instance15_children'", sql, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(sql, "ON DUPLICATE KEY UPDATE"));
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void EscortMigrationRestoresTheRemainingQuestChain()
    {
        string sql = LoadMigration("20260722_000021_gridania_man0g1_escort_and_completion.sql");

        foreach (int actorClassId in new[] { 1090202, 1090203, 1090204 })
            Assert.Contains($"({actorClassId},'/Chara/Npc/Populace/PopulaceStandard'", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE `id` IN (2290009,2290010)", sql, StringComparison.Ordinal);
        Assert.Contains("ChigoeLesserStandard", sql, StringComparison.Ordinal);
        Assert.Contains("(1050,1090202,'man0g1_white_wolf_gate',155", sql, StringComparison.Ordinal);
        Assert.Contains("(1052,1090204,'man0g1_lifemend_exit',150,'PrivateAreaMasterPast',1,-756.77", sql, StringComparison.Ordinal);
        Assert.Contains("(120,2290009,'escort_powle'", sql, StringComparison.Ordinal);
        Assert.Contains("(121,2290010,'escort_sansa'", sql, StringComparison.Ordinal);
        Assert.Contains("(122,2205603,'ankle_biter'", sql, StringComparison.Ordinal);
        Assert.Contains("(122,122,'ankle_biter_gridania',5,7,0,60,", sql, StringComparison.Ordinal);
        foreach (int spawnId in Enumerable.Range(34, 8))
            Assert.Contains($"({spawnId},'", sql, StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void EscortBalanceMigrationKeepsTheAppliedEscortMigrationImmutable()
    {
        string sql = LoadMigration("20260722_000022_gridania_man0g1_escort_balance.sql");

        Assert.Contains("UPDATE `server_battlenpc_groups`", sql, StringComparison.Ordinal);
        Assert.Contains("SET `hp`=3", sql, StringComparison.Ordinal);
        Assert.Contains("`groupId`=122", sql, StringComparison.Ordinal);
        Assert.Contains("`poolId`=122", sql, StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void EscortBoundaryMigrationRestoresTheRetailMovingMinimapHalo()
    {
        string sql = LoadMigration("20260722_000023_gridania_man0g1_escort_boundary.sql");

        Assert.Contains("`id`=1290003", sql, StringComparison.Ordinal);
        Assert.Contains("/Chara/Npc/Object/ContentPrivateAreaRange", sql, StringComparison.Ordinal);
        Assert.Contains("\"conditionName\":\"exit\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"conditionName\":\"caution\"", sql, StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void EscortBoundaryPolarityMigrationTriggersOnlyWhenLeavingTheHalo()
    {
        string sql = LoadMigration("20260722_000024_gridania_man0g1_escort_boundary_polarity.sql");

        Assert.Contains("`id`=1290003", sql, StringComparison.Ordinal);
        Assert.Contains("/Chara/Npc/Object/ContentPrivateAreaRange", sql, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(sql, "\"outwards\":true"));
        Assert.DoesNotContain("\"outwards\":false", sql, StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
    }

    [Fact]
    public void EscortPresentationMigrationRestoresLocalizedAnimatedActors()
    {
        string sql = LoadMigration("20260722_000025_gridania_man0g1_escort_actor_presentation.sql");

        Assert.Contains("`id` IN (2290009,2290010,2205603)", sql, StringComparison.Ordinal);
        Assert.Contains("`propertyFlags`=23", sql, StringComparison.Ordinal);
        Assert.Contains("\"conditionName\":\"noticeEvent\"", sql, StringComparison.Ordinal);
        Assert.Contains("SET `customDisplayName`=''", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE `bnpcId` BETWEEN 34 AND 41", sql, StringComparison.Ordinal);
        Assert.EndsWith("COMMIT;", sql.TrimEnd(), StringComparison.Ordinal);
    }

    private static string LoadMigration()
    {
        return LoadMigration("20260720_000017_gridania_tutorial_spawn_contract.sql");
    }

    private static string LoadMigration(string fileName)
    {
        return LoadRepositoryFile("db", "direct-core", "migrations", fileName);
    }

    private static string LoadRepositoryFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = parts.Aggregate(directory.FullName, Path.Combine);
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file {Path.Combine(parts)}.");
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }
        return count;
    }
}
