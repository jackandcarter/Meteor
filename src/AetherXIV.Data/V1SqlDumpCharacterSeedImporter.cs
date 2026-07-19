using System.Globalization;
using AetherXIV.Core;

namespace AetherXIV.Data;

public sealed record V1SqlDumpCharacterSeedRecord(
    CharacterRecord Character,
    CharacterAppearanceRecord Appearance,
    ReadOnlyMemory<byte> LobbyAppearancePayload,
    CharacterMapStateRecord MapState,
    IReadOnlyList<CharacterClassStateRecord> ClassStates,
    IReadOnlyList<CharacterInventoryItemRecord> InventoryItems,
    IReadOnlyList<CharacterEquipmentSlotRecord> EquipmentSlots);

public sealed record V1SqlDumpCharacterSeedDataSet(
    IReadOnlyList<V1SqlDumpCharacterSeedRecord> Characters,
    IReadOnlyList<string> Warnings);

public sealed class V1SqlDumpCharacterSeedImporter
{
    private static readonly (string Column, byte ClassId)[] ClassColumns =
    [
        ("pug", 2),
        ("gla", 3),
        ("mrd", 4),
        ("arc", 7),
        ("lnc", 8),
        ("thm", 22),
        ("cnj", 23),
        ("crp", 29),
        ("bsm", 30),
        ("arm", 31),
        ("gsm", 32),
        ("ltw", 33),
        ("wvr", 34),
        ("alc", 35),
        ("cul", 36),
        ("min", 39),
        ("btn", 40),
        ("fsh", 41)
    ];

