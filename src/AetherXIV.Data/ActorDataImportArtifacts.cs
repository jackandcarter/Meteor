using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AetherXIV.Data;

public sealed record ActorDataImportArtifactRequest(
    string V1SqlRootPath,
    string OutputRootPath,
    string? ClientActorImportFocusPath = null);

public sealed record ActorDataImportArtifactResult(
    ActorDataImportSummary Summary,
    ActorDataImportArtifactPaths Paths);

public sealed record ActorDataImportArtifactPaths(
    string SummaryJsonPath,
    string SummaryMarkdownPath,
    string ZonesJsonPath,
    string ActorClassesJsonPath,
    string ActorAppearancesJsonPath,
    string StaticActorSpawnsJsonPath,
    string MissingRelationshipsJsonPath);

public sealed record ActorDataImportSummary(
    int ActorClassCount,
    int ActorAppearanceCount,
    int StaticActorSpawnCount,
    int WarningCount,
    int MissingRelationshipCount,
    IReadOnlyList<ActorDataImportRelationshipKindCount> MissingRelationshipKindCounts,
    IReadOnlyList<uint> ZoneIds,
    IReadOnlyList<ActorDataImportMissingRelationship> MissingRelationships,
    IReadOnlyList<string> Warnings,
    string? ClientActorImportFocusPath);

public sealed record ActorDataImportRelationshipKindCount(string Kind, int Count);

public sealed record ActorDataImportMissingRelationship(
    string Kind,
    string SourceRef,
    uint? ActorClassId,
    uint? SpawnId,
    string Detail);

