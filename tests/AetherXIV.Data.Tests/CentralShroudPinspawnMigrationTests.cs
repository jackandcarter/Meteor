using System.Text.RegularExpressions;

namespace AetherXIV.Data.Tests;

public sealed partial class CentralShroudPinspawnMigrationTests
{
    [Fact]
    public void MigrationImportsOnlyTheSixtyPinspawnRowsAndNormalizesNames()
    {
        string sql = LoadMigration();
        string sourceRows = Section(
            sql,
            "INSERT INTO `tmp_central_shroud_pinspawn`",
            "-- Converge matching local rows");

        MatchCollection rows = SourcePinRowRegex().Matches(sourceRows);
        Assert.Equal(60, rows.Count);
        Assert.Equal(
            Enumerable.Range(1, 60),
            rows.Select(match => Int32.Parse(match.Groups[1].Value)));
        Assert.DoesNotContain("Starm Marmot", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("FOrest Funguar", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("Stumbing Funguar", sql, StringComparison.Ordinal);
        Assert.Contains("'Stumbling Funguar'", sourceRows, StringComparison.Ordinal);
        Assert.Contains("'Akhebica Loha'", sourceRows, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationPromotesOnlyCorroboratedPost119StarMarmotPins()
    {
        string sql = LoadMigration();
        string promotion = Section(
            sql,
            "UPDATE `server_battlenpc_spawn_audit_pins` AS p\nJOIN `tmp_central_shroud_pinspawn` AS s",
            "DROP TEMPORARY TABLE `tmp_central_shroud_pinspawn`");
        Match promoted = PromotedPinsRegex().Match(promotion);

        Assert.True(promoted.Success);
        Assert.Equal(
            new[] { 34, 35, 39, 41, 42, 51, 55, 56, 57, 58, 60 },
            promoted.Groups[1].Value.Split(',').Select(Int32.Parse));
        Assert.Contains("Spore Spoor director-owned enemy; never ambient", sql, StringComparison.Ordinal);
        Assert.Contains("footage predates patch 1.19 enemy redistribution", sql, StringComparison.Ordinal);
        Assert.Contains("exact 1.23b actor/profile pending", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveRowsUseOnlyClientAndRetailTraceConfirmedStarMarmotVariants()
    {
        string sql = LoadMigration();
        string poolRows = Section(
            sql,
            "INSERT INTO `server_battlenpc_pools`",
            "-- maxLevel is exclusive");
        string spawnRows = Section(
            sql,
            "INSERT INTO `server_battlenpc_spawn_locations`",
            "UPDATE `server_battlenpc_spawn_audit_pins` AS p");

        Assert.Contains("(150001, 2104009, 'star_marmot_3104009'", poolRows, StringComparison.Ordinal);
        Assert.Contains("(150002, 2104028, 'star_marmot_3104028'", poolRows, StringComparison.Ordinal);
        Assert.Equal(13, ActiveSpawnRowRegex().Matches(spawnRows).Count);
        Assert.Contains("410.786163, 5.366178, -397.583344, 2.340096", spawnRows, StringComparison.Ordinal);
        Assert.Contains("325.138397, 5.438738, -505.644379, 1.074152", spawnRows, StringComparison.Ordinal);
        Assert.Contains("3, 4, 10, 99, 130", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("Funguar", poolRows, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Chigoe", poolRows, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Firefly", poolRows, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "c308aa4c984eb3e1912383c06e5115c6924713978df43cdb8075335e1bac7d32",
            sql,
            StringComparison.Ordinal);
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
                "20260718_000013_central_shroud_pinspawn_restore.sql");
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the Central Shroud pinspawn migration.");
    }

    private static string Section(string text, string start, string end)
    {
        int startIndex = text.IndexOf(start, StringComparison.Ordinal);
        int endIndex = text.IndexOf(end, startIndex + start.Length, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Missing section start: {start}");
        Assert.True(endIndex > startIndex, $"Missing section end: {end}");
        return text[startIndex..endIndex];
    }

    [GeneratedRegex(@"(?m)^\s*\((\d+),'[^']+'", RegexOptions.CultureInvariant)]
    private static partial Regex SourcePinRowRegex();

    [GeneratedRegex(@"WHERE s\.`sourcePinId` IN \(([\d,]+)\);", RegexOptions.CultureInvariant)]
    private static partial Regex PromotedPinsRegex();

    [GeneratedRegex(@"(?m)^\s*\(150\d{4}, '', 150010[12],", RegexOptions.CultureInvariant)]
    private static partial Regex ActiveSpawnRowRegex();
}
