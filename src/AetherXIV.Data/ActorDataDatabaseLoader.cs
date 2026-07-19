using System.Text.Json;
using System.Text.Json.Serialization;
using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed record ActorDataDatabaseLoadRequest(
    string ReviewRootPath,
    string V1SqlRootPath,
    MariaDbOptions DatabaseOptions,
    WorldRecord? World = null);

public sealed record ActorDataDatabaseLoadResult(
    int ZoneCount,
    int ActorClassInsertedCount,
    int ActorClassSkippedCount,
    int ActorAppearanceInsertedCount,
    int ActorAppearanceSkippedCount,
    int StaticActorSpawnInsertedCount,
    int StaticActorSpawnSkippedCount,
    IReadOnlyList<string> Warnings);

public sealed class ActorDataDatabaseLoader
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<ActorDataDatabaseLoadResult> LoadAsync(
        ActorDataDatabaseLoadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string reviewRoot = Path.GetFullPath(request.ReviewRootPath);
        string v1SqlRoot = Path.GetFullPath(request.V1SqlRootPath);
        IReadOnlyList<ActorClassRecord> actorClasses = await ReadJsonAsync<IReadOnlyList<ActorClassRecord>>(
            Path.Combine(reviewRoot, "actor-classes.json"),
            cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ActorAppearanceRecord> actorAppearances = await ReadJsonAsync<IReadOnlyList<ActorAppearanceRecord>>(
            Path.Combine(reviewRoot, "actor-appearances.json"),
            cancellationToken).ConfigureAwait(false);
        IReadOnlyList<StaticActorSpawnRecord> staticSpawns = await ReadJsonAsync<IReadOnlyList<StaticActorSpawnRecord>>(
            Path.Combine(reviewRoot, "static-actor-spawns.json"),
            cancellationToken).ConfigureAwait(false);

        V1SqlDumpZoneDataImporter zoneImporter = new();
        IReadOnlyList<ZoneRecord> zones = await zoneImporter.ImportAsync(
            Path.Combine(v1SqlRoot, "server_zones.sql"),
            cancellationToken).ConfigureAwait(false);
        WorldRecord world = request.World ?? new WorldRecord(new WorldId(1), "AetherXIV 2.0 Local", new ServerEndpoint("127.0.0.1", 54992));
        List<string> warnings = [];

        await using MySqlConnection connection = new(request.DatabaseOptions.ToConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            await UpsertWorldAsync(connection, transaction, world, cancellationToken).ConfigureAwait(false);
            foreach (ZoneRecord zone in zones)
                await UpsertZoneAsync(connection, transaction, zone, world.Id, cancellationToken).ConfigureAwait(false);

            HashSet<uint> insertedActorClassIds = [];
            int actorClassInserted = 0;
            int actorClassSkipped = 0;
            foreach (ActorClassRecord actorClass in actorClasses.OrderBy(row => row.ActorClassId))
            {
                if (!IsValidJson(actorClass.EventConditions))
                {
                    actorClassSkipped++;
                    warnings.Add($"Skipped actor class {actorClass.ActorClassId}: invalid event condition JSON from {actorClass.Provenance.SourceRef}.");
                    continue;
                }

                ulong provenanceId = await GetOrInsertProvenanceAsync(connection, transaction, actorClass.Provenance, cancellationToken).ConfigureAwait(false);
                await UpsertActorClassAsync(connection, transaction, actorClass, provenanceId, cancellationToken).ConfigureAwait(false);
                insertedActorClassIds.Add(actorClass.ActorClassId);
                actorClassInserted++;
            }

            int appearanceInserted = 0;
            int appearanceSkipped = 0;
            foreach (ActorAppearanceRecord appearance in actorAppearances.OrderBy(row => row.ActorClassId))
            {
                if (!insertedActorClassIds.Contains(appearance.ActorClassId))
                {
                    appearanceSkipped++;
                    warnings.Add($"Skipped appearance {appearance.ActorClassId}: actor class was not loaded.");
                    continue;
                }

                ulong provenanceId = await GetOrInsertProvenanceAsync(connection, transaction, appearance.Provenance, cancellationToken).ConfigureAwait(false);
                await UpsertActorAppearanceAsync(connection, transaction, appearance, provenanceId, cancellationToken).ConfigureAwait(false);
                appearanceInserted++;
            }

            HashSet<uint> zoneIds = zones.Select(row => row.Id.Value).ToHashSet();
            int spawnInserted = 0;
            int spawnSkipped = 0;
            foreach (StaticActorSpawnRecord spawn in staticSpawns.OrderBy(row => row.SpawnId))
            {
                if (!insertedActorClassIds.Contains(spawn.ActorClassId))
                {
                    spawnSkipped++;
                    warnings.Add($"Skipped static spawn {spawn.SpawnId}: actor class {spawn.ActorClassId} was not loaded.");
                    continue;
                }

                if (!zoneIds.Contains(spawn.ZoneId.Value))
                {
                    spawnSkipped++;
                    warnings.Add($"Skipped static spawn {spawn.SpawnId}: zone {spawn.ZoneId.Value} was not loaded from server_zones.sql.");
                    continue;
                }

                ulong provenanceId = await GetOrInsertProvenanceAsync(connection, transaction, spawn.Provenance, cancellationToken).ConfigureAwait(false);
                await UpsertStaticActorSpawnAsync(connection, transaction, spawn, provenanceId, cancellationToken).ConfigureAwait(false);
                spawnInserted++;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ActorDataDatabaseLoadResult(
                zones.Count,
                actorClassInserted,
                actorClassSkipped,
                appearanceInserted,
                appearanceSkipped,
                spawnInserted,
                spawnSkipped,
                warnings);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    internal static async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException($"Could not deserialize required actor data artifact: {path}");
    }

    internal static async Task UpsertWorldAsync(
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

    internal static async Task UpsertZoneAsync(
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

    internal static async Task<ulong> GetOrInsertProvenanceAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ProvenanceRef provenance,
        CancellationToken cancellationToken)
    {
        await using (MySqlCommand selectCommand = connection.CreateCommand())
        {
            selectCommand.Transaction = transaction;
            selectCommand.CommandText = """
SELECT provenance_id
FROM provenance_refs
WHERE evidence_status = @evidence_status
  AND source_type = @source_type
  AND source_ref = @source_ref
  AND notes = @notes
ORDER BY provenance_id
LIMIT 1;
""";
            AddProvenanceParameters(selectCommand, provenance);
            object? existing = await selectCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (existing is not null)
                return Convert.ToUInt64(existing, System.Globalization.CultureInfo.InvariantCulture);
        }

        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO provenance_refs (evidence_status, source_type, source_ref, notes)
VALUES (@evidence_status, @source_type, @source_ref, @notes);
SELECT LAST_INSERT_ID();
""";
        AddProvenanceParameters(command, provenance);
        object? result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToUInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddProvenanceParameters(MySqlCommand command, ProvenanceRef provenance)
    {
        command.Parameters.AddWithValue("@evidence_status", provenance.Status.ToString());
        command.Parameters.AddWithValue("@source_type", provenance.SourceType);
        command.Parameters.AddWithValue("@source_ref", provenance.SourceRef);
        command.Parameters.AddWithValue("@notes", provenance.Notes);
    }

    internal static async Task UpsertActorClassAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ActorClassRecord actorClass,
        ulong provenanceId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO actor_classes (actor_class_id, class_path, display_name_id, property_flags, event_conditions,
    push_command, push_command_sub, push_command_priority, provenance_id)
VALUES (@actor_class_id, @class_path, @display_name_id, @property_flags, @event_conditions,
    @push_command, @push_command_sub, @push_command_priority, @provenance_id)
ON DUPLICATE KEY UPDATE class_path = VALUES(class_path), display_name_id = VALUES(display_name_id),
    property_flags = VALUES(property_flags), event_conditions = VALUES(event_conditions),
    push_command = VALUES(push_command), push_command_sub = VALUES(push_command_sub),
    push_command_priority = VALUES(push_command_priority), provenance_id = VALUES(provenance_id);
""";
        command.Parameters.AddWithValue("@actor_class_id", actorClass.ActorClassId);
        command.Parameters.AddWithValue("@class_path", actorClass.ClassPath);
        command.Parameters.AddWithValue("@display_name_id", actorClass.DisplayNameId);
        command.Parameters.AddWithValue("@property_flags", actorClass.PropertyFlags);
        command.Parameters.AddWithValue("@event_conditions", actorClass.EventConditions);
        command.Parameters.AddWithValue("@push_command", actorClass.PushCommand);
        command.Parameters.AddWithValue("@push_command_sub", actorClass.PushCommandSub);
        command.Parameters.AddWithValue("@push_command_priority", actorClass.PushCommandPriority);
        command.Parameters.AddWithValue("@provenance_id", provenanceId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async Task UpsertActorAppearanceAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ActorAppearanceRecord appearance,
        ulong provenanceId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO actor_appearances (
    actor_class_id, base, size, hair_style, hair_highlight_color, hair_variation,
    face_type, characteristics, characteristics_color, face_eyebrows, face_iris_size,
    face_eye_shape, face_nose, face_features, face_mouth, ears, hair_color, skin_color,
    eye_color, voice, main_hand, off_hand, sp_main_hand, sp_off_hand, throwing, pack,
    pouch, head, body, legs, hands, feet, waist, neck, left_ear, right_ear,
    left_index, right_index, left_finger, right_finger, provenance_id)
VALUES (
    @actor_class_id, @base, @size, @hair_style, @hair_highlight_color, @hair_variation,
    @face_type, @characteristics, @characteristics_color, @face_eyebrows, @face_iris_size,
    @face_eye_shape, @face_nose, @face_features, @face_mouth, @ears, @hair_color, @skin_color,
    @eye_color, @voice, @main_hand, @off_hand, @sp_main_hand, @sp_off_hand, @throwing, @pack,
    @pouch, @head, @body, @legs, @hands, @feet, @waist, @neck, @left_ear, @right_ear,
    @left_index, @right_index, @left_finger, @right_finger, @provenance_id)
ON DUPLICATE KEY UPDATE base = VALUES(base), size = VALUES(size), hair_style = VALUES(hair_style),
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
    left_ear = VALUES(left_ear), right_ear = VALUES(right_ear), left_index = VALUES(left_index),
    right_index = VALUES(right_index), left_finger = VALUES(left_finger),
    right_finger = VALUES(right_finger), provenance_id = VALUES(provenance_id);
""";
        AddAppearanceParameters(command, appearance, provenanceId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async Task UpsertStaticActorSpawnAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        StaticActorSpawnRecord spawn,
        ulong provenanceId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO static_actor_spawns (spawn_id, actor_class_id, unique_id, zone_id, private_area_name,
    private_area_level, position_x, position_y, position_z, rotation, actor_state, animation_id,
    custom_display_name, map_object_layout_id, map_object_instance_id, provenance_id)
VALUES (@spawn_id, @actor_class_id, @unique_id, @zone_id, @private_area_name,
    @private_area_level, @position_x, @position_y, @position_z, @rotation, @actor_state, @animation_id,
    @custom_display_name, @map_object_layout_id, @map_object_instance_id, @provenance_id)
ON DUPLICATE KEY UPDATE actor_class_id = VALUES(actor_class_id), unique_id = VALUES(unique_id),
    zone_id = VALUES(zone_id), private_area_name = VALUES(private_area_name),
    private_area_level = VALUES(private_area_level), position_x = VALUES(position_x),
    position_y = VALUES(position_y), position_z = VALUES(position_z), rotation = VALUES(rotation),
    actor_state = VALUES(actor_state), animation_id = VALUES(animation_id),
    custom_display_name = VALUES(custom_display_name), map_object_layout_id = VALUES(map_object_layout_id),
    map_object_instance_id = VALUES(map_object_instance_id), provenance_id = VALUES(provenance_id);
""";
        command.Parameters.AddWithValue("@spawn_id", spawn.SpawnId);
        command.Parameters.AddWithValue("@actor_class_id", spawn.ActorClassId);
        command.Parameters.AddWithValue("@unique_id", spawn.UniqueId);
        command.Parameters.AddWithValue("@zone_id", spawn.ZoneId.Value);
        command.Parameters.AddWithValue("@private_area_name", String.IsNullOrEmpty(spawn.PrivateAreaName) ? DBNull.Value : spawn.PrivateAreaName);
        command.Parameters.AddWithValue("@private_area_level", spawn.PrivateAreaLevel);
        command.Parameters.AddWithValue("@position_x", spawn.PositionX);
        command.Parameters.AddWithValue("@position_y", spawn.PositionY);
        command.Parameters.AddWithValue("@position_z", spawn.PositionZ);
        command.Parameters.AddWithValue("@rotation", spawn.Rotation);
        command.Parameters.AddWithValue("@actor_state", spawn.ActorState);
        command.Parameters.AddWithValue("@animation_id", spawn.AnimationId);
        command.Parameters.AddWithValue("@custom_display_name", String.IsNullOrEmpty(spawn.CustomDisplayName) ? DBNull.Value : spawn.CustomDisplayName);
        command.Parameters.AddWithValue("@map_object_layout_id", spawn.MapObjectLayoutId is null ? DBNull.Value : spawn.MapObjectLayoutId.Value);
        command.Parameters.AddWithValue("@map_object_instance_id", spawn.MapObjectInstanceId is null ? DBNull.Value : spawn.MapObjectInstanceId.Value);
        command.Parameters.AddWithValue("@provenance_id", provenanceId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddAppearanceParameters(MySqlCommand command, ActorAppearanceRecord appearance, ulong provenanceId)
    {
        command.Parameters.AddWithValue("@actor_class_id", appearance.ActorClassId);
        command.Parameters.AddWithValue("@base", appearance.Base);
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
        command.Parameters.AddWithValue("@left_index", appearance.LeftIndex);
        command.Parameters.AddWithValue("@right_index", appearance.RightIndex);
        command.Parameters.AddWithValue("@left_finger", appearance.LeftFinger);
        command.Parameters.AddWithValue("@right_finger", appearance.RightFinger);
        command.Parameters.AddWithValue("@provenance_id", provenanceId);
    }

    internal static bool IsValidJson(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