public sealed class ActorDataImportArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<ActorDataImportArtifactResult> WriteAsync(
        ActorDataImportArtifactRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string v1SqlRoot = Path.GetFullPath(request.V1SqlRootPath);
        string outputRoot = Path.GetFullPath(request.OutputRootPath);
        Directory.CreateDirectory(outputRoot);

        string actorClassPath = RequiredSqlPath(v1SqlRoot, "gamedata_actor_class.sql");
        string actorPushCommandPath = RequiredSqlPath(v1SqlRoot, "gamedata_actor_pushcommand.sql");
        string actorAppearancePath = RequiredSqlPath(v1SqlRoot, "gamedata_actor_appearance.sql");
        string staticSpawnPath = RequiredSqlPath(v1SqlRoot, "server_spawn_locations.sql");
        string zonePath = RequiredSqlPath(v1SqlRoot, "server_zones.sql");

        V1SqlDumpActorDataImporter importer = new();
        V1SqlDumpActorDataSet dataSet = await importer.ImportAsync(
            actorClassPath,
            actorAppearancePath,
            staticSpawnPath,
            actorPushCommandPath,
            cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ZoneRecord> zones = await new V1SqlDumpZoneDataImporter()
            .ImportAsync(zonePath, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<ActorDataImportMissingRelationship> missing = BuildMissingRelationships(dataSet);
        ActorDataImportSummary summary = new(
            dataSet.ActorClasses.Count,
            dataSet.ActorAppearances.Count,
            dataSet.StaticActorSpawns.Count,
            dataSet.Warnings.Count,
            missing.Count,
            missing
                .GroupBy(row => row.Kind)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new ActorDataImportRelationshipKindCount(group.Key, group.Count()))
                .ToArray(),
            dataSet.StaticActorSpawns.Select(row => row.ZoneId.Value).Distinct().Order().ToArray(),
            missing,
            dataSet.Warnings.ToArray(),
            ResolveOptionalPath(request.ClientActorImportFocusPath));

        ActorDataImportArtifactPaths paths = new(
            Path.Combine(outputRoot, "actor-data-summary.json"),
            Path.Combine(outputRoot, "actor-data-summary.md"),
            Path.Combine(outputRoot, "zones.json"),
            Path.Combine(outputRoot, "actor-classes.json"),
            Path.Combine(outputRoot, "actor-appearances.json"),
            Path.Combine(outputRoot, "static-actor-spawns.json"),
            Path.Combine(outputRoot, "missing-relationships.json"));

        await WriteJsonAsync(paths.SummaryJsonPath, summary, cancellationToken).ConfigureAwait(false);
        await WriteMarkdownAsync(paths.SummaryMarkdownPath, summary, cancellationToken).ConfigureAwait(false);
        await WriteJsonAsync(paths.ZonesJsonPath, zones.OrderBy(row => row.Id.Value), cancellationToken).ConfigureAwait(false);
        await WriteJsonAsync(paths.ActorClassesJsonPath, dataSet.ActorClasses.OrderBy(row => row.ActorClassId), cancellationToken).ConfigureAwait(false);
        await WriteJsonAsync(paths.ActorAppearancesJsonPath, dataSet.ActorAppearances.OrderBy(row => row.ActorClassId), cancellationToken).ConfigureAwait(false);
        await WriteJsonAsync(paths.StaticActorSpawnsJsonPath, dataSet.StaticActorSpawns.OrderBy(row => row.ZoneId.Value).ThenBy(row => row.SpawnId), cancellationToken).ConfigureAwait(false);
        await WriteJsonAsync(paths.MissingRelationshipsJsonPath, missing, cancellationToken).ConfigureAwait(false);

        return new ActorDataImportArtifactResult(summary, paths);
    }

    private static IReadOnlyList<ActorDataImportMissingRelationship> BuildMissingRelationships(V1SqlDumpActorDataSet dataSet)
    {
        HashSet<uint> actorClassIds = dataSet.ActorClasses.Select(row => row.ActorClassId).ToHashSet();
        HashSet<uint> appearanceActorClassIds = dataSet.ActorAppearances.Select(row => row.ActorClassId).ToHashSet();
        List<ActorDataImportMissingRelationship> missing = [];

        foreach (StaticActorSpawnRecord spawn in dataSet.StaticActorSpawns.OrderBy(row => row.SpawnId))
        {
            if (!actorClassIds.Contains(spawn.ActorClassId))
            {
                missing.Add(new ActorDataImportMissingRelationship(
                    "StaticSpawnMissingActorClass",
                    spawn.Provenance.SourceRef,
                    spawn.ActorClassId,
                    spawn.SpawnId,
                    $"Static spawn {spawn.SpawnId} references actor class {spawn.ActorClassId}, but that actor class was not imported."));
            }
        }

        foreach (ActorClassRecord actorClass in dataSet.ActorClasses.OrderBy(row => row.ActorClassId))
        {
            if (!appearanceActorClassIds.Contains(actorClass.ActorClassId))
            {
                missing.Add(new ActorDataImportMissingRelationship(
                    "ActorClassMissingAppearance",
                    actorClass.Provenance.SourceRef,
                    actorClass.ActorClassId,
                    null,
                    $"Actor class {actorClass.ActorClassId} has no imported appearance row."));
            }
        }

        foreach (ActorAppearanceRecord appearance in dataSet.ActorAppearances.OrderBy(row => row.ActorClassId))
        {
            if (!actorClassIds.Contains(appearance.ActorClassId))
            {
                missing.Add(new ActorDataImportMissingRelationship(
                    "AppearanceMissingActorClass",
                    appearance.Provenance.SourceRef,
                    appearance.ActorClassId,
                    null,
                    $"Appearance row {appearance.ActorClassId} has no imported actor class row."));
            }
        }

        foreach (ActorClassRecord actorClass in dataSet.ActorClasses.OrderBy(row => row.ActorClassId))
        {
            if (!IsValidJson(actorClass.EventConditions))
            {
                missing.Add(new ActorDataImportMissingRelationship(
                    "ActorClassInvalidEventConditions",
                    actorClass.Provenance.SourceRef,
                    actorClass.ActorClassId,
                    null,
                    $"Actor class {actorClass.ActorClassId} has event condition JSON that cannot be parsed."));
            }
        }

        return missing;
    }

    private static bool IsValidJson(string json)
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

    private static string RequiredSqlPath(string root, string fileName)
    {
        string path = Path.Combine(root, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Required v1 SQL dump file was not found: {path}", path);

        return path;
    }

    private static string? ResolveOptionalPath(string? path)
    {
        if (String.IsNullOrWhiteSpace(path))
            return null;

        return Path.GetFullPath(path);
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteMarkdownAsync(
        string path,
        ActorDataImportSummary summary,
        CancellationToken cancellationToken)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Actor Data Import Review");
        builder.AppendLine();
        builder.AppendLine("This report is a reviewed artifact from v1 SQL dump data. It is not generated server behavior.");
        builder.AppendLine();
        builder.AppendLine("## Counts");
        builder.AppendLine();
        builder.AppendLine($"- Actor classes: {summary.ActorClassCount}");
        builder.AppendLine($"- Actor appearances: {summary.ActorAppearanceCount}");
        builder.AppendLine($"- Static actor spawns: {summary.StaticActorSpawnCount}");
        builder.AppendLine($"- Warnings: {summary.WarningCount}");
        builder.AppendLine($"- Missing relationships: {summary.MissingRelationshipCount}");
        builder.AppendLine($"- Zones with static spawns: {String.Join(", ", summary.ZoneIds)}");
        if (summary.ClientActorImportFocusPath is not null)
            builder.AppendLine($"- Client actor-import focus evidence: {summary.ClientActorImportFocusPath}");
        builder.AppendLine();
        builder.AppendLine("## Missing Relationship Counts");
        builder.AppendLine();
        if (summary.MissingRelationshipKindCounts.Count == 0)
        {
            builder.AppendLine("- None detected.");
        }
        else
        {
            foreach (ActorDataImportRelationshipKindCount count in summary.MissingRelationshipKindCounts)
                builder.AppendLine($"- {count.Kind}: {count.Count}");
        }

        builder.AppendLine();
        builder.AppendLine("## Missing Relationships");
        builder.AppendLine();
        if (summary.MissingRelationships.Count == 0)
        {
            builder.AppendLine("- None detected.");
        }
        else
        {
            foreach (ActorDataImportMissingRelationship item in summary.MissingRelationships)
                builder.AppendLine($"- {item.Kind}: {item.Detail} Source: `{item.SourceRef}`.");
        }

        builder.AppendLine();
        builder.AppendLine("## Warnings");
        builder.AppendLine();
        if (summary.Warnings.Count == 0)
        {
            builder.AppendLine("- None.");
        }
        else
        {
            foreach (string warning in summary.Warnings)
                builder.AppendLine($"- {warning}");
        }

        await File.WriteAllTextAsync(path, builder.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
