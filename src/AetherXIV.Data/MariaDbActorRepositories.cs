using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbActorClassRepository : IActorClassRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbActorClassRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<ActorClassRecord?> GetAsync(uint actorClassId, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT ac.actor_class_id, ac.class_path, ac.display_name_id, ac.property_flags, ac.event_conditions,
       ac.push_command, ac.push_command_sub, ac.push_command_priority,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM actor_classes ac
JOIN provenance_refs p ON p.provenance_id = ac.provenance_id
WHERE ac.actor_class_id = @actor_class_id;
""";
        command.Parameters.AddWithValue("@actor_class_id", actorClassId);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return ReadActorClass(reader);
    }

    public async Task<IReadOnlyList<ActorClassRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT ac.actor_class_id, ac.class_path, ac.display_name_id, ac.property_flags, ac.event_conditions,
       ac.push_command, ac.push_command_sub, ac.push_command_priority,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM actor_classes ac
JOIN provenance_refs p ON p.provenance_id = ac.provenance_id
ORDER BY ac.actor_class_id;
""";

        List<ActorClassRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(ReadActorClass(reader));

        return rows;
    }

    private static ActorClassRecord ReadActorClass(MySqlDataReader reader)
    {
        return new ActorClassRecord(
            reader.GetUInt32("actor_class_id"),
            reader.GetString("class_path"),
            reader.GetUInt32("display_name_id"),
            reader.GetUInt32("property_flags"),
            reader.GetString("event_conditions"),
            reader.GetUInt16("push_command"),
            reader.GetUInt16("push_command_sub"),
            reader.GetByte("push_command_priority"),
            ReadProvenance(reader));
    }

    private static ProvenanceRef ReadProvenance(MySqlDataReader reader)
    {
        return new ProvenanceRef(
            Enum.Parse<EvidenceStatus>(reader.GetString("evidence_status")),
            reader.GetString("source_type"),
            reader.GetString("source_ref"),
            reader.GetString("notes"));
    }
}

