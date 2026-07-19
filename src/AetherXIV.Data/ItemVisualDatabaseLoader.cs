using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed record ItemVisualDatabaseLoadRequest(
    string V1SqlRootPath,
    MariaDbOptions DatabaseOptions);

public sealed record ItemVisualDatabaseLoadResult(
    int ItemVisualInsertedCount,
    IReadOnlyList<string> Warnings);

public sealed class ItemVisualDatabaseLoader
{
    public async Task<ItemVisualDatabaseLoadResult> LoadAsync(
        ItemVisualDatabaseLoadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string v1SqlRoot = Path.GetFullPath(request.V1SqlRootPath);
        string itemGraphicsPath = Path.Combine(v1SqlRoot, "gamedata_items_graphics.sql");
        string itemGraphicsExtraPath = Path.Combine(v1SqlRoot, "gamedata_items_graphics_extra.sql");
        V1SqlDumpItemVisualImporter importer = new();
        V1SqlDumpItemVisualDataSet dataSet = await importer.ImportAsync(
            itemGraphicsPath,
            File.Exists(itemGraphicsExtraPath) ? itemGraphicsExtraPath : null,
            cancellationToken).ConfigureAwait(false);

        await using MySqlConnection connection = new(request.DatabaseOptions.ToConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            int inserted = 0;
            foreach (ItemVisualRecord itemVisual in dataSet.ItemVisuals.OrderBy(row => row.ItemId))
            {
                ulong provenanceId = await GetOrInsertProvenanceAsync(
                    connection,
                    transaction,
                    itemVisual.Provenance,
                    cancellationToken).ConfigureAwait(false);
                await UpsertItemVisualAsync(
                    connection,
                    transaction,
                    itemVisual,
                    provenanceId,
                    cancellationToken).ConfigureAwait(false);
                inserted++;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ItemVisualDatabaseLoadResult(inserted, dataSet.Warnings);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<ulong> GetOrInsertProvenanceAsync(
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

    private static async Task UpsertItemVisualAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        ItemVisualRecord itemVisual,
        ulong provenanceId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO item_visuals (
    item_id, weapon_id, equipment_id, variant_id, color_id,
    off_hand_weapon_id, off_hand_equipment_id, off_hand_variant_id, provenance_id)
VALUES (
    @item_id, @weapon_id, @equipment_id, @variant_id, @color_id,
    @off_hand_weapon_id, @off_hand_equipment_id, @off_hand_variant_id, @provenance_id)
ON DUPLICATE KEY UPDATE weapon_id = VALUES(weapon_id), equipment_id = VALUES(equipment_id),
    variant_id = VALUES(variant_id), color_id = VALUES(color_id),
    off_hand_weapon_id = VALUES(off_hand_weapon_id),
    off_hand_equipment_id = VALUES(off_hand_equipment_id),
    off_hand_variant_id = VALUES(off_hand_variant_id),
    provenance_id = VALUES(provenance_id);
""";
        command.Parameters.AddWithValue("@item_id", itemVisual.ItemId);
        command.Parameters.AddWithValue("@weapon_id", itemVisual.WeaponId);
        command.Parameters.AddWithValue("@equipment_id", itemVisual.EquipmentId);
        command.Parameters.AddWithValue("@variant_id", itemVisual.VariantId);
        command.Parameters.AddWithValue("@color_id", itemVisual.ColorId);
        command.Parameters.AddWithValue("@off_hand_weapon_id", itemVisual.OffHandWeaponId);
        command.Parameters.AddWithValue("@off_hand_equipment_id", itemVisual.OffHandEquipmentId);
        command.Parameters.AddWithValue("@off_hand_variant_id", itemVisual.OffHandVariantId);
        command.Parameters.AddWithValue("@provenance_id", provenanceId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
