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

    private static string LoadMigration()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "db",
                "direct-core",
                "migrations",
                "20260720_000017_gridania_tutorial_spawn_contract.sql");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the Gridania tutorial spawn migration.");
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