public sealed class MariaDbActorAppearanceRepository : IActorAppearanceRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbActorAppearanceRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<ActorAppearanceRecord?> GetAsync(uint actorClassId, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT aa.actor_class_id, aa.base, aa.size, aa.hair_style, aa.hair_highlight_color, aa.hair_variation,
       aa.face_type, aa.characteristics, aa.characteristics_color, aa.face_eyebrows, aa.face_iris_size,
       aa.face_eye_shape, aa.face_nose, aa.face_features, aa.face_mouth, aa.ears, aa.hair_color,
       aa.skin_color, aa.eye_color, aa.voice, aa.main_hand, aa.off_hand, aa.sp_main_hand, aa.sp_off_hand,
       aa.throwing, aa.pack, aa.pouch, aa.head, aa.body, aa.legs, aa.hands, aa.feet, aa.waist,
       aa.neck, aa.left_ear, aa.right_ear, aa.left_index, aa.right_index, aa.left_finger, aa.right_finger,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM actor_appearances aa
JOIN provenance_refs p ON p.provenance_id = aa.provenance_id
WHERE aa.actor_class_id = @actor_class_id;
""";
        command.Parameters.AddWithValue("@actor_class_id", actorClassId);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return ReadActorAppearance(reader);
    }

    public async Task<IReadOnlyList<ActorAppearanceRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT aa.actor_class_id, aa.base, aa.size, aa.hair_style, aa.hair_highlight_color, aa.hair_variation,
       aa.face_type, aa.characteristics, aa.characteristics_color, aa.face_eyebrows, aa.face_iris_size,
       aa.face_eye_shape, aa.face_nose, aa.face_features, aa.face_mouth, aa.ears, aa.hair_color,
       aa.skin_color, aa.eye_color, aa.voice, aa.main_hand, aa.off_hand, aa.sp_main_hand, aa.sp_off_hand,
       aa.throwing, aa.pack, aa.pouch, aa.head, aa.body, aa.legs, aa.hands, aa.feet, aa.waist,
       aa.neck, aa.left_ear, aa.right_ear, aa.left_index, aa.right_index, aa.left_finger, aa.right_finger,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM actor_appearances aa
JOIN provenance_refs p ON p.provenance_id = aa.provenance_id
ORDER BY aa.actor_class_id;
""";

        List<ActorAppearanceRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(ReadActorAppearance(reader));

        return rows;
    }

    private static ActorAppearanceRecord ReadActorAppearance(MySqlDataReader reader)
    {
        return new ActorAppearanceRecord(
            reader.GetUInt32("actor_class_id"),
            reader.GetUInt32("base"),
            reader.GetUInt32("size"),
            reader.GetUInt32("hair_style"),
            reader.GetUInt32("hair_highlight_color"),
            reader.GetUInt32("hair_variation"),
            reader.GetByte("face_type"),
            reader.GetByte("characteristics"),
            reader.GetByte("characteristics_color"),
            reader.GetByte("face_eyebrows"),
            reader.GetByte("face_iris_size"),
            reader.GetByte("face_eye_shape"),
            reader.GetByte("face_nose"),
            reader.GetByte("face_features"),
            reader.GetByte("face_mouth"),
            reader.GetByte("ears"),
            reader.GetUInt32("hair_color"),
            reader.GetUInt32("skin_color"),
            reader.GetUInt32("eye_color"),
            reader.GetUInt32("voice"),
            reader.GetUInt32("main_hand"),
            reader.GetUInt32("off_hand"),
            reader.GetUInt32("sp_main_hand"),
            reader.GetUInt32("sp_off_hand"),
            reader.GetUInt32("throwing"),
            reader.GetUInt32("pack"),
            reader.GetUInt32("pouch"),
            reader.GetUInt32("head"),
            reader.GetUInt32("body"),
            reader.GetUInt32("legs"),
            reader.GetUInt32("hands"),
            reader.GetUInt32("feet"),
            reader.GetUInt32("waist"),
            reader.GetUInt32("neck"),
            reader.GetUInt32("left_ear"),
            reader.GetUInt32("right_ear"),
            reader.GetUInt32("left_index"),
            reader.GetUInt32("right_index"),
            reader.GetUInt32("left_finger"),
            reader.GetUInt32("right_finger"),
            ReadProvenance(reader));
    }

    private static ProvenanceRef ReadProvenance(MySqlDataReader reader)
    {
        return new ProvenanceRef(
            Enum.Parse<EvidenceStatus>(reader.GetString("evidence_status")),
            reader.GetString("source_type"),
            reader.GetString("source_ref"),
            reader.GetString("notes"));
    }
}

