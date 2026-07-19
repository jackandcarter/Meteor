using System.Globalization;
using System.Text;

namespace AetherXIV.Data;

public sealed record V1SqlDumpActorDataSet(
    IReadOnlyList<ActorClassRecord> ActorClasses,
    IReadOnlyList<ActorAppearanceRecord> ActorAppearances,
    IReadOnlyList<StaticActorSpawnRecord> StaticActorSpawns,
    IReadOnlyList<string> Warnings);

public sealed class V1SqlDumpActorDataImporter
{
    public async Task<V1SqlDumpActorDataSet> ImportAsync(
        string actorClassSqlPath,
        string actorAppearanceSqlPath,
        string staticSpawnSqlPath,
        string? actorPushCommandSqlPath = null,
        CancellationToken cancellationToken = default)
    {
        Dictionary<uint, V1ActorPushCommandRow> pushCommands = actorPushCommandSqlPath is null
            ? []
            : await ReadPushCommandsAsync(actorPushCommandSqlPath, cancellationToken).ConfigureAwait(false);
        List<string> warnings = [];

        IReadOnlyList<ActorClassRecord> actorClasses = await ReadActorClassesAsync(
            actorClassSqlPath,
            pushCommands,
            warnings,
            cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ActorAppearanceRecord> actorAppearances = await ReadActorAppearancesAsync(
            actorAppearanceSqlPath,
            warnings,
            cancellationToken).ConfigureAwait(false);
        IReadOnlyList<StaticActorSpawnRecord> staticSpawns = await ReadStaticSpawnsAsync(
            staticSpawnSqlPath,
            warnings,
            cancellationToken).ConfigureAwait(false);

        return new V1SqlDumpActorDataSet(actorClasses, actorAppearances, staticSpawns, warnings);
    }

    public static ImportedActorDataRepositories CreateRepositories(V1SqlDumpActorDataSet dataSet)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        return new ImportedActorDataRepositories(dataSet.ActorClasses, dataSet.ActorAppearances, dataSet.StaticActorSpawns);
    }

