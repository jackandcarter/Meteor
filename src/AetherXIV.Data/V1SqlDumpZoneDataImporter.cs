using System.Globalization;
using System.Text;
using AetherXIV.Core;

namespace AetherXIV.Data;

public sealed class V1SqlDumpZoneDataImporter
{
    public async Task<IReadOnlyList<ZoneRecord>> ImportAsync(
        string serverZonesSqlPath,
        CancellationToken cancellationToken = default)
    {
        List<ZoneRecord> zones = [];
        await foreach (SqlInsertRow row in ReadInsertRowsAsync(serverZonesSqlPath, "server_zones", cancellationToken).ConfigureAwait(false))
        {
            if (row.Values.Count < 16)
                continue;

            uint zoneId = ToUInt32(row.Values[0]);
            uint regionId = ToUInt32(row.Values[1]);
            string? zoneName = row.Values[2];
            string placeName = row.Values[3] ?? String.Empty;
            string classPath = row.Values[6] ?? String.Empty;
            ushort dayMusic = ToUInt16(row.Values[7]);
            ushort nightMusic = ToUInt16(row.Values[8]);
            ushort battleMusic = ToUInt16(row.Values[9]);
            bool isInn = ToBoolean(row.Values[11]);
            bool canRideChocobo = ToBoolean(row.Values[12]);
            bool canStealth = ToBoolean(row.Values[13]);
            bool isInstanceRaid = ToBoolean(row.Values[14]);
            bool loadNavMesh = ToBoolean(row.Values[15]);
            string name = String.IsNullOrWhiteSpace(zoneName)
                ? String.IsNullOrWhiteSpace(placeName) ? $"Zone {zoneId}" : placeName
                : zoneName;

            zones.Add(new ZoneRecord(
                new ZoneId(zoneId),
                name,
                regionId,
                false,
                loadNavMesh,
                classPath,
                dayMusic,
                nightMusic,
                battleMusic,
                isInn,
                canRideChocobo,
                canStealth,
                isInstanceRaid));
        }

        return zones;
    }

    private static async IAsyncEnumerable<SqlInsertRow> ReadInsertRowsAsync(
        string path,
        string tableName,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using StreamReader reader = File.OpenText(path);
        string prefix = $"INSERT INTO `{tableName}` VALUES ";
        int lineNumber = 0;
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            lineNumber++;
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            string valuesText = line[prefix.Length..].Trim();
            if (valuesText.EndsWith(';'))
                valuesText = valuesText[..^1];

            yield return new SqlInsertRow(
                $"{tableName}:{lineNumber}",
                ParseValues(valuesText));
        }
    }

    private static IReadOnlyList<string?> ParseValues(string valuesText)
    {
        if (valuesText.Length < 2 || valuesText[0] != '(' || valuesText[^1] != ')')
            throw new FormatException("SQL INSERT values must be wrapped in parentheses.");

        List<string?> values = [];
        StringBuilder current = new();
        bool inString = false;
        bool escaped = false;
        for (int i = 1; i < valuesText.Length - 1; i++)
        {
            char c = valuesText[i];
            if (inString)
            {
                if (escaped)
                {
                    current.Append(Unescape(c));
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '\'')
                {
                    inString = false;
                    continue;
                }

                current.Append(c);
                continue;
            }

            if (c == '\'')
            {
                inString = true;
                continue;
            }

            if (c == ',')
            {
                values.Add(NormalizeValue(current.ToString()));
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        values.Add(NormalizeValue(current.ToString()));
        return values;
    }

    private static char Unescape(char c)
    {
        return c switch
        {
            'r' => '\r',
            'n' => '\n',
            't' => '\t',
            '0' => '\0',
            _ => c
        };
    }

    private static string? NormalizeValue(string value)
    {
        string trimmed = value.Trim();
        return trimmed.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : trimmed;
    }

    private static uint ToUInt32(string? value)
    {
        return UInt32.Parse(value ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static ushort ToUInt16(string? value)
    {
        return UInt16.Parse(value ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static bool ToBoolean(string? value)
    {
        return ToUInt32(value) != 0;
    }

    private sealed record SqlInsertRow(string SourceRef, IReadOnlyList<string?> Values);
}