public sealed class MariaDbActorSpawnRepository : IActorSpawnRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbActorSpawnRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<StaticActorSpawnRecord>> ListStaticSpawnsAsync(
        ZoneId zoneId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT sas.spawn_id, sas.actor_class_id, sas.unique_id, sas.zone_id, sas.private_area_name,
       sas.private_area_level, sas.position_x, sas.position_y, sas.position_z, sas.rotation,
       sas.actor_state, sas.animation_id, sas.custom_display_name, sas.map_object_layout_id,
       sas.map_object_instance_id, p.evidence_status, p.source_type, p.source_ref, p.notes
FROM static_actor_spawns sas
JOIN provenance_refs p ON p.provenance_id = sas.provenance_id
WHERE sas.zone_id = @zone_id
ORDER BY sas.spawn_id;
""";
        command.Parameters.AddWithValue("@zone_id", zoneId.Value);

        List<StaticActorSpawnRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new StaticActorSpawnRecord(
                reader.GetUInt32("spawn_id"),
                reader.GetUInt32("actor_class_id"),
                reader.GetString("unique_id"),
                new ZoneId(reader.GetUInt32("zone_id")),
                reader.IsDBNull(reader.GetOrdinal("private_area_name")) ? null : reader.GetString("private_area_name"),
                reader.GetUInt32("private_area_level"),
                reader.GetFloat("position_x"),
                reader.GetFloat("position_y"),
                reader.GetFloat("position_z"),
                reader.GetFloat("rotation"),
                reader.GetUInt16("actor_state"),
                reader.GetUInt32("animation_id"),
                reader.IsDBNull(reader.GetOrdinal("custom_display_name")) ? null : reader.GetString("custom_display_name"),
                ReadProvenance(reader),
                reader.IsDBNull(reader.GetOrdinal("map_object_layout_id")) ? null : reader.GetUInt32("map_object_layout_id"),
                reader.IsDBNull(reader.GetOrdinal("map_object_instance_id")) ? null : reader.GetUInt32("map_object_instance_id")));
        }

        return rows;
    }

    public async Task<IReadOnlyList<BattleNpcSpawnRecord>> ListBattleNpcSpawnsAsync(
        ZoneId zoneId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT bns.battle_npc_id, bns.group_id, bns.custom_display_name,
       bns.position_x, bns.position_y, bns.position_z, bns.rotation,
       bng.pool_id, bng.zone_id, bng.script_name, bng.min_level, bng.max_level,
       bng.respawn_seconds, bng.hit_points, bng.magic_points, bng.drop_list_id,
       bng.allegiance, bng.spawn_type, bng.animation_id, bng.actor_state,
       bng.private_area_name, bng.private_area_level,
       bnp.actor_class_id, bnp.genus_id, bnp.current_job, bnp.combat_skill, bnp.combat_delay,
       bnp.combat_damage_multiplier, bnp.aggro_type, bnp.immunity,
       bnp.link_type, bnp.skill_list_id, bnp.spell_list_id,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM battle_npc_spawns bns
JOIN battle_npc_groups bng ON bng.group_id = bns.group_id
JOIN battle_npc_pools bnp ON bnp.pool_id = bng.pool_id
JOIN provenance_refs p ON p.provenance_id = bns.provenance_id
WHERE bng.zone_id = @zone_id
ORDER BY bns.battle_npc_id;
""";
        command.Parameters.AddWithValue("@zone_id", zoneId.Value);

        List<BattleNpcSpawnRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new BattleNpcSpawnRecord(
                new BattleNpcId(reader.GetUInt32("battle_npc_id")),
                reader.GetUInt32("group_id"),
                reader.GetUInt32("pool_id"),
                new ZoneId(reader.GetUInt32("zone_id")),
                reader.GetString("script_name"),
                reader.GetByte("min_level"),
                reader.GetByte("max_level"),
                reader.GetFloat("position_x"),
                reader.GetFloat("position_y"),
                reader.GetFloat("position_z"),
                reader.GetFloat("rotation"),
                ReadProvenance(reader),
                reader.IsDBNull(reader.GetOrdinal("custom_display_name")) ? null : reader.GetString("custom_display_name"),
                reader.GetUInt32("genus_id"),
                reader.GetByte("current_job"),
                reader.GetByte("combat_skill"),
                reader.GetUInt16("combat_delay"),
                reader.GetFloat("combat_damage_multiplier"),
                reader.GetByte("aggro_type"),
                reader.GetUInt32("immunity"),
                reader.GetByte("link_type"),
                reader.GetUInt32("skill_list_id"),
                reader.GetUInt32("spell_list_id"),
                reader.GetUInt32("respawn_seconds"),
                reader.GetUInt32("hit_points"),
                reader.GetUInt32("magic_points"),
                reader.GetUInt32("drop_list_id"),
                reader.GetByte("allegiance"),
                reader.GetUInt16("spawn_type"),
                reader.GetUInt32("animation_id"),
                reader.GetUInt16("actor_state"),
                reader.IsDBNull(reader.GetOrdinal("private_area_name")) ? null : reader.GetString("private_area_name"),
                reader.GetUInt32("private_area_level"),
                reader.GetUInt32("actor_class_id")));
        }

        return rows;
    }

    private static ProvenanceRef ReadProvenance(MySqlDataReader reader)
    {
        return new ProvenanceRef(
            Enum.Parse<EvidenceStatus>(reader.GetString("evidence_status")),
            reader.GetString("source_type"),
            reader.GetString("source_ref"),
            reader.GetString("notes"));
    }
}