    private static async Task<Dictionary<uint, V1ActorPushCommandRow>> ReadPushCommandsAsync(
        string path,
        CancellationToken cancellationToken)
    {
        Dictionary<uint, V1ActorPushCommandRow> rows = [];
        await foreach (SqlInsertRow row in ReadInsertRowsAsync(path, "gamedata_actor_pushcommand", cancellationToken).ConfigureAwait(false))
        {
            if (row.Values.Count < 4)
                continue;

            uint actorClassId = ToUInt32(row.Values[0]);
            rows[actorClassId] = new V1ActorPushCommandRow(
                actorClassId,
                ToUInt16(row.Values[1]),
                ToUInt16(row.Values[2]),
                ToByte(row.Values[3]));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ActorClassRecord>> ReadActorClassesAsync(
        string path,
        IReadOnlyDictionary<uint, V1ActorPushCommandRow> pushCommands,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        List<ActorClassRecord> rows = [];
        await foreach (SqlInsertRow row in ReadInsertRowsAsync(path, "gamedata_actor_class", cancellationToken).ConfigureAwait(false))
        {
            if (row.Values.Count < 5)
            {
                warnings.Add($"{row.SourceRef} has {row.Values.Count} values; expected 5.");
                continue;
            }

            uint actorClassId = ToUInt32(row.Values[0]);
            pushCommands.TryGetValue(actorClassId, out V1ActorPushCommandRow? push);
            rows.Add(V1CompatibilityMappings.ToActorClassRecord(new V1ActorClassRow(
                actorClassId,
                row.Values[1] ?? String.Empty,
                ToUInt32(row.Values[2]),
                ToUInt32(row.Values[3]),
                row.Values[4],
                push?.PushCommand ?? 0,
                push?.PushCommandSub ?? 0,
                push?.PushCommandPriority ?? 0,
                $"gamedata_actor_class:{actorClassId}")));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ActorAppearanceRecord>> ReadActorAppearancesAsync(
        string path,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        List<ActorAppearanceRecord> rows = [];
        await foreach (SqlInsertRow row in ReadInsertRowsAsync(path, "gamedata_actor_appearance", cancellationToken).ConfigureAwait(false))
        {
            if (row.Values.Count < 40)
            {
                warnings.Add($"{row.SourceRef} has {row.Values.Count} values; expected 40.");
                continue;
            }

            uint actorClassId = ToUInt32(row.Values[0]);
            rows.Add(V1CompatibilityMappings.ToActorAppearanceRecord(new V1ActorAppearanceRow(
                actorClassId,
                ToUInt32(row.Values[1]),
                ToUInt32(row.Values[2]),
                ToUInt32(row.Values[3]),
                ToUInt32(row.Values[4]),
                ToUInt32(row.Values[5]),
                ToByte(row.Values[6]),
                ToByte(row.Values[7]),
                ToByte(row.Values[8]),
                ToByte(row.Values[9]),
                ToByte(row.Values[10]),
                ToByte(row.Values[11]),
                ToByte(row.Values[12]),
                ToByte(row.Values[13]),
                ToByte(row.Values[14]),
                ToByte(row.Values[15]),
                ToUInt32(row.Values[16]),
                ToUInt32(row.Values[17]),
                ToUInt32(row.Values[18]),
                ToUInt32(row.Values[19]),
                ToUInt32(row.Values[20]),
                ToUInt32(row.Values[21]),
                ToUInt32(row.Values[22]),
                ToUInt32(row.Values[23]),
                ToUInt32(row.Values[24]),
                ToUInt32(row.Values[25]),
                ToUInt32(row.Values[26]),
                ToUInt32(row.Values[27]),
                ToUInt32(row.Values[28]),
                ToUInt32(row.Values[29]),
                ToUInt32(row.Values[30]),
                ToUInt32(row.Values[31]),
                ToUInt32(row.Values[32]),
                ToUInt32(row.Values[33]),
                ToUInt32(row.Values[34]),
                ToUInt32(row.Values[35]),
                ToUInt32(row.Values[36]),
                ToUInt32(row.Values[37]),
                ToUInt32(row.Values[38]),
                ToUInt32(row.Values[39]),
                $"gamedata_actor_appearance:{actorClassId}")));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<StaticActorSpawnRecord>> ReadStaticSpawnsAsync(
        string path,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        List<StaticActorSpawnRecord> rows = [];
        await foreach (SqlInsertRow row in ReadInsertRowsAsync(path, "server_spawn_locations", cancellationToken).ConfigureAwait(false))
        {
            if (row.Values.Count < 13)
            {
                warnings.Add($"{row.SourceRef} has {row.Values.Count} values; expected 13.");
                continue;
            }

            uint spawnId = ToUInt32(row.Values[0]);
            rows.Add(V1CompatibilityMappings.ToStaticActorSpawnRecord(new V1StaticActorSpawnRow(
                spawnId,
                ToUInt32(row.Values[1]),
                row.Values[2] ?? String.Empty,
                ToUInt32(row.Values[3]),
                row.Values[4],
                ToUInt32(row.Values[5]),
                ToSingle(row.Values[6]),
                ToSingle(row.Values[7]),
                ToSingle(row.Values[8]),
                ToSingle(row.Values[9]),
                ToUInt16(row.Values[10]),
                ToUInt32(row.Values[11]),
                row.Values[12],
                $"server_spawn_locations:{spawnId}")));
        }

        return rows;
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

    private static byte ToByte(string? value)
    {
        return Byte.Parse(value ?? "0", NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static float ToSingle(string? value)
    {
        return Single.Parse(value ?? "0", NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    private sealed record V1ActorPushCommandRow(
        uint ActorClassId,
        ushort PushCommand,
        ushort PushCommandSub,
        byte PushCommandPriority);

    private sealed record SqlInsertRow(string SourceRef, IReadOnlyList<string?> Values);
}

public sealed class ImportedActorDataRepositories :
    IActorClassRepository,
    IActorAppearanceRepository,
    IActorSpawnRepository
{
    private readonly IReadOnlyDictionary<uint, ActorClassRecord> actorClasses;
    private readonly IReadOnlyDictionary<uint, ActorAppearanceRecord> actorAppearances;
    private readonly IReadOnlyDictionary<AetherXIV.Core.ZoneId, IReadOnlyList<StaticActorSpawnRecord>> staticSpawnsByZone;
    private readonly IReadOnlyDictionary<AetherXIV.Core.ZoneId, IReadOnlyList<BattleNpcSpawnRecord>> battleNpcSpawnsByZone;

    public ImportedActorDataRepositories(
        IEnumerable<ActorClassRecord> actorClasses,
        IEnumerable<ActorAppearanceRecord> actorAppearances,
        IEnumerable<StaticActorSpawnRecord> staticSpawns,
        IEnumerable<BattleNpcSpawnRecord>? battleNpcSpawns = null)
    {
        this.actorClasses = actorClasses.ToDictionary(row => row.ActorClassId);
        this.actorAppearances = actorAppearances.ToDictionary(row => row.ActorClassId);
        staticSpawnsByZone = staticSpawns
            .GroupBy(row => row.ZoneId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<StaticActorSpawnRecord>)group.ToArray());
        battleNpcSpawnsByZone = (battleNpcSpawns ?? [])
            .GroupBy(row => row.ZoneId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<BattleNpcSpawnRecord>)group.ToArray());
    }

    public Task<ActorClassRecord?> GetAsync(uint actorClassId, CancellationToken cancellationToken = default)
    {
        actorClasses.TryGetValue(actorClassId, out ActorClassRecord? row);
        return Task.FromResult(row);
    }

    public Task<IReadOnlyList<ActorClassRecord>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ActorClassRecord>>(actorClasses.Values.OrderBy(row => row.ActorClassId).ToArray());

    Task<ActorAppearanceRecord?> IActorAppearanceRepository.GetAsync(uint actorClassId, CancellationToken cancellationToken)
    {
        actorAppearances.TryGetValue(actorClassId, out ActorAppearanceRecord? row);
        return Task.FromResult(row);
    }

    Task<IReadOnlyList<ActorAppearanceRecord>> IActorAppearanceRepository.ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ActorAppearanceRecord>>(actorAppearances.Values.OrderBy(row => row.ActorClassId).ToArray());

    public Task<IReadOnlyList<StaticActorSpawnRecord>> ListStaticSpawnsAsync(
        AetherXIV.Core.ZoneId zoneId,
        CancellationToken cancellationToken = default)
    {
        staticSpawnsByZone.TryGetValue(zoneId, out IReadOnlyList<StaticActorSpawnRecord>? rows);
        return Task.FromResult(rows ?? []);
    }

    public Task<IReadOnlyList<BattleNpcSpawnRecord>> ListBattleNpcSpawnsAsync(
        AetherXIV.Core.ZoneId zoneId,
        CancellationToken cancellationToken = default)
    {
        battleNpcSpawnsByZone.TryGetValue(zoneId, out IReadOnlyList<BattleNpcSpawnRecord>? rows);
        return Task.FromResult(rows ?? []);
    }
}
