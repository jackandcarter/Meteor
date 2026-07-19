using System.Text.Json;
using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed record CharacterSeedDatabaseLoadRequest(
    string V1SqlRootPath,
    MariaDbOptions DatabaseOptions,
    AccountId? TargetAccountId = null,
    WorldId? TargetWorldId = null,
    string? TargetAccountLoginName = null,
    WorldRecord? TargetWorld = null);

public sealed record CharacterSeedDatabaseLoadResult(
    int WorldCount,
    int ZoneCount,
    int AccountCount,
    int CharacterCount,
    int AppearanceCount,
    int MapStateCount,
    int ClassStateCount,
    int InventoryItemCount,
    int EquipmentSlotCount,
    IReadOnlyList<string> Warnings);

public sealed class CharacterSeedDatabaseLoader
{
    public async Task<CharacterSeedDatabaseLoadResult> LoadAsync(
        CharacterSeedDatabaseLoadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string root = Path.GetFullPath(request.V1SqlRootPath);
        V1SqlDumpCharacterSeedImporter characterImporter = new();
        V1SqlDumpCharacterSeedDataSet characterData = await characterImporter.ImportAsync(
            root,
            request.TargetAccountId,
            request.TargetWorld?.Id ?? request.TargetWorldId,
            cancellationToken).ConfigureAwait(false);

        List<string> warnings = [.. characterData.Warnings];
        IReadOnlyList<ZoneRecord> zones = await ImportZonesAsync(root, warnings, cancellationToken).ConfigureAwait(false);
        HashSet<uint> importedZoneIds = zones.Select(zone => zone.Id.Value).ToHashSet();
        List<V1SqlDumpCharacterSeedRecord> characters = characterData.Characters
            .Where(row => HasZone(row, importedZoneIds, warnings))
            .OrderBy(row => row.Character.WorldId.Value)
            .ThenBy(row => row.Character.AccountId.Value)
            .ThenBy(row => row.Character.Slot)
            .ThenBy(row => row.Character.Id.Value)
            .ToList();

        WorldRecord[] worlds = ResolveWorlds(request, characters)
            .OrderBy(world => world.Id.Value)
            .ToArray();
        AccountRecord[] accounts = ResolveAccounts(request, characters)
            .OrderBy(account => account.Id.Value)
            .ToArray();
        WorldId? zoneWorldId = worlds.Length == 0 ? null : worlds[0].Id;
        if (worlds.Length > 1)
            warnings.Add("Multiple imported worlds were detected; zone rows are global in the 2.0 schema and were attached to the first imported world.");

        await using MySqlConnection connection = new(request.DatabaseOptions.ToConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (WorldRecord world in worlds)
                await UpsertWorldAsync(connection, transaction, world, cancellationToken).ConfigureAwait(false);

            foreach (ZoneRecord zone in zones.OrderBy(zone => zone.Id.Value))
            {
                if (zoneWorldId is not null)
                    await UpsertZoneAsync(connection, transaction, zone, zoneWorldId.Value, cancellationToken).ConfigureAwait(false);
            }

            foreach (AccountRecord account in accounts)
                await UpsertAccountAsync(connection, transaction, account, cancellationToken).ConfigureAwait(false);

            int appearanceCount = 0;
            int mapStateCount = 0;
            int classStateCount = 0;
            int inventoryItemCount = 0;
            int equipmentSlotCount = 0;
            foreach (V1SqlDumpCharacterSeedRecord seed in characters)
            {
                await UpsertCharacterAsync(connection, transaction, seed.Character, cancellationToken).ConfigureAwait(false);
                await DeleteCharacterSeedRowsAsync(connection, transaction, seed.Character.Id, cancellationToken).ConfigureAwait(false);
                await UpsertAppearanceAsync(connection, transaction, seed.Appearance, seed.LobbyAppearancePayload, cancellationToken).ConfigureAwait(false);
                appearanceCount++;

                await UpsertMapStateAsync(connection, transaction, seed.MapState, cancellationToken).ConfigureAwait(false);
                mapStateCount++;

                foreach (CharacterClassStateRecord classState in seed.ClassStates)
                {
                    await UpsertClassStateAsync(connection, transaction, classState, cancellationToken).ConfigureAwait(false);
                    classStateCount++;
                }

                foreach (CharacterInventoryItemRecord item in seed.InventoryItems)
                {
                    await UpsertInventoryItemAsync(connection, transaction, item, cancellationToken).ConfigureAwait(false);
                    inventoryItemCount++;
                }

                foreach (CharacterEquipmentSlotRecord slot in seed.EquipmentSlots)
                {
                    await UpsertEquipmentSlotAsync(connection, transaction, slot, cancellationToken).ConfigureAwait(false);
                    equipmentSlotCount++;
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new CharacterSeedDatabaseLoadResult(
                worlds.Length,
                zones.Count,
                accounts.Length,
                characters.Count,
                appearanceCount,
                mapStateCount,
                classStateCount,
                inventoryItemCount,
                equipmentSlotCount,
                warnings);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<IReadOnlyList<ZoneRecord>> ImportZonesAsync(
        string root,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        string zonesPath = Path.Combine(root, "server_zones.sql");
        if (!File.Exists(zonesPath))
        {
            warnings.Add("No server_zones.sql found; character seed import cannot load playable characters without zone rows.");
            return [];
        }

        V1SqlDumpZoneDataImporter zoneImporter = new();
        return await zoneImporter.ImportAsync(zonesPath, cancellationToken).ConfigureAwait(false);
    }

    private static bool HasZone(
        V1SqlDumpCharacterSeedRecord seed,
        IReadOnlySet<uint> importedZoneIds,
        List<string> warnings)
    {
        if (importedZoneIds.Contains(seed.Character.CurrentZoneId.Value))
            return true;

        warnings.Add($"Skipped character {seed.Character.Id.Value}: zone {seed.Character.CurrentZoneId.Value} was not present in server_zones.sql.");
        return false;
    }

    private static IEnumerable<WorldRecord> ResolveWorlds(
        CharacterSeedDatabaseLoadRequest request,
        IReadOnlyList<V1SqlDumpCharacterSeedRecord> characters)
    {
        if (request.TargetWorld is not null)
        {
            yield return request.TargetWorld;
            yield break;
        }

        foreach (WorldId worldId in characters.Select(row => row.Character.WorldId).DistinctBy(id => id.Value))
            yield return new WorldRecord(worldId, ResolveWorldName(worldId), new ServerEndpoint("127.0.0.1", 54992));
    }

    private static string ResolveWorldName(WorldId worldId)
    {
        return worldId.Value == 1
            ? "AetherXIV 2.0 Local"
            : $"Imported World {worldId.Value}";
    }

    private static IEnumerable<AccountRecord> ResolveAccounts(
        CharacterSeedDatabaseLoadRequest request,
        IReadOnlyList<V1SqlDumpCharacterSeedRecord> characters)
    {
        foreach (AccountId accountId in characters.Select(row => row.Character.AccountId).DistinctBy(id => id.Value))
        {
            string loginName = request.TargetAccountId == accountId && !String.IsNullOrWhiteSpace(request.TargetAccountLoginName)
                ? request.TargetAccountLoginName!
                : $"seed-account-{accountId.Value}";
            yield return new AccountRecord(accountId, loginName, DateTimeOffset.UtcNow);
        }
    }

    private static async Task UpsertWorldAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        WorldRecord world,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO worlds (world_id, name, host, port)
VALUES (@world_id, @name, @host, @port)
ON DUPLICATE KEY UPDATE name = VALUES(name), host = VALUES(host), port = VALUES(port);
""";
        command.Parameters.AddWithValue("@world_id", world.Id.Value);
        command.Parameters.AddWithValue("@name", world.Name);
        command.Parameters.AddWithValue("@host", world.Endpoint.Host);
        command.Parameters.AddWithValue("@port", world.Endpoint.Port);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertZoneAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ZoneRecord zone,
        WorldId worldId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO zones (
    zone_id, world_id, name, region_id, is_private, load_nav_mesh,
    class_path, day_music, night_music, battle_music, is_inn,
    can_ride_chocobo, can_stealth, is_instance_raid)
VALUES (
    @zone_id, @world_id, @name, @region_id, @is_private, @load_nav_mesh,
    @class_path, @day_music, @night_music, @battle_music, @is_inn,
    @can_ride_chocobo, @can_stealth, @is_instance_raid)
ON DUPLICATE KEY UPDATE world_id = VALUES(world_id), name = VALUES(name), region_id = VALUES(region_id),
    is_private = VALUES(is_private), load_nav_mesh = VALUES(load_nav_mesh),
    class_path = VALUES(class_path), day_music = VALUES(day_music), night_music = VALUES(night_music),
    battle_music = VALUES(battle_music), is_inn = VALUES(is_inn),
    can_ride_chocobo = VALUES(can_ride_chocobo), can_stealth = VALUES(can_stealth),
    is_instance_raid = VALUES(is_instance_raid);
""";
        command.Parameters.AddWithValue("@zone_id", zone.Id.Value);
        command.Parameters.AddWithValue("@world_id", worldId.Value);
        command.Parameters.AddWithValue("@name", zone.Name);
        command.Parameters.AddWithValue("@region_id", zone.RegionId);
        command.Parameters.AddWithValue("@is_private", zone.IsPrivate);
        command.Parameters.AddWithValue("@load_nav_mesh", zone.LoadNavMesh);
        command.Parameters.AddWithValue("@class_path", zone.ClassPath);
        command.Parameters.AddWithValue("@day_music", zone.DayMusic);
        command.Parameters.AddWithValue("@night_music", zone.NightMusic);
        command.Parameters.AddWithValue("@battle_music", zone.BattleMusic);
        command.Parameters.AddWithValue("@is_inn", zone.IsInn);
        command.Parameters.AddWithValue("@can_ride_chocobo", zone.CanRideChocobo);
        command.Parameters.AddWithValue("@can_stealth", zone.CanStealth);
        command.Parameters.AddWithValue("@is_instance_raid", zone.IsInstanceRaid);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertAccountAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        AccountRecord account,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO accounts (account_id, login_name)
VALUES (@account_id, @login_name)
ON DUPLICATE KEY UPDATE login_name = login_name;
""";
        command.Parameters.AddWithValue("@account_id", account.Id.Value);
        command.Parameters.AddWithValue("@login_name", account.LoginName);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteCharacterSeedRowsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterId characterId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
DELETE FROM character_class_state WHERE character_id = @character_id;
DELETE FROM character_inventory_items WHERE character_id = @character_id;
DELETE FROM character_equipment_slots WHERE character_id = @character_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertCharacterAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterRecord character,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO characters (character_id, account_id, world_id, slot, name, current_zone_id,
    position_x, position_y, position_z, rotation)
VALUES (@character_id, @account_id, @world_id, @slot, @name, @current_zone_id,
    @position_x, @position_y, @position_z, @rotation)
ON DUPLICATE KEY UPDATE account_id = VALUES(account_id), world_id = VALUES(world_id),
    slot = VALUES(slot), name = VALUES(name), current_zone_id = VALUES(current_zone_id),
    position_x = VALUES(position_x), position_y = VALUES(position_y),
    position_z = VALUES(position_z), rotation = VALUES(rotation);
""";
        command.Parameters.AddWithValue("@character_id", character.Id.Value);
        command.Parameters.AddWithValue("@account_id", character.AccountId.Value);
        command.Parameters.AddWithValue("@world_id", character.WorldId.Value);
        command.Parameters.AddWithValue("@slot", character.Slot);
        command.Parameters.AddWithValue("@name", character.Name);
        command.Parameters.AddWithValue("@current_zone_id", character.CurrentZoneId.Value);
        command.Parameters.AddWithValue("@position_x", character.PositionX);
        command.Parameters.AddWithValue("@position_y", character.PositionY);
        command.Parameters.AddWithValue("@position_z", character.PositionZ);
        command.Parameters.AddWithValue("@rotation", character.Rotation);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertAppearanceAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterAppearanceRecord appearance,
        ReadOnlyMemory<byte> lobbyAppearancePayload,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_appearance (
    character_id, payload_json, model_id, tribe, size, hair_style, hair_highlight_color,
    hair_variation, face_type, characteristics, characteristics_color, face_eyebrows,
    face_iris_size, face_eye_shape, face_nose, face_features, face_mouth, ears, hair_color,
    skin_color, eye_color, voice, main_hand, off_hand, sp_main_hand, sp_off_hand, throwing,
    pack, pouch, head, body, legs, hands, feet, waist, neck, left_ear, right_ear,
    left_wrist, right_wrist, left_index, right_index, left_finger, right_finger)
VALUES (
    @character_id, @payload_json, @model_id, @tribe, @size, @hair_style, @hair_highlight_color,
    @hair_variation, @face_type, @characteristics, @characteristics_color, @face_eyebrows,
    @face_iris_size, @face_eye_shape, @face_nose, @face_features, @face_mouth, @ears, @hair_color,
    @skin_color, @eye_color, @voice, @main_hand, @off_hand, @sp_main_hand, @sp_off_hand, @throwing,
    @pack, @pouch, @head, @body, @legs, @hands, @feet, @waist, @neck, @left_ear, @right_ear,
    @left_wrist, @right_wrist, @left_index, @right_index, @left_finger, @right_finger)
ON DUPLICATE KEY UPDATE payload_json = VALUES(payload_json), model_id = VALUES(model_id),
    tribe = VALUES(tribe), size = VALUES(size), hair_style = VALUES(hair_style),
    hair_highlight_color = VALUES(hair_highlight_color), hair_variation = VALUES(hair_variation),
    face_type = VALUES(face_type), characteristics = VALUES(characteristics),
    characteristics_color = VALUES(characteristics_color), face_eyebrows = VALUES(face_eyebrows),
    face_iris_size = VALUES(face_iris_size), face_eye_shape = VALUES(face_eye_shape),
    face_nose = VALUES(face_nose), face_features = VALUES(face_features),
    face_mouth = VALUES(face_mouth), ears = VALUES(ears), hair_color = VALUES(hair_color),
    skin_color = VALUES(skin_color), eye_color = VALUES(eye_color), voice = VALUES(voice),
    main_hand = VALUES(main_hand), off_hand = VALUES(off_hand), sp_main_hand = VALUES(sp_main_hand),
    sp_off_hand = VALUES(sp_off_hand), throwing = VALUES(throwing), pack = VALUES(pack),
    pouch = VALUES(pouch), head = VALUES(head), body = VALUES(body), legs = VALUES(legs),
    hands = VALUES(hands), feet = VALUES(feet), waist = VALUES(waist), neck = VALUES(neck),
    left_ear = VALUES(left_ear), right_ear = VALUES(right_ear), left_wrist = VALUES(left_wrist),
    right_wrist = VALUES(right_wrist), left_index = VALUES(left_index),
    right_index = VALUES(right_index), left_finger = VALUES(left_finger),
    right_finger = VALUES(right_finger);
""";
        AddAppearanceParameters(command, appearance);
        command.Parameters.AddWithValue("@payload_json", CreateLobbyPayloadJson(lobbyAppearancePayload));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertMapStateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterMapStateRecord state,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO map_character_state (character_id, zone_id, private_area_name, private_area_level,
    position_x, position_y, position_z, rotation)
VALUES (@character_id, @zone_id, @private_area_name, @private_area_level,
    @position_x, @position_y, @position_z, @rotation)
ON DUPLICATE KEY UPDATE zone_id = VALUES(zone_id), private_area_name = VALUES(private_area_name),
    private_area_level = VALUES(private_area_level), position_x = VALUES(position_x),
    position_y = VALUES(position_y), position_z = VALUES(position_z), rotation = VALUES(rotation);
""";
        command.Parameters.AddWithValue("@character_id", state.CharacterId.Value);
        command.Parameters.AddWithValue("@zone_id", state.ZoneId.Value);
        command.Parameters.AddWithValue("@private_area_name", (object?)state.PrivateAreaName ?? DBNull.Value);
        command.Parameters.AddWithValue("@private_area_level", state.PrivateAreaLevel);
        command.Parameters.AddWithValue("@position_x", state.PositionX);
        command.Parameters.AddWithValue("@position_y", state.PositionY);
        command.Parameters.AddWithValue("@position_z", state.PositionZ);
        command.Parameters.AddWithValue("@rotation", state.Rotation);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertClassStateAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterClassStateRecord state,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_class_state (character_id, class_id, level, experience, is_current)
VALUES (@character_id, @class_id, @level, @experience, @is_current)
ON DUPLICATE KEY UPDATE level = VALUES(level), experience = VALUES(experience),
    is_current = VALUES(is_current);
""";
        command.Parameters.AddWithValue("@character_id", state.CharacterId.Value);
        command.Parameters.AddWithValue("@class_id", state.ClassId);
        command.Parameters.AddWithValue("@level", state.Level);
        command.Parameters.AddWithValue("@experience", state.Experience);
        command.Parameters.AddWithValue("@is_current", state.IsCurrent);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertInventoryItemAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterInventoryItemRecord item,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_inventory_items (character_id, container_id, slot_id, item_id, quantity, server_item_id, quality)
VALUES (@character_id, @container_id, @slot_id, @item_id, @quantity, @server_item_id, @quality)
ON DUPLICATE KEY UPDATE item_id = VALUES(item_id), quantity = VALUES(quantity),
    server_item_id = VALUES(server_item_id), quality = VALUES(quality);
""";
        command.Parameters.AddWithValue("@character_id", item.CharacterId.Value);
        command.Parameters.AddWithValue("@container_id", item.ContainerId);
        command.Parameters.AddWithValue("@slot_id", item.SlotId);
        command.Parameters.AddWithValue("@item_id", item.ItemId);
        command.Parameters.AddWithValue("@quantity", item.Quantity);
        command.Parameters.AddWithValue("@server_item_id", item.ServerItemId);
        command.Parameters.AddWithValue("@quality", item.Quality);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertEquipmentSlotAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        CharacterEquipmentSlotRecord slot,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO character_equipment_slots (character_id, slot_id, item_id, dye_id, server_item_id, inventory_container_id, inventory_slot_id)
VALUES (@character_id, @slot_id, @item_id, @dye_id, @server_item_id, @inventory_container_id, @inventory_slot_id)
ON DUPLICATE KEY UPDATE item_id = VALUES(item_id), dye_id = VALUES(dye_id),
    server_item_id = VALUES(server_item_id),
    inventory_container_id = VALUES(inventory_container_id),
    inventory_slot_id = VALUES(inventory_slot_id);
""";
        command.Parameters.AddWithValue("@character_id", slot.CharacterId.Value);
        command.Parameters.AddWithValue("@slot_id", slot.SlotId);
        command.Parameters.AddWithValue("@item_id", slot.ItemId);
        command.Parameters.AddWithValue("@dye_id", slot.DyeId);
        command.Parameters.AddWithValue("@server_item_id", slot.ServerItemId);
        command.Parameters.AddWithValue("@inventory_container_id", slot.InventoryContainerId);
        command.Parameters.AddWithValue("@inventory_slot_id", slot.InventorySlotId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddAppearanceParameters(MySqlCommand command, CharacterAppearanceRecord appearance)
    {
        command.Parameters.AddWithValue("@character_id", appearance.CharacterId.Value);
        command.Parameters.AddWithValue("@model_id", appearance.ModelId);
        command.Parameters.AddWithValue("@tribe", appearance.Tribe);
        command.Parameters.AddWithValue("@size", appearance.Size);
        command.Parameters.AddWithValue("@hair_style", appearance.HairStyle);
        command.Parameters.AddWithValue("@hair_highlight_color", appearance.HairHighlightColor);
        command.Parameters.AddWithValue("@hair_variation", appearance.HairVariation);
        command.Parameters.AddWithValue("@face_type", appearance.FaceType);
        command.Parameters.AddWithValue("@characteristics", appearance.Characteristics);
        command.Parameters.AddWithValue("@characteristics_color", appearance.CharacteristicsColor);
        command.Parameters.AddWithValue("@face_eyebrows", appearance.FaceEyebrows);
        command.Parameters.AddWithValue("@face_iris_size", appearance.FaceIrisSize);
        command.Parameters.AddWithValue("@face_eye_shape", appearance.FaceEyeShape);
        command.Parameters.AddWithValue("@face_nose", appearance.FaceNose);
        command.Parameters.AddWithValue("@face_features", appearance.FaceFeatures);
        command.Parameters.AddWithValue("@face_mouth", appearance.FaceMouth);
        command.Parameters.AddWithValue("@ears", appearance.Ears);
        command.Parameters.AddWithValue("@hair_color", appearance.HairColor);
        command.Parameters.AddWithValue("@skin_color", appearance.SkinColor);
        command.Parameters.AddWithValue("@eye_color", appearance.EyeColor);
        command.Parameters.AddWithValue("@voice", appearance.Voice);
        command.Parameters.AddWithValue("@main_hand", appearance.MainHand);
        command.Parameters.AddWithValue("@off_hand", appearance.OffHand);
        command.Parameters.AddWithValue("@sp_main_hand", appearance.SpMainHand);
        command.Parameters.AddWithValue("@sp_off_hand", appearance.SpOffHand);
        command.Parameters.AddWithValue("@throwing", appearance.Throwing);
        command.Parameters.AddWithValue("@pack", appearance.Pack);
        command.Parameters.AddWithValue("@pouch", appearance.Pouch);
        command.Parameters.AddWithValue("@head", appearance.Head);
        command.Parameters.AddWithValue("@body", appearance.Body);
        command.Parameters.AddWithValue("@legs", appearance.Legs);
        command.Parameters.AddWithValue("@hands", appearance.Hands);
        command.Parameters.AddWithValue("@feet", appearance.Feet);
        command.Parameters.AddWithValue("@waist", appearance.Waist);
        command.Parameters.AddWithValue("@neck", appearance.Neck);
        command.Parameters.AddWithValue("@left_ear", appearance.LeftEar);
        command.Parameters.AddWithValue("@right_ear", appearance.RightEar);
        command.Parameters.AddWithValue("@left_wrist", appearance.LeftWrist);
        command.Parameters.AddWithValue("@right_wrist", appearance.RightWrist);
        command.Parameters.AddWithValue("@left_index", appearance.LeftIndex);
        command.Parameters.AddWithValue("@right_index", appearance.RightIndex);
        command.Parameters.AddWithValue("@left_finger", appearance.LeftFinger);
        command.Parameters.AddWithValue("@right_finger", appearance.RightFinger);
    }

    private static string CreateLobbyPayloadJson(ReadOnlyMemory<byte> payload)
    {
        return JsonSerializer.Serialize(new
        {
            encoding = "base64",
            payload = Convert.ToBase64String(payload.Span)
        });
    }
}
