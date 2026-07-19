using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbZoneEntranceRepository : IZoneEntranceRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbZoneEntranceRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<ZoneEntranceRecord?> GetAsync(
        uint entranceId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT e.entrance_id, e.zone_id, e.private_area_name, e.private_area_level,
       e.spawn_type, e.position_x, e.position_y, e.position_z, e.rotation,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM zone_entrances e
JOIN provenance_refs p ON p.provenance_id = e.provenance_id
WHERE e.entrance_id = @entrance_id;
""";
        command.Parameters.AddWithValue("@entrance_id", entranceId);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new ZoneEntranceRecord(
            reader.GetUInt32("entrance_id"),
            new ZoneId(reader.GetUInt32("zone_id")),
            reader.IsDBNull(reader.GetOrdinal("private_area_name"))
                ? null
                : reader.GetString("private_area_name"),
            reader.GetUInt32("private_area_level"),
            reader.GetUInt16("spawn_type"),
            reader.GetFloat("position_x"),
            reader.GetFloat("position_y"),
            reader.GetFloat("position_z"),
            reader.GetFloat("rotation"),
            new ProvenanceRef(
                Enum.Parse<EvidenceStatus>(reader.GetString("evidence_status"), ignoreCase: true),
                reader.GetString("source_type"),
                reader.GetString("source_ref"),
                reader.GetString("notes")));
    }
}