    public async Task<V1SqlDumpCharacterSeedDataSet> ImportAsync(
        string v1SqlRootPath,
        AccountId? targetAccountId = null,
        WorldId? targetWorldId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(v1SqlRootPath);

        string root = Path.GetFullPath(v1SqlRootPath);
        List<string> warnings = [];
        Dictionary<uint, LegacyCharacterRow> characters = await ReadCharactersAsync(
            Path.Combine(root, "characters.sql"),
            warnings,
            cancellationToken).ConfigureAwait(false);
        Dictionary<uint, LegacyAppearanceRow> appearances = await ReadAppearancesAsync(
            Path.Combine(root, "characters_appearance.sql"),
            warnings,
            cancellationToken).ConfigureAwait(false);
        Dictionary<uint, LegacyParameterSaveRow> parameterSaves = await ReadParameterSavesAsync(
            Path.Combine(root, "characters_parametersave.sql"),
            cancellationToken).ConfigureAwait(false);
        Dictionary<uint, Dictionary<byte, ushort>> classLevels = await ReadClassLevelsAsync(
            Path.Combine(root, "characters_class_levels.sql"),
            cancellationToken).ConfigureAwait(false);
        Dictionary<uint, Dictionary<byte, uint>> classExperience = await ReadClassExperienceAsync(
            Path.Combine(root, "characters_class_exp.sql"),
            cancellationToken).ConfigureAwait(false);
        Dictionary<uint, LegacyServerItemRow> serverItems = await ReadServerItemsAsync(
            Path.Combine(root, "server_items.sql"),
            cancellationToken).ConfigureAwait(false);
        Dictionary<uint, IReadOnlyList<CharacterInventoryItemRecord>> inventory = await ReadInventoryAsync(
            Path.Combine(root, "characters_inventory.sql"),
            serverItems,
            warnings,
            cancellationToken).ConfigureAwait(false);
        Dictionary<uint, IReadOnlyList<LegacyEquipmentRow>> equipment = await ReadEquipmentAsync(
            Path.Combine(root, "characters_inventory_equipment.sql"),
            warnings,
            cancellationToken).ConfigureAwait(false);

        List<V1SqlDumpCharacterSeedRecord> records = [];
        foreach (LegacyCharacterRow legacyCharacter in characters.Values.OrderBy(row => row.Slot).ThenBy(row => row.CharacterId))
        {
            if (legacyCharacter.State != 2)
                continue;

            if (!appearances.TryGetValue(legacyCharacter.CharacterId, out LegacyAppearanceRow? legacyAppearance))
            {
                warnings.Add($"Skipped character {legacyCharacter.CharacterId}: missing characters_appearance row.");
                continue;
            }

            parameterSaves.TryGetValue(legacyCharacter.CharacterId, out LegacyParameterSaveRow? parameterSave);
            if (parameterSave is null)
                warnings.Add($"Character {legacyCharacter.CharacterId} has no characters_parametersave row; current class is unknown.");

            AccountId accountId = targetAccountId ?? new AccountId(legacyCharacter.AccountId);
            WorldId worldId = targetWorldId ?? new WorldId(legacyCharacter.WorldId == 0 ? 1u : legacyCharacter.WorldId);
            CharacterRecord character = new(
                new CharacterId(legacyCharacter.CharacterId),
                accountId,
                worldId,
                legacyCharacter.Name,
                new ZoneId(legacyCharacter.CurrentZoneId),
                legacyCharacter.PositionX,
                legacyCharacter.PositionY,
                legacyCharacter.PositionZ,
                legacyCharacter.Rotation,
                legacyCharacter.Slot);
            CharacterAppearanceRecord appearance = ToAppearance(legacyCharacter, legacyAppearance);
            IReadOnlyList<CharacterClassStateRecord> classStates = BuildClassStates(
                character.Id,
                parameterSave,
                classLevels.GetValueOrDefault(legacyCharacter.CharacterId),
                classExperience.GetValueOrDefault(legacyCharacter.CharacterId));
            if (classStates.Count == 0)
                warnings.Add($"Character {legacyCharacter.CharacterId} has no class state rows to import.");

            IReadOnlyList<CharacterEquipmentSlotRecord> equipmentSlots = BuildEquipmentSlots(
                character.Id,
                parameterSave?.MainSkill ?? 0,
                equipment.GetValueOrDefault(legacyCharacter.CharacterId) ?? [],
                inventory.GetValueOrDefault(legacyCharacter.CharacterId) ?? [],
                serverItems,
                warnings);
            byte currentClass = parameterSave?.MainSkill ?? classStates.FirstOrDefault(row => row.IsCurrent)?.ClassId ?? 0;
            ushort currentLevel = parameterSave?.MainSkillLevel ?? classStates.FirstOrDefault(row => row.IsCurrent)?.Level ?? 0;
            byte[] lobbyPayload = LobbyAppearancePayloadBuilder.Build(
                legacyCharacter.Name,
                appearance,
                new CharacterCreationPayloadInfo(
                    legacyCharacter.Tribe,
                    legacyCharacter.Guardian,
                    legacyCharacter.BirthMonth,
                    legacyCharacter.BirthDay,
                    currentClass,
                    legacyCharacter.InitialTown),
                currentLevel);

            CharacterMapStateRecord mapState = new(
                character.Id,
                character.CurrentZoneId,
                String.IsNullOrWhiteSpace(legacyCharacter.PrivateAreaName) ? null : legacyCharacter.PrivateAreaName,
                legacyCharacter.PrivateAreaLevel,
                character.PositionX,
                character.PositionY,
                character.PositionZ,
                character.Rotation,
                DateTimeOffset.UtcNow);

            records.Add(new V1SqlDumpCharacterSeedRecord(
                character,
                appearance,
                lobbyPayload,
                mapState,
                classStates,
                inventory.GetValueOrDefault(legacyCharacter.CharacterId) ?? [],
                equipmentSlots));
        }

        return new V1SqlDumpCharacterSeedDataSet(records, warnings);
    }

