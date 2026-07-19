using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbWeaponCombatProfileRepository : IWeaponCombatProfileRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbWeaponCombatProfileRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<WeaponCombatProfileRecord?> GetAsync(
        uint itemId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT wcp.item_id, wcp.name, wcp.class_job, wcp.equip_point, wcp.hit_count,
       wcp.damage_attribute, wcp.damage_power, wcp.damage_interval_ms,
       wcp.ammo_virtual_damage_power,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM weapon_combat_profiles wcp
JOIN provenance_refs p ON p.provenance_id = wcp.provenance_id
WHERE wcp.item_id = @item_id;
""";
        command.Parameters.AddWithValue("@item_id", itemId);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new WeaponCombatProfileRecord(
            reader.GetUInt32("item_id"),
            reader.GetString("name"),
            reader.GetByte("class_job"),
            reader.GetUInt16("equip_point"),
            reader.GetByte("hit_count"),
            reader.GetUInt16("damage_attribute"),
            reader.GetUInt16("damage_power"),
            reader.GetUInt32("damage_interval_ms"),
            reader.GetUInt16("ammo_virtual_damage_power"),
            new ProvenanceRef(
                Enum.Parse<EvidenceStatus>(reader.GetString("evidence_status")),
                reader.GetString("source_type"),
                reader.GetString("source_ref"),
                reader.GetString("notes")));
    }
}

public sealed class MariaDbBattleCommandRepository : IBattleCommandRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbBattleCommandRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<BattleCommandRecord?> GetAsync(ushort commandId, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = BattleCommandSelectSql + " WHERE bc.command_id = @command_id;";
        command.Parameters.AddWithValue("@command_id", commandId);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadBattleCommand(reader) : null;
    }

    public async Task<IReadOnlyList<BattleCommandRecord>> ListForClassLevelAsync(byte classJob, ushort level, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = BattleCommandSelectSql + """
 WHERE bc.class_job = @class_job
   AND bc.level <= @level
 ORDER BY bc.level, bc.command_id;
""";
        command.Parameters.AddWithValue("@class_job", classJob);
        command.Parameters.AddWithValue("@level", level);

        List<BattleCommandRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(ReadBattleCommand(reader));

        return rows;
    }

    private const string BattleCommandSelectSql = """
	SELECT bc.command_id, bc.name, bc.class_job, bc.level, bc.requirements, bc.main_target, bc.valid_target,
	       bc.aoe_type, bc.aoe_target, bc.base_potency, bc.num_hits, bc.range_yalms, bc.min_range_yalms,
	       bc.range_height, bc.range_width, bc.battle_animation, bc.world_master_text_id, bc.command_type,
	       bc.mp_cost, bc.tp_cost, bc.recast_time_ms, bc.action_type, bc.action_property,
	       bc.aoe_range_yalms, bc.aoe_min_range_yalms, bc.aoe_cone_angle, bc.aoe_rotate_angle,
	       bc.position_bonus, bc.proc_requirement, bc.best_range_yalms, bc.status_id,
	       bc.status_duration_seconds, bc.status_chance, bc.cast_type, bc.cast_time_ms,
	       bc.animation_type, bc.effect_animation, bc.model_animation, bc.animation_duration_seconds,
	       bc.valid_user, bc.combo_command_id_1, bc.combo_command_id_2, bc.combo_step,
	       bc.accuracy_modifier, bc.is_ranged,
	       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM battle_commands bc
JOIN provenance_refs p ON p.provenance_id = bc.provenance_id
""";

    private static BattleCommandRecord ReadBattleCommand(MySqlDataReader reader)
    {
        return new BattleCommandRecord(
            reader.GetUInt16("command_id"),
            reader.GetString("name"),
            reader.GetByte("class_job"),
            reader.GetByte("level"),
            reader.GetUInt16("requirements"),
            reader.GetUInt16("main_target"),
            reader.GetUInt16("valid_target"),
            reader.GetByte("aoe_type"),
            reader.GetByte("aoe_target"),
            reader.GetUInt16("base_potency"),
            reader.GetByte("num_hits"),
            reader.GetFloat("range_yalms"),
            reader.GetFloat("min_range_yalms"),
            reader.GetInt32("range_height"),
            reader.GetInt32("range_width"),
            reader.GetUInt32("battle_animation"),
            reader.GetUInt16("world_master_text_id"),
            (BattleCommandType)reader.GetUInt16("command_type"),
            reader.GetInt16("mp_cost"),
            reader.GetInt16("tp_cost"),
            reader.GetUInt32("recast_time_ms"),
            reader.GetUInt16("action_type"),
            reader.GetUInt16("action_property"),
            ReadProvenance(reader),
            reader.GetFloat("aoe_range_yalms"),
            reader.GetFloat("aoe_min_range_yalms"),
            reader.GetFloat("aoe_cone_angle"),
            reader.GetFloat("aoe_rotate_angle"),
            reader.GetByte("position_bonus"),
            reader.GetByte("proc_requirement"),
            reader.GetFloat("best_range_yalms"),
            reader.GetUInt32("status_id"),
            reader.GetUInt32("status_duration_seconds"),
            reader.GetFloat("status_chance"),
            reader.GetByte("cast_type"),
            reader.GetUInt32("cast_time_ms"),
            reader.GetByte("animation_type"),
            reader.GetUInt16("effect_animation"),
            reader.GetUInt16("model_animation"),
            reader.GetUInt32("animation_duration_seconds"),
            reader.GetByte("valid_user"),
            reader.GetUInt16("combo_command_id_1"),
            reader.GetUInt16("combo_command_id_2"),
            reader.GetByte("combo_step"),
            reader.GetFloat("accuracy_modifier"),
            reader.GetBoolean("is_ranged"));
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

public sealed class MariaDbBattleCommandScriptRepository : IBattleCommandScriptRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbBattleCommandScriptRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<BattleCommandScriptRecord?> GetAsync(ushort commandId, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT bcs.command_id, bcs.script_folder, bcs.script_name,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM battle_command_scripts bcs
JOIN provenance_refs p ON p.provenance_id = bcs.provenance_id
WHERE bcs.command_id = @command_id;
""";
        command.Parameters.AddWithValue("@command_id", commandId);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new BattleCommandScriptRecord(
            reader.GetUInt16("command_id"),
            reader.GetString("script_folder"),
            reader.GetString("script_name"),
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

public sealed class MariaDbBattleTraitRepository : IBattleTraitRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbBattleTraitRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<BattleTraitRecord>> ListForClassLevelAsync(
        byte classJob,
        ushort level,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT bt.trait_id, bt.name, bt.class_job, bt.level, bt.modifier_id, bt.bonus,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM battle_traits bt
JOIN provenance_refs p ON p.provenance_id = bt.provenance_id
WHERE bt.class_job = @class_job
  AND bt.level <= @level
ORDER BY bt.level, bt.trait_id;
""";
        command.Parameters.AddWithValue("@class_job", classJob);
        command.Parameters.AddWithValue("@level", level);

        List<BattleTraitRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new BattleTraitRecord(
                reader.GetUInt16("trait_id"),
                reader.GetString("name"),
                reader.GetByte("class_job"),
                reader.GetByte("level"),
                reader.GetUInt32("modifier_id"),
                reader.GetInt16("bonus"),
                ReadProvenance(reader)));
        }

        return rows;
    }

    private static ProvenanceRef ReadProvenance(MySqlDataReader reader) => new(
        Enum.Parse<EvidenceStatus>(reader.GetString("evidence_status")),
        reader.GetString("source_type"),
        reader.GetString("source_ref"),
        reader.GetString("notes"));
}

public sealed class MariaDbStatusEffectDefinitionRepository : IStatusEffectDefinitionRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbStatusEffectDefinitionRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<StatusEffectDefinitionRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT se.status_effect_id, se.name, se.flags, se.overwrite_tier, se.tick_ms,
       se.hidden, se.silent_on_gain, se.silent_on_loss, se.gain_text_id, se.loss_text_id,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM status_effects se
JOIN provenance_refs p ON p.provenance_id = se.provenance_id
ORDER BY se.status_effect_id;
""";

        List<StatusEffectDefinitionRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new StatusEffectDefinitionRecord(
                reader.GetUInt32("status_effect_id"),
                reader.GetString("name"),
                reader.GetUInt32("flags"),
                reader.GetByte("overwrite_tier"),
                reader.GetUInt32("tick_ms"),
                reader.GetBoolean("hidden"),
                reader.GetBoolean("silent_on_gain"),
                reader.GetBoolean("silent_on_loss"),
                reader.GetUInt16("gain_text_id"),
                reader.GetUInt16("loss_text_id"),
                ReadProvenance(reader)));
        }

        return rows;
    }

    private static ProvenanceRef ReadProvenance(MySqlDataReader reader) => new(
        Enum.Parse<EvidenceStatus>(reader.GetString("evidence_status")),
        reader.GetString("source_type"),
        reader.GetString("source_ref"),
        reader.GetString("notes"));
}

