using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbZoneSeamlessBoundaryRepository : IZoneSeamlessBoundaryRepository
{
    private const string SelectColumns = """
SELECT b.boundary_id, b.region_id, b.zone_a_id, b.zone_b_id,
       b.zone_a_min_x, b.zone_a_max_x, b.zone_a_min_z, b.zone_a_max_z,
       b.zone_b_min_x, b.zone_b_max_x, b.zone_b_min_z, b.zone_b_max_z,
       b.merge_min_x, b.merge_max_x, b.merge_min_z, b.merge_max_z,
       p.evidence_status, p.source_type, p.source_ref, p.notes
FROM zone_seamless_boundaries b
JOIN provenance_refs p ON p.provenance_id = b.provenance_id
""";
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbZoneSeamlessBoundaryRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyList<ZoneSeamlessBoundaryRecord>> ListAllAsync(
        CancellationToken cancellationToken = default) =>
        QueryAsync(null, cancellationToken);

    public Task<IReadOnlyList<ZoneSeamlessBoundaryRecord>> ListByRegionAsync(
        uint regionId,
        CancellationToken cancellationToken = default) =>
        QueryAsync(regionId, cancellationToken);

    private async Task<IReadOnlyList<ZoneSeamlessBoundaryRecord>> QueryAsync(
        uint? regionId,
        CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = regionId.HasValue
            ? SelectColumns + " WHERE b.region_id = @region_id ORDER BY b.boundary_id;"
            : SelectColumns + " ORDER BY b.boundary_id;";
        if (regionId.HasValue)
            command.Parameters.AddWithValue("@region_id", regionId.Value);

        List<ZoneSeamlessBoundaryRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ZoneSeamlessBoundaryRecord(
                reader.GetUInt32("boundary_id"),
                reader.GetUInt32("region_id"),
                new ZoneId(reader.GetUInt32("zone_a_id")),
                new ZoneId(reader.GetUInt32("zone_b_id")),
                reader.GetFloat("zone_a_min_x"),
                reader.GetFloat("zone_a_max_x"),
                reader.GetFloat("zone_a_min_z"),
                reader.GetFloat("zone_a_max_z"),
                reader.GetFloat("zone_b_min_x"),
                reader.GetFloat("zone_b_max_x"),
                reader.GetFloat("zone_b_min_z"),
                reader.GetFloat("zone_b_max_z"),
                reader.GetFloat("merge_min_x"),
                reader.GetFloat("merge_max_x"),
                reader.GetFloat("merge_min_z"),
                reader.GetFloat("merge_max_z"),
                new ProvenanceRef(
                    Enum.Parse<EvidenceStatus>(reader.GetString("evidence_status"), ignoreCase: true),
                    reader.GetString("source_type"),
                    reader.GetString("source_ref"),
                    reader.GetString("notes"))));
        }

        return rows;
    }
}