    private static async Task<Dictionary<uint, LegacyCharacterRow>> ReadCharactersAsync(
        string path,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        Dictionary<uint, LegacyCharacterRow> rows = [];
        await foreach (SqlDumpInsertRow row in ReadRowsIfExistsAsync(path, "characters", cancellationToken).ConfigureAwait(false))
        {
            if (row.Values.Count < 25)
            {
                warnings.Add($"{row.SourceRef} has {row.Values.Count} values; expected at least 25.");
                continue;
            }

            uint characterId = ToUInt32(row.Values[0]);
            rows[characterId] = new LegacyCharacterRow(
                characterId,
                ToUInt32(row.Values[1]),
                ToUInt16(row.Values[2]),
                ToUInt32(row.Values[3]),
                row.Values[4] ?? String.Empty,
                ToUInt16(row.Values[5]),
                ToSingle(row.Values[10]),
                ToSingle(row.Values[11]),
                ToSingle(row.Values[12]),
                ToSingle(row.Values[13]),
                ToUInt32(row.Values[15]),
                row.Values[16],
                ToUInt32(row.Values[17]),
                ToByte(row.Values[20]),
                ToByte(row.Values[21]),
                ToByte(row.Values[22]),
                ToByte(row.Values[23]),
                ToByte(row.Values[24]));
        }

        return rows;
    }

    private static async Task<Dictionary<uint, LegacyAppearanceRow>> ReadAppearancesAsync(
        string path,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        Dictionary<uint, LegacyAppearanceRow> rows = [];
        await foreach (SqlDumpInsertRow row in ReadRowsIfExistsAsync(path, "characters_appearance", cancellationToken).ConfigureAwait(false))
        {
            if (row.Values.Count < 36)
            {
                warnings.Add($"{row.SourceRef} has {row.Values.Count} values; expected 36.");
                continue;
            }

            uint characterId = ToUInt32(row.Values[1]);
            rows[characterId] = new LegacyAppearanceRow(
                characterId,
                ToUInt32(row.Values[2]),
                ToUInt32(row.Values[3]),
                ToUInt32(row.Values[4]),
                ToUInt32(row.Values[5]),
                ToUInt32(row.Values[6]),
                ToUInt32(row.Values[7]),
                ToUInt32(row.Values[8]),
                ToUInt32(row.Values[9]),
                ToUInt32(row.Values[10]),
                ToByte(row.Values[11]),
                ToByte(row.Values[12]),
                ToByte(row.Values[13]),
                ToByte(row.Values[14]),
                ToByte(row.Values[15]),
                ToByte(row.Values[16]),
                ToByte(row.Values[17]),
                ToByte(row.Values[18]),
                ToByte(row.Values[19]),
                ToByte(row.Values[20]),
                ToUInt32(row.Values[21]),
                ToUInt32(row.Values[22]),
                ToUInt32(row.Values[23]),
                ToUInt32(row.Values[24]),
                ToUInt32(row.Values[25]),
                ToUInt32(row.Values[26]),
                ToUInt32(row.Values[27]),
                ToUInt32(row.Values[28]),
                ToUInt32(row.Values[29]),
                ToUInt32(row.Values[30]),
                ToUInt32(row.Values[31]),
                ToUInt32(row.Values[32]),
                ToUInt32(row.Values[33]),
                ToUInt32(row.Values[34]),
                ToUInt32(row.Values[35]));
        }

        return rows;
    }

    private static async Task<Dictionary<uint, LegacyParameterSaveRow>> ReadParameterSavesAsync(
        string path,
        CancellationToken cancellationToken)
    {
        Dictionary<uint, LegacyParameterSaveRow> rows = [];
        await foreach (SqlDumpInsertRow row in ReadRowsIfExistsAsync(path, "characters_parametersave", cancellationToken).ConfigureAwait(false))
        {
            if (row.Values.Count < 7)
                continue;

            uint characterId = ToUInt32(row.Values[0]);
            rows[characterId] = new LegacyParameterSaveRow(
                characterId,
                ToByte(row.Values[5]),
                ToUInt16(row.Values[6]));
        }

        return rows;
    }

