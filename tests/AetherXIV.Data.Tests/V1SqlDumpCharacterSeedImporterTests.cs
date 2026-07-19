using System.Buffers.Binary;
using System.Text;
using AetherXIV.Core;
using AetherXIV.Data;

namespace AetherXIV.Data.Tests;

public sealed class V1SqlDumpCharacterSeedImporterTests
{
    [Fact]
    public async Task ImporterBuildsNativeCharacterSeedFromV1SqlRows()
    {
        string root = CreateTempDirectory();
        try
        {
            await WriteCharacterSeedSqlAsync(root);

            V1SqlDumpCharacterSeedImporter importer = new();
            V1SqlDumpCharacterSeedDataSet dataSet = await importer.ImportAsync(
                root,
                targetAccountId: new AccountId(99),
                targetWorldId: new WorldId(1));

            Assert.Empty(dataSet.Warnings);
            V1SqlDumpCharacterSeedRecord seed = Assert.Single(dataSet.Characters);
            Assert.Equal(42u, seed.Character.Id.Value);
            Assert.Equal(99u, seed.Character.AccountId.Value);
            Assert.Equal(1u, seed.Character.WorldId.Value);
            Assert.Equal("Tester", seed.Character.Name);
            Assert.Equal(209u, seed.Character.CurrentZoneId.Value);
            Assert.Equal(1, seed.Character.Slot);
            Assert.Equal(10f, seed.Character.PositionX);
            Assert.Equal(20f, seed.Character.PositionY);
            Assert.Equal(30f, seed.Character.PositionZ);
            Assert.Equal(1.5f, seed.Character.Rotation);

            Assert.Equal(CharacterModelIds.FromTribe(3), seed.Appearance.ModelId);
            Assert.Equal(3, seed.Appearance.Tribe);
            Assert.Equal(2u, seed.Appearance.Size);
            Assert.Equal(111u, seed.Appearance.MainHand);
            Assert.Equal(114u, seed.Appearance.Body);
            Assert.Equal(124u, seed.Appearance.LeftEar);
            Assert.Equal(125u, seed.Appearance.RightEar);

            CharacterClassStateRecord classState = Assert.Single(seed.ClassStates);
            Assert.Equal(3, classState.ClassId);
            Assert.Equal(12, classState.Level);
            Assert.Equal(345u, classState.Experience);
            Assert.True(classState.IsCurrent);

            Assert.Equal(2, seed.InventoryItems.Count);
            CharacterInventoryItemRecord inventoryItem = seed.InventoryItems.Single(item => item.ServerItemId == 1002);
            Assert.Equal(0, inventoryItem.ContainerId);
            Assert.Equal(5, inventoryItem.SlotId);
            Assert.Equal(7000001u, inventoryItem.ItemId);
            Assert.Equal(3, inventoryItem.Quantity);
            Assert.Equal(1002u, inventoryItem.ServerItemId);
            Assert.Equal(1, inventoryItem.Quality);

            CharacterEquipmentSlotRecord equipmentSlot = Assert.Single(seed.EquipmentSlots);
            Assert.Equal(0, equipmentSlot.SlotId);
            Assert.Equal(4020001u, equipmentSlot.ItemId);
            Assert.Equal(1001u, equipmentSlot.ServerItemId);
            Assert.Equal(0, equipmentSlot.InventoryContainerId);
            Assert.Equal(0, equipmentSlot.InventorySlotId);

            Assert.Equal(0x190, seed.LobbyAppearancePayload.Length);
            byte[] decodedLobbyAppearance = DecodeLobbyAppearancePayload(seed.LobbyAppearancePayload.Span);
            Assert.Equal(0xF5, decodedLobbyAppearance.Length);
            Assert.Equal(0x000004C0u, BinaryPrimitives.ReadUInt32LittleEndian(decodedLobbyAppearance.AsSpan(0x00)));
            Assert.Equal(0x232327EAu, BinaryPrimitives.ReadUInt32LittleEndian(decodedLobbyAppearance.AsSpan(0x04)));
            Assert.True(ContainsSequence(decodedLobbyAppearance, Encoding.UTF8.GetBytes("Tester\0")));
            Assert.True(ContainsSequence(decodedLobbyAppearance, Encoding.UTF8.GetBytes("defaultTerritory\0")));

            Assert.Equal("prv0Inn01", seed.MapState.PrivateAreaName);
            Assert.Equal(2u, seed.MapState.PrivateAreaLevel);
            Assert.Equal(209u, seed.MapState.ZoneId.Value);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task ImporterSkipsCompletedCharacterWithoutAppearance()
    {
        string root = CreateTempDirectory();
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, "characters.sql"),
                """
                INSERT INTO `characters` VALUES
                ('42','7','1','1','Tester','2','2020-01-01','0','0','0','10','20','30','1.5','0','209','','0','0','0','6','12','7','2','3','0','127','127','127','0','0','0','','0','0');
                """);

            V1SqlDumpCharacterSeedImporter importer = new();
            V1SqlDumpCharacterSeedDataSet dataSet = await importer.ImportAsync(root);

            Assert.Empty(dataSet.Characters);
            Assert.Contains(dataSet.Warnings, warning => warning.Contains("missing characters_appearance row", StringComparison.Ordinal));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static async Task WriteCharacterSeedSqlAsync(string root)
    {
        await File.WriteAllTextAsync(
            Path.Combine(root, "characters.sql"),
            """
            INSERT INTO `characters` VALUES
            ('42','7','1','1','Tester','2','2020-01-01','0','0','0','10','20','30','1.5','0','209','prv0Inn01','2','0','0','6','12','7','2','3','0','127','127','127','0','0','0','','0','0');
            """);
        await File.WriteAllTextAsync(
            Path.Combine(root, "characters_appearance.sql"),
            """
            INSERT INTO `characters_appearance` VALUES
            ('1','42','4294967295','2','44','100','200','300','400','5','600','1','2','3','4','5','6','7','8','9','10','111','112','113','114','115','116','117','118','119','120','121','122','123','124','125');
            """);
        await File.WriteAllTextAsync(
            Path.Combine(root, "characters_parametersave.sql"),
            "INSERT INTO `characters_parametersave` VALUES ('42','100','100','50','50','3','12');\n");
        await File.WriteAllTextAsync(
            Path.Combine(root, "characters_class_levels.sql"),
            "INSERT INTO `characters_class_levels` VALUES ('42','0','12','0','0','0','0','0','0','0','0','0','0','0','0','0','0','0','0');\n");
        await File.WriteAllTextAsync(
            Path.Combine(root, "characters_class_exp.sql"),
            "INSERT INTO `characters_class_exp` VALUES ('42','0','345','0','0','0','0','0','0','0','0','0','0','0','0','0','0','0','0');\n");
        await File.WriteAllTextAsync(
            Path.Combine(root, "server_items.sql"),
            """
            INSERT INTO `server_items` VALUES
            ('1001','4020001','1','1'),
            ('1002','7000001','3','1');
            """);
        await File.WriteAllTextAsync(
            Path.Combine(root, "characters_inventory.sql"),
            """
            INSERT INTO `characters_inventory` VALUES
            ('42','1001','0','0'),
            ('42','1002','0','5');
            """);
        await File.WriteAllTextAsync(
            Path.Combine(root, "characters_inventory_equipment.sql"),
            "INSERT INTO `characters_inventory_equipment` VALUES ('42','3','0','1001');\n");
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetherxiv-character-seed-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }

    private static bool ContainsSequence(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
    {
        return haystack.IndexOf(needle) >= 0;
    }

    private static byte[] DecodeLobbyAppearancePayload(ReadOnlySpan<byte> payload)
    {
        string encoded = Encoding.ASCII.GetString(payload).TrimEnd('\0');
        return Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/'));
    }
}
