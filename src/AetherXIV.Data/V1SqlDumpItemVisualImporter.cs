using System.Globalization;
using AetherXIV.Core;

namespace AetherXIV.Data;

public sealed record V1SqlDumpItemVisualDataSet(
    IReadOnlyList<ItemVisualRecord> ItemVisuals,
    IReadOnlyList<string> Warnings);

public sealed class V1SqlDumpItemVisualImporter
{
    public async Task<V1SqlDumpItemVisualDataSet> ImportAsync(
        string itemGraphicsSqlPath,
        string? itemGraphicsExtraSqlPath = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemGraphicsSqlPath);

        Dictionary<uint, V1ItemVisualExtraRow> extraRows = itemGraphicsExtraSqlPath is null
            ? []
            : await ReadExtraRowsAsync(itemGraphicsExtraSqlPath, cancellationToken).ConfigureAwait(false);
        List<ItemVisualRecord> itemVisuals = [];
        List<string> warnings = [];

        await foreach (SqlDumpInsertRow row in SqlDumpInsertReader
            .ReadRowsAsync(itemGraphicsSqlPath, "gamedata_items_graphics", cancellationToken)
            .ConfigureAwait(false))
        {
            if (row.Values.Count < 5)
            {
                warnings.Add($"{row.SourceRef} has {row.Values.Count} values; expected 5.");
                continue;
            }

            uint itemId = ToUInt32(row.Values[0]);
            extraRows.TryGetValue(itemId, out V1ItemVisualExtraRow? extra);
            itemVisuals.Add(new ItemVisualRecord(
                itemId,
                ToUInt32(row.Values[1]),
                ToUInt32(row.Values[2]),
                ToUInt32(row.Values[3]),
                ToUInt32(row.Values[4]),
                extra?.OffHandWeaponId ?? 0,
                extra?.OffHandEquipmentId ?? 0,
                extra?.OffHandVariantId ?? 0,
                Provenance(itemId, extra is not null)));
        }

        return new V1SqlDumpItemVisualDataSet(itemVisuals, warnings);
    }

    private static async Task<Dictionary<uint, V1ItemVisualExtraRow>> ReadExtraRowsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        Dictionary<uint, V1ItemVisualExtraRow> rows = [];
        await foreach (SqlDumpInsertRow row in SqlDumpInsertReader
            .ReadRowsAsync(path, "gamedata_items_graphics_extra", cancellationToken)
            .ConfigureAwait(false))
        {
            if (row.Values.Count < 4)
                continue;

            uint itemId = ToUInt32(row.Values[0]);
            rows[itemId] = new V1ItemVisualExtraRow(
                itemId,
                ToUInt32(row.Values[1]),
                ToUInt32(row.Values[2]),
                ToUInt32(row.Values[3]));
        }

        return rows;
    }

    private static ProvenanceRef Provenance(uint itemId, bool hasExtra)
    {
        string sourceRef = hasExtra
            ? $"gamedata_items_graphics:{itemId};gamedata_items_graphics_extra:{itemId}"
            : $"gamedata_items_graphics:{itemId}";
        return new ProvenanceRef(
            EvidenceStatus.RepoConfirmed,
            "v1-sql",
            sourceRef,
            "Imported into AetherXIV 2.0 item_visuals; original SQL table shape is not used at runtime.");
    }

    private static uint ToUInt32(string? value)
    {
        return UInt32.Parse(value ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private sealed record V1ItemVisualExtraRow(
        uint ItemId,
        uint OffHandWeaponId,
        uint OffHandEquipmentId,
        uint OffHandVariantId);
}
