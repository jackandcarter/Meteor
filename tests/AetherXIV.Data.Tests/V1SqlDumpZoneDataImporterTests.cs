using AetherXIV.Data;

namespace AetherXIV.Data.Tests;

public sealed class V1SqlDumpZoneDataImporterTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), "aetherxiv-zone-import-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ImporterCarriesZoneRuntimeStateNeededByMapZoneIn()
    {
        Directory.CreateDirectory(tempRoot);
        string zonesPath = Path.Combine(tempRoot, "server_zones.sql");
        await File.WriteAllTextAsync(
            zonesPath,
            "INSERT INTO `server_zones` VALUES (209,105,'gri1Town01','Gridania','127.0.0.1',1989,'/Area/Zone/ZoneMasterGriS0',57,58,59,0,1,1,0,0,1);\n");
        V1SqlDumpZoneDataImporter importer = new();

        ZoneRecord zone = Assert.Single(await importer.ImportAsync(zonesPath));

        Assert.Equal(209u, zone.Id.Value);
        Assert.Equal("gri1Town01", zone.Name);
        Assert.Equal(105u, zone.RegionId);
        Assert.True(zone.LoadNavMesh);
        Assert.Equal("/Area/Zone/ZoneMasterGriS0", zone.ClassPath);
        Assert.Equal(57, zone.DayMusic);
        Assert.Equal(58, zone.NightMusic);
        Assert.Equal(59, zone.BattleMusic);
        Assert.True(zone.IsInn);
        Assert.True(zone.CanRideChocobo);
        Assert.False(zone.CanStealth);
        Assert.False(zone.IsInstanceRaid);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
            Directory.Delete(tempRoot, recursive: true);
    }
}