    private static async Task<Dictionary<uint, Dictionary<byte, ushort>>> ReadClassLevelsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        Dictionary<uint, Dictionary<byte, ushort>> rows = [];
        await foreach (SqlDumpInsertRow row in ReadRowsIfExistsAsync(path, "characters_class_levels", cancellationToken).ConfigureAwait(false))
        {
            if (row.Values.Count < ClassColumns.Length + 1)
                continue;

            uint characterId = ToUInt32(row.Values[0]);
            Dictionary<byte, ushort> values = [];
            for (int index = 0; index < ClassColumns.Length; index++)
            {
                ushort level = ToUInt16(row.Values[index + 1]);
                if (level > 0)
                    values[ClassColumns[index].ClassId] = level;
            }

            rows[characterId] = values;
        }

        return rows;
    }

    private static async Task<Dictionary<uint, Dictionary<byte, uint>>> ReadClassExperienceAsync(
        string path,
        CancellationToken cancellationToken)
    {
        Dictionary<uint, Dictionary<byte, uint>> rows = [];
        await foreach (SqlDumpInsertRow row in ReadRowsIfExistsAsync(path, "characters_class_exp", cancellationToken).ConfigureAwait(false))
        {
            if (row.Values.Count < ClassColumns.Length + 1)
                continue;

            uint characterId = ToUInt32(row.Values[0]);
            Dictionary<byte, uint> values = [];
            for (int index = 0; index < ClassColumns.Length; index++)
                values[ClassColumns[index].ClassId] = ToUInt32(row.Values[index + 1]);

            rows[characterId] = values;
        }

        return rows;
    }

    private static async Task<Dictionary<uint, LegacyServerItemRow>> ReadServerItemsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        Dictionary<uint, LegacyServerItemRow> rows = [];
        await foreach (SqlDumpInsertRow row in ReadRowsIfExistsAsync(path, "server_items", cancellationToken).ConfigureAwait(false))
        {
            if (row.Values.Count < 4)
                continue;

            uint serverItemId = ToUInt32(row.Values[0]);
            rows[serverItemId] = new LegacyServerItemRow(
                serverItemId,
                ToUInt32(row.Values[1]),
                ToUInt32(row.Values[2]),
                ToByte(row.Values[3]));
        }

        return rows;
    }

    private static async Task<Dictionary<uint, IReadOnlyList<CharacterInventoryItemRecord>>> ReadInventoryAsync(
        string path,
        IReadOnlyDictionary<uint, LegacyServerItemRow> serverItems,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        Dictionary<uint, List<CharacterInventoryItemRecord>> rows = [];
        await foreach (SqlDumpInsertRow row in ReadRowsIfExistsAsync(path, "characters_inventory", cancellationToken).ConfigureAwait(false))
        {
            if (row.Values.Count < 4)
                continue;

            uint characterId = ToUInt32(row.Values[0]);
            uint serverItemId = ToUInt32(row.Values[1]);
            if (!serverItems.TryGetValue(serverItemId, out LegacyServerItemRow? item))
            {
                warnings.Add($"Skipped inventory item {serverItemId} for character {characterId}: missing server_items row.");
                continue;
            }

            if (item.Quantity > UInt16.MaxValue)
            {
                warnings.Add($"Skipped inventory item {serverItemId} for character {characterId}: quantity {item.Quantity} exceeds 16-bit storage.");
                continue;
            }

            CharacterInventoryItemRecord record = new(
                new CharacterId(characterId),
                ToByte(row.Values[2]),
                ToUInt16(row.Values[3]),
                item.ItemId,
                checked((ushort)item.Quantity),
                item.ServerItemId,
                item.Quality);
            if (!rows.TryGetValue(characterId, out List<CharacterInventoryItemRecord>? characterRows))
            {
                characterRows = [];
                rows[characterId] = characterRows;
            }

            characterRows.Add(record);
        }

        return rows.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<CharacterInventoryItemRecord>)pair.Value
                .OrderBy(item => item.ContainerId)
                .ThenBy(item => item.SlotId)
                .ToArray());
    }

    private static async Task<Dictionary<uint, IReadOnlyList<LegacyEquipmentRow>>> ReadEquipmentAsync(
        string path,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        Dictionary<uint, List<LegacyEquipmentRow>> rows = [];
        await foreach (SqlDumpInsertRow row in ReadRowsIfExistsAsync(path, "characters_inventory_equipment", cancellationToken).ConfigureAwait(false))
        {
            if (row.Values.Count < 4)
                continue;

            uint characterId = ToUInt32(row.Values[0]);
            LegacyEquipmentRow record = new(
                characterId,
                ToByte(row.Values[1]),
                ToUInt16(row.Values[2]),
                ToUInt32(row.Values[3]));
            if (!rows.TryGetValue(characterId, out List<LegacyEquipmentRow>? characterRows))
            {
                characterRows = [];
                rows[characterId] = characterRows;
            }

            characterRows.Add(record);
        }

        return rows.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<LegacyEquipmentRow>)pair.Value.OrderBy(item => item.EquipSlot).ToArray());
    }

    private static IReadOnlyList<CharacterClassStateRecord> BuildClassStates(
        CharacterId characterId,
        LegacyParameterSaveRow? parameterSave,
        IReadOnlyDictionary<byte, ushort>? classLevels,
        IReadOnlyDictionary<byte, uint>? classExperience)
    {
        Dictionary<byte, CharacterClassStateRecord> rows = [];
        if (classLevels is not null)
        {
            foreach ((byte classId, ushort level) in classLevels)
            {
                rows[classId] = new CharacterClassStateRecord(
                    characterId,
                    classId,
                    level,
                    classExperience?.GetValueOrDefault(classId) ?? 0,
                    parameterSave is not null && classId == parameterSave.MainSkill);
            }
        }

        if (parameterSave is not null && parameterSave.MainSkill != 0)
        {
            rows[parameterSave.MainSkill] = new CharacterClassStateRecord(
                characterId,
                parameterSave.MainSkill,
                parameterSave.MainSkillLevel,
                classExperience?.GetValueOrDefault(parameterSave.MainSkill) ?? 0,
                true);
        }

        return rows.Values.OrderByDescending(row => row.IsCurrent).ThenBy(row => row.ClassId).ToArray();
    }

    private static IReadOnlyList<CharacterEquipmentSlotRecord> BuildEquipmentSlots(
        CharacterId characterId,
        byte currentClassId,
        IReadOnlyList<LegacyEquipmentRow> equipment,
        IReadOnlyList<CharacterInventoryItemRecord> inventory,
        IReadOnlyDictionary<uint, LegacyServerItemRow> serverItems,
        List<string> warnings)
    {
        Dictionary<ushort, CharacterEquipmentSlotRecord> slots = [];
        Dictionary<uint, CharacterInventoryItemRecord> inventoryByServerItem = inventory
            .Where(item => item.ServerItemId != 0)
            .GroupBy(item => item.ServerItemId)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (LegacyEquipmentRow row in equipment
            .Where(row => row.ClassId == 0 || row.ClassId == currentClassId)
            .OrderBy(row => row.ClassId == 0 ? 0 : 1)
            .ThenBy(row => row.EquipSlot))
        {
            if (!serverItems.TryGetValue(row.ServerItemId, out LegacyServerItemRow? item))
            {
                warnings.Add($"Skipped equipment item {row.ServerItemId} for character {row.CharacterId}: missing server_items row.");
                continue;
            }

            byte inventoryContainerId = CharacterEquipmentInventoryLink.MissingContainerId;
            ushort inventorySlotId = CharacterEquipmentInventoryLink.MissingSlotId;
            if (inventoryByServerItem.TryGetValue(row.ServerItemId, out CharacterInventoryItemRecord? inventoryItem))
            {
                inventoryContainerId = inventoryItem.ContainerId;
                inventorySlotId = inventoryItem.SlotId;
            }
            else
            {
                warnings.Add($"Equipment item {row.ServerItemId} for character {row.CharacterId} has no matching characters_inventory link.");
            }

            slots[row.EquipSlot] = new CharacterEquipmentSlotRecord(
                characterId,
                row.EquipSlot,
                item.ItemId,
                DyeId: 0,
                row.ServerItemId,
                inventoryContainerId,
                inventorySlotId);
        }

        return slots.Values.OrderBy(row => row.SlotId).ToArray();
    }

    private static CharacterAppearanceRecord ToAppearance(LegacyCharacterRow character, LegacyAppearanceRow appearance)
    {
        uint modelId = appearance.BaseId == UInt32.MaxValue
            ? CharacterModelIds.FromTribe(character.Tribe)
            : appearance.BaseId;

        return new CharacterAppearanceRecord(
            new CharacterId(character.CharacterId),
            modelId,
            character.Tribe,
            appearance.Size,
            appearance.HairStyle,
            appearance.HairHighlightColor,
            appearance.HairVariation,
            appearance.FaceType,
            appearance.Characteristics,
            appearance.CharacteristicsColor,
            appearance.FaceEyebrows,
            appearance.FaceIrisSize,
            appearance.FaceEyeShape,
            appearance.FaceNose,
            appearance.FaceFeatures,
            appearance.FaceMouth,
            appearance.Ears,
            appearance.HairColor,
            appearance.SkinColor,
            appearance.EyeColor,
            appearance.Voice,
            appearance.MainHand,
            appearance.OffHand,
            SpMainHand: 0,
            SpOffHand: 0,
            Throwing: 0,
            Pack: 0,
            Pouch: 0,
            appearance.Head,
            appearance.Body,
            appearance.Legs,
            appearance.Hands,
            appearance.Feet,
            appearance.Waist,
            appearance.Neck,
            appearance.LeftEar,
            appearance.RightEar,
            LeftWrist: 0,
            RightWrist: 0,
            appearance.LeftIndex,
            appearance.RightIndex,
            appearance.LeftFinger,
            appearance.RightFinger);
    }

    private static async IAsyncEnumerable<SqlDumpInsertRow> ReadRowsIfExistsAsync(
        string path,
        string tableName,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            yield break;

        await foreach (SqlDumpInsertRow row in SqlDumpInsertReader.ReadRowsAsync(path, tableName, cancellationToken).ConfigureAwait(false))
            yield return row;
    }

    private static uint ToUInt32(string? value)
    {
        return UInt32.Parse(value ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static ushort ToUInt16(string? value)
    {
        return UInt16.Parse(value ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static byte ToByte(string? value)
    {
        return Byte.Parse(value ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static float ToSingle(string? value)
    {
        return Single.Parse(value ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private sealed record LegacyCharacterRow(
        uint CharacterId,
        uint AccountId,
        ushort Slot,
        uint WorldId,
        string Name,
        ushort State,
        float PositionX,
        float PositionY,
        float PositionZ,
        float Rotation,
        uint CurrentZoneId,
        string? PrivateAreaName,
        uint PrivateAreaLevel,
        byte Guardian,
        byte BirthDay,
        byte BirthMonth,
        byte InitialTown,
        byte Tribe);

    private sealed record LegacyAppearanceRow(
        uint CharacterId,
        uint BaseId,
        uint Size,
        uint Voice,
        uint SkinColor,
        uint HairStyle,
        uint HairColor,
        uint HairHighlightColor,
        uint HairVariation,
        uint EyeColor,
        byte FaceType,
        byte FaceEyebrows,
        byte FaceEyeShape,
        byte FaceIrisSize,
        byte FaceNose,
        byte FaceMouth,
        byte FaceFeatures,
        byte Ears,
        byte Characteristics,
        byte CharacteristicsColor,
        uint MainHand,
        uint OffHand,
        uint Head,
        uint Body,
        uint Hands,
        uint Legs,
        uint Feet,
        uint Waist,
        uint Neck,
        uint LeftIndex,
        uint RightIndex,
        uint LeftFinger,
        uint RightFinger,
        uint LeftEar,
        uint RightEar);

    private sealed record LegacyParameterSaveRow(uint CharacterId, byte MainSkill, ushort MainSkillLevel);

    private sealed record LegacyServerItemRow(uint ServerItemId, uint ItemId, uint Quantity, byte Quality);

    private sealed record LegacyEquipmentRow(uint CharacterId, byte ClassId, ushort EquipSlot, uint ServerItemId);

}
