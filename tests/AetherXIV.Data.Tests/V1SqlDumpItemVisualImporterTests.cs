using AetherXIV.Core;
using AetherXIV.Data;

namespace AetherXIV.Data.Tests;

public sealed class V1SqlDumpItemVisualImporterTests
{
    [Fact]
    public async Task ImporterLoadsMultiRowItemGraphicsAndExtraOffhandRows()
    {
        string root = CreateTempDirectory();
        try
        {
            string graphicsPath = Path.Combine(root, "gamedata_items_graphics.sql");
            string extraPath = Path.Combine(root, "gamedata_items_graphics_extra.sql");
            await File.WriteAllTextAsync(
                graphicsPath,
                """
                INSERT INTO `gamedata_items_graphics` (`catalogID`, `weaponId`, `equipmentId`, `variantId`, `colorId`) VALUES
                    (4020001, 58, 1, 0, 0),
                    (7000001, 0, 4, 3, 5);
                """);
            await File.WriteAllTextAsync(
                extraPath,
                "INSERT INTO `gamedata_items_graphics_extra` VALUES ('4020001', '59', '1', '0');\n");

            V1SqlDumpItemVisualImporter importer = new();
            V1SqlDumpItemVisualDataSet dataSet = await importer.ImportAsync(graphicsPath, extraPath);

            Assert.Empty(dataSet.Warnings);
            Assert.Equal(2, dataSet.ItemVisuals.Count);
            ItemVisualRecord mainHand = dataSet.ItemVisuals.Single(row => row.ItemId == 4020001);
            Assert.Equal(58u, mainHand.WeaponId);
            Assert.Equal(59u, mainHand.OffHandWeaponId);
            Assert.Equal(EvidenceStatus.RepoConfirmed, mainHand.Provenance.Status);
            Assert.Equal("v1-sql", mainHand.Provenance.SourceType);
            Assert.Contains("gamedata_items_graphics_extra:4020001", mainHand.Provenance.SourceRef);
            ItemVisualRecord armor = dataSet.ItemVisuals.Single(row => row.ItemId == 7000001);
            Assert.Equal(0u, armor.WeaponId);
            Assert.Equal(4u, armor.EquipmentId);
            Assert.Equal(3u, armor.VariantId);
            Assert.Equal(5u, armor.ColorId);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetherxiv-item-visual-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
