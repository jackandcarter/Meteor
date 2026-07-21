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