public sealed class MariaDbBattleNpcStatRepository : IBattleNpcStatRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbBattleNpcStatRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<BattleNpcStatRecord>> ListForBattleNpcAsync(BattleNpcId battleNpcId, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT bns.battle_npc_id, bns.stat_id, bns.stat_value,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM battle_npc_stats bns
JOIN provenance_refs p ON p.provenance_id = bns.provenance_id
WHERE bns.battle_npc_id = @battle_npc_id
ORDER BY bns.stat_id;
""";
        command.Parameters.AddWithValue("@battle_npc_id", battleNpcId.Value);

        List<BattleNpcStatRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new BattleNpcStatRecord(
                new BattleNpcId(reader.GetUInt32("battle_npc_id")),
                reader.GetUInt16("stat_id"),
                reader.GetInt32("stat_value"),
                ReadProvenance(reader)));
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

public sealed class MariaDbBattleNpcActionRepository : IBattleNpcActionRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbBattleNpcActionRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<BattleNpcActionRecord>> ListForActionListAsync(uint listId, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT bnal.list_id, bnal.command_id, bnal.command_type, bnal.priority,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM battle_npc_action_lists bnal
JOIN provenance_refs p ON p.provenance_id = bnal.provenance_id
WHERE bnal.list_id = @list_id
ORDER BY bnal.priority, bnal.command_id;
""";
        command.Parameters.AddWithValue("@list_id", listId);

        List<BattleNpcActionRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new BattleNpcActionRecord(
                reader.GetUInt32("list_id"),
                reader.GetUInt16("command_id"),
                (BattleCommandType)reader.GetUInt16("command_type"),
                reader.GetByte("priority"),
                ReadProvenance(reader)));
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
