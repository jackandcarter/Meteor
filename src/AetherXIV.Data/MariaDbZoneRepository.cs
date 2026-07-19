using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbZoneRepository : IZoneRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbZoneRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<ZoneRecord?> GetAsync(ZoneId zoneId, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT zone_id, name, region_id, is_private, load_nav_mesh, class_path,
       day_music, night_music, battle_music, is_inn, can_ride_chocobo,
       can_stealth, is_instance_raid
FROM zones
WHERE zone_id = @zone_id;
""";
        command.Parameters.AddWithValue("@zone_id", zoneId.Value);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return ReadZone(reader);
    }

    public async Task<IReadOnlyList<ZoneRecord>> ListForWorldAsync(
        WorldId worldId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT zone_id, name, region_id, is_private, load_nav_mesh, class_path,
       day_music, night_music, battle_music, is_inn, can_ride_chocobo,
       can_stealth, is_instance_raid
FROM zones
WHERE world_id = @world_id
ORDER BY zone_id;
""";
        command.Parameters.AddWithValue("@world_id", worldId.Value);

        List<ZoneRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            rows.Add(ReadZone(reader));

        return rows;
    }

    private static ZoneRecord ReadZone(MySqlDataReader reader)
    {
        return new ZoneRecord(
            new ZoneId(reader.GetUInt32("zone_id")),
            reader.GetString("name"),
            reader.GetUInt32("region_id"),
            reader.GetBoolean("is_private"),
            reader.GetBoolean("load_nav_mesh"),
            reader.GetString("class_path"),
            reader.GetUInt16("day_music"),
            reader.GetUInt16("night_music"),
            reader.GetUInt16("battle_music"),
            reader.GetBoolean("is_inn"),
            reader.GetBoolean("can_ride_chocobo"),
            reader.GetBoolean("can_stealth"),
            reader.GetBoolean("is_instance_raid"));
    }
}
