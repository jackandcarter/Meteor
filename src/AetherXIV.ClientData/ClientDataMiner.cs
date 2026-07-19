using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AetherXIV.ClientData;

public sealed class ClientDataMiner
{
    private const int SchemaVersion = 13;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<ClientDataMiningResult> MineAsync(
        ClientDataMiningRequest request,
        CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(request.ClientRootPath))
            return ClientDataMiningResult.Failed("Client root path is required.");

        if (String.IsNullOrWhiteSpace(request.OutputRootPath))
            return ClientDataMiningResult.Failed("Output root path is required.");

        string clientRoot = Path.GetFullPath(request.ClientRootPath);
        string outputRoot = Path.GetFullPath(request.OutputRootPath);

        if (!Directory.Exists(clientRoot))
            return ClientDataMiningResult.Failed($"Client root path does not exist: {clientRoot}");

        Directory.CreateDirectory(outputRoot);

        List<string> warnings = [];
        List<ClientDataFileRecord> files = [];
        int visitedFileCount = 0;

        request.Progress?.Report(new ClientDataMiningProgress("Scanning", visitedFileCount, files.Count, clientRoot));

        foreach (string filePath in EnumerateFilesSafely(clientRoot, warnings))
        {
            cancellationToken.ThrowIfCancellationRequested();
            visitedFileCount++;

            if (visitedFileCount % 5_000 == 0)
                request.Progress?.Report(new ClientDataMiningProgress("Scanning", visitedFileCount, files.Count, filePath));

            ClientDataKind kind = ClientDataClassifier.Classify(clientRoot, filePath);
            if (kind == ClientDataKind.UnknownCandidate && !ClientDataClassifier.IsCandidate(clientRoot, filePath))
                continue;

            try
            {
                files.Add(await CreateFileRecordAsync(clientRoot, filePath, kind, request, cancellationToken));

                if (ShouldReportCandidateProgress(files.Count))
                    request.Progress?.Report(new ClientDataMiningProgress("CatalogingCandidates", visitedFileCount, files.Count, filePath));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or FileNotFoundException)
            {
                warnings.Add($"Could not read candidate file '{filePath}': {ex.Message}");
            }
        }

        if (files.Count == 0)
            warnings.Add("No .gmd, .geb, or sqpack candidate files were found under the client root.");

        if (!files.Any(file => file.Kind is ClientDataKind.LooseGmd or ClientDataKind.LooseGeb))
            warnings.Add("No loose .gmd or .geb files were found. Packed .DAT resources are header-probed only in this pass.");

        ClientDataManifest manifest = new(
            SchemaVersion,
            DateTimeOffset.UtcNow,
            clientRoot,
            outputRoot,
            CreateSummary(files),
            files,
            warnings);

        string manifestPath = Path.Combine(outputRoot, "client-data-manifest.json");
        request.Progress?.Report(new ClientDataMiningProgress("WritingManifest", visitedFileCount, files.Count, manifestPath));
        await using FileStream manifestStream = File.Create(manifestPath);
        await JsonSerializer.SerializeAsync(manifestStream, manifest, JsonOptions, cancellationToken);

        string focusReportPath = Path.Combine(outputRoot, "actor-import-focus.json");
        string focusMarkdownPath = Path.Combine(outputRoot, "actor-import-focus.md");
        request.Progress?.Report(new ClientDataMiningProgress("WritingActorImportFocus", visitedFileCount, files.Count, focusReportPath));
        ClientDataActorImportFocusReport focusReport = CreateActorImportFocusReport(manifest);
        await WriteActorImportFocusReportAsync(focusReportPath, focusMarkdownPath, focusReport, cancellationToken)
            .ConfigureAwait(false);

        request.Progress?.Report(new ClientDataMiningProgress("Complete", visitedFileCount, files.Count, manifestPath));

        return new ClientDataMiningResult(true, manifestPath, files.Count, warnings, null, focusReportPath);
    }

    private static async Task<ClientDataFileRecord> CreateFileRecordAsync(
        string clientRoot,
        string filePath,
        ClientDataKind kind,
        ClientDataMiningRequest request,
        CancellationToken cancellationToken)
    {
        FileInfo file = new(filePath);
        ClientDataExtractionMode mode = ClientDataClassifier.GetExtractionMode(kind, request.IncludeStringProbes);
        ClientDataHeaderProbe headerProbe = await ClientDataHeaderProbeExtractor.ExtractAsync(filePath, cancellationToken);
        ClientDataResourceProbe? resourceProbe = mode == ClientDataExtractionMode.ResourceHeaderProbe
            ? await ClientDataResourceProbeExtractor.ExtractAsync(filePath, file.Length, cancellationToken)
            : null;
        IReadOnlyList<ClientDataStringProbe> stringProbes = mode == ClientDataExtractionMode.StringProbe
            ? await ClientDataStringProbeExtractor.ExtractAsync(
                filePath,
                request.MinStringLength,
                request.MaxStringProbesPerFile,
                request.MaxProbeBytesPerFile,
                cancellationToken)
            : [];

        return new ClientDataFileRecord(
            ClientDataClassifier.NormalizeRelativePath(clientRoot, filePath),
            kind,
            mode,
            file.Length,
            file.LastWriteTimeUtc,
            await ComputeSha256Async(filePath, cancellationToken),
            headerProbe,
            resourceProbe,
            stringProbes);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool ShouldReportCandidateProgress(int candidateFileCount)
    {
        return candidateFileCount <= 10 || candidateFileCount % 1_000 == 0;
    }

    private static ClientDataManifestSummary CreateSummary(IReadOnlyList<ClientDataFileRecord> files)
    {
        return new ClientDataManifestSummary(
            files.Count,
            files.Sum(file => file.SizeBytes),
            files.Count(file => file.ResourceProbe is not null),
            files.Count(file => file.ResourceProbe?.DeclaredSizeMatchesFileSize == false),
            files.Count(file => file.ResourceProbe?.LayoutProbe is not null),
            files.Count(file => file.ResourceProbe?.Observations.Count > 0),
            files.Sum(file => file.ResourceProbe?.Observations.Count ?? 0),
            CountBy(files, file => file.Kind.ToString()),
            CountBy(files, file => file.HeaderProbe.HeaderKind.ToString()),
            CountBy(files, file => file.ResourceProbe?.Family.ToString() ?? "NoResourceProbe"),
            SummarizeObservations(files),
            SummarizeLayouts(files),
            SummarizeLayoutSlots(files),
            SummarizeLayoutSlotValues(files),
            SummarizeSections(files),
            SummarizeActorImportFocus(files));
    }

    private static IReadOnlyList<ClientDataSummaryCount> CountBy(
        IReadOnlyList<ClientDataFileRecord> files,
        Func<ClientDataFileRecord, string> keySelector)
    {
        return files
            .GroupBy(keySelector, StringComparer.Ordinal)
            .Select(group => new ClientDataSummaryCount(
                group.Key,
                group.Count(),
                group.Sum(file => file.SizeBytes)))
            .OrderByDescending(count => count.Count)
            .ThenBy(count => count.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ClientDataObservationSummary> SummarizeObservations(IReadOnlyList<ClientDataFileRecord> files)
    {
        return files
            .SelectMany(file => file.ResourceProbe?.Observations ?? [])
            .GroupBy(observation => new { observation.Name, observation.Status })
            .Select(group => new ClientDataObservationSummary(
                group.Key.Name,
                group.Key.Status,
                group.Count(),
                group.Count(observation => observation.UnsignedLittleEndianValue is not null and not 0),
                group.Select(observation => observation.RawHex).Distinct(StringComparer.Ordinal).Count(),
                group
                    .GroupBy(observation => new { observation.RawHex, observation.UnsignedLittleEndianValue })
                    .Select(valueGroup => new ClientDataObservationValueCount(
                        valueGroup.Key.RawHex,
                        valueGroup.Key.UnsignedLittleEndianValue,
                        valueGroup.Count()))
                    .OrderByDescending(value => value.Count)
                    .ThenBy(value => value.RawHex, StringComparer.Ordinal)
                    .Take(10)
                    .ToArray()))
            .OrderBy(summary => summary.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ClientDataLayoutSummary> SummarizeLayouts(IReadOnlyList<ClientDataFileRecord> files)
    {
        return files
            .Select(file => file.ResourceProbe?.LayoutProbe)
            .OfType<ClientDataResourceLayoutProbe>()
            .GroupBy(probe => new { probe.Name, probe.Status })
            .Select(group => new ClientDataLayoutSummary(
                group.Key.Name,
                group.Key.Status,
                group.Count(),
                group.Min(probe => probe.CandidateOffsetCount),
                group.Max(probe => probe.CandidateOffsetCount),
                group.Count(probe => probe.InvalidNonZeroOffsetCandidateCount > 0)))
            .OrderBy(summary => summary.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ClientDataLayoutSlotSummary> SummarizeLayoutSlots(IReadOnlyList<ClientDataFileRecord> files)
    {
        List<ClientDataLayoutSlotSummary> summaries = [];
        foreach (IGrouping<string, ClientDataResourceLayoutProbe> group in files
            .Select(file => file.ResourceProbe?.LayoutProbe)
            .OfType<ClientDataResourceLayoutProbe>()
            .GroupBy(probe => probe.Name, StringComparer.Ordinal))
        {
            ClientDataResourceLayoutProbe first = group.First();
            for (int slotOffset = first.ScanStartOffsetBytes; slotOffset + 4 <= first.ScanEndOffsetBytes; slotOffset += 4)
            {
                int candidateCount = group.Count(probe => probe.CandidateSlotOffsetsBytes.Contains(slotOffset));
                int invalidCount = group.Count(probe => probe.InvalidNonZeroSlotOffsetsBytes.Contains(slotOffset));
                int emptyOrZeroCount = group.Count() - candidateCount - invalidCount;
                summaries.Add(new ClientDataLayoutSlotSummary(
                    group.Key,
                    slotOffset,
                    ClassifyLayoutSlotPattern(group.Count(), candidateCount, invalidCount, emptyOrZeroCount),
                    candidateCount,
                    invalidCount,
                    emptyOrZeroCount));
            }
        }

        return summaries
            .OrderBy(summary => summary.LayoutName, StringComparer.Ordinal)
            .ThenBy(summary => summary.SlotOffsetBytes)
            .ToArray();
    }

    private static ClientDataLayoutSlotPattern ClassifyLayoutSlotPattern(
        int totalCount,
        int candidateCount,
        int invalidNonZeroCount,
        int emptyOrZeroCount)
    {
        if (totalCount == 0)
            return ClientDataLayoutSlotPattern.Empty;

        if (candidateCount == totalCount)
            return ClientDataLayoutSlotPattern.AlwaysCandidateOffset;

        if (emptyOrZeroCount == totalCount)
            return ClientDataLayoutSlotPattern.AlwaysZero;

        if (candidateCount > 0 && invalidNonZeroCount == 0)
            return ClientDataLayoutSlotPattern.OptionalCandidateOffsetOrZero;

        if (candidateCount > 0 && invalidNonZeroCount > 0)
            return ClientDataLayoutSlotPattern.MixedCandidateOffsetAndScalar;

        if (invalidNonZeroCount > 0 && emptyOrZeroCount > 0)
            return ClientDataLayoutSlotPattern.MixedScalarOrZero;

        return ClientDataLayoutSlotPattern.SparseCandidateOffset;
    }

    private static IReadOnlyList<ClientDataLayoutSlotValueSummary> SummarizeLayoutSlotValues(IReadOnlyList<ClientDataFileRecord> files)
    {
        return files
            .SelectMany(file => file.ResourceProbe?.LayoutProbe?.SlotProbes.Select(slot => new
            {
                LayoutName = file.ResourceProbe.LayoutProbe.Name,
                Slot = slot
            }) ?? [])
            .GroupBy(item => new { item.LayoutName, item.Slot.SlotOffsetBytes, item.Slot.Classification })
            .Select(group => new ClientDataLayoutSlotValueSummary(
                group.Key.LayoutName,
                group.Key.SlotOffsetBytes,
                group.Key.Classification,
                group.Count(),
                group.Select(item => item.Slot.RawHex).Distinct(StringComparer.Ordinal).Count(),
                group
                    .GroupBy(item => new { item.Slot.RawHex, item.Slot.UnsignedLittleEndianValue })
                    .Select(valueGroup => new ClientDataObservationValueCount(
                        valueGroup.Key.RawHex,
                        valueGroup.Key.UnsignedLittleEndianValue,
                        valueGroup.Count()))
                    .OrderByDescending(value => value.Count)
                    .ThenBy(value => value.RawHex, StringComparer.Ordinal)
                    .Take(10)
                    .ToArray()))
            .OrderBy(summary => summary.LayoutName, StringComparer.Ordinal)
            .ThenBy(summary => summary.SlotOffsetBytes)
            .ThenBy(summary => summary.Classification)
            .ToArray();
    }

    private static IReadOnlyList<ClientDataSectionSummary> SummarizeSections(IReadOnlyList<ClientDataFileRecord> files)
    {
        return files
            .SelectMany(file => file.ResourceProbe?.LayoutProbe?.SectionProbes.Select(section => new
            {
                file.RelativePath,
                LayoutName = file.ResourceProbe.LayoutProbe.Name,
                Section = section
            }) ?? [])
            .GroupBy(item => new { item.LayoutName, item.Section.SourceSlotOffsetBytes, item.Section.Status })
            .Select(group =>
            {
                int[] lengths = group
                    .Select(item => item.Section.LengthBytes)
                    .OfType<int>()
                    .ToArray();
                return new ClientDataSectionSummary(
                    group.Key.LayoutName,
                    group.Key.SourceSlotOffsetBytes,
                    group.Key.Status,
                    group.Count(),
                    group.Count(item => item.Section.PrefixKind != ClientDataSectionPrefixKind.PrefixUnavailable),
                    group
                        .Where(item => item.Section.PrefixKind != ClientDataSectionPrefixKind.PrefixUnavailable)
                        .Select(item => item.Section.PrefixHex)
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                    lengths.Length == 0 ? 0 : lengths.Min(),
                    lengths.Length == 0 ? 0 : lengths.Max(),
                    group
                        .GroupBy(item => item.Section.PrefixKind)
                        .Select(prefixKindGroup => new ClientDataSectionPrefixKindCount(
                            prefixKindGroup.Key,
                            prefixKindGroup.Count()))
                        .OrderByDescending(count => count.Count)
                        .ThenBy(count => count.Kind)
                        .ToArray(),
                    group
                        .Where(item => item.Section.FirstUInt32LittleEndian is not null)
                        .GroupBy(item => item.Section.FirstUInt32LittleEndian!.Value)
                        .Select(valueGroup => new ClientDataObservationValueCount(
                            ToLittleEndianHex(valueGroup.Key),
                            valueGroup.Key,
                            valueGroup.Count()))
                        .OrderByDescending(value => value.Count)
                        .ThenBy(value => value.RawHex, StringComparer.Ordinal)
                        .Take(10)
                        .ToArray(),
                    group
                        .Where(item => item.Section.PrefixKind != ClientDataSectionPrefixKind.PrefixUnavailable)
                        .GroupBy(item => new { item.Section.PrefixHex, item.Section.PrefixKind })
                        .Select(prefixGroup => new ClientDataSectionPrefixValueCount(
                            prefixGroup.Key.PrefixHex,
                            prefixGroup.Key.PrefixKind,
                            prefixGroup.Count(),
                            prefixGroup
                                .Select(item => item.RelativePath)
                                .Order(StringComparer.Ordinal)
                                .Take(5)
                                .ToArray()))
                        .OrderByDescending(value => value.Count)
                        .ThenBy(value => value.PrefixHex, StringComparer.Ordinal)
                        .Take(10)
                        .ToArray(),
                    group
                        .GroupBy(item => item.Section.WordShape, StringComparer.Ordinal)
                        .Select(shapeGroup => new ClientDataSectionWordShapeCount(
                            shapeGroup.Key,
                            shapeGroup.Count(),
                            shapeGroup
                                .Select(item => item.RelativePath)
                                .Order(StringComparer.Ordinal)
                                .Take(5)
                                .ToArray()))
                        .OrderByDescending(value => value.Count)
                        .ThenBy(value => value.Shape, StringComparer.Ordinal)
                        .Take(10)
                        .ToArray());
            })
            .OrderBy(summary => summary.LayoutName, StringComparer.Ordinal)
            .ThenBy(summary => summary.SourceSlotOffsetBytes)
            .ToArray();
    }

    private static IReadOnlyList<ClientDataActorImportFocusSummary> SummarizeActorImportFocus(
        IReadOnlyList<ClientDataFileRecord> files)
    {
        return ActorImportFocusRule.Rules
            .Select(rule => CreateActorImportFocusSummary(files, rule))
            .Where(summary => summary.FileCount > 0 || summary.StringProbeHitCount > 0)
            .OrderByDescending(summary => summary.FileCount + summary.StringProbeHitCount)
            .ThenBy(summary => summary.Focus, StringComparer.Ordinal)
            .ToArray();
    }

    private static ClientDataActorImportFocusSummary CreateActorImportFocusSummary(
        IReadOnlyList<ClientDataFileRecord> files,
        ActorImportFocusRule rule)
    {
        ClientDataFileRecord[] matchingFiles = files
            .Where(file => rule.MatchesPath(file.RelativePath) || file.StringProbes.Any(probe => rule.MatchesString(probe.Value)))
            .ToArray();
        string[] matchingStrings = matchingFiles
            .SelectMany(file => file.StringProbes)
            .Where(probe => rule.MatchesString(probe.Value))
            .Select(probe => probe.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToArray();

        return new ClientDataActorImportFocusSummary(
            rule.Name,
            matchingFiles.Length,
            matchingFiles.Sum(file => file.StringProbes.Count(probe => rule.MatchesString(probe.Value))),
            matchingFiles
                .Select(file => file.RelativePath)
                .Order(StringComparer.Ordinal)
                .Take(12)
                .ToArray(),
            matchingStrings);
    }

    private static ClientDataActorImportFocusReport CreateActorImportFocusReport(ClientDataManifest manifest)
    {
        return new ClientDataActorImportFocusReport(
            SchemaVersion,
            manifest.GeneratedAtUtc,
            manifest.ClientRootPath,
            manifest.OutputRootPath,
            "Client mining output is discovery evidence only. Use it to reconcile or enrich repo-confirmed legacy v1/v1 SQL actor data; do not promote mined strings, offsets, or anchors to canonical server behavior without a separate source or fixture.",
            manifest.Summary.ActorImportFocusSummaries,
            [
                "Compare actor/chara and layout-placement focus files against imported v1 actor class and spawn rows.",
                "Use zone/map/event focus hits to find missing anchors or script descriptor context.",
                "Promote a mined value only after recording provenance and adding a compatibility fixture."
            ]);
    }

    private static async Task WriteActorImportFocusReportAsync(
        string jsonPath,
        string markdownPath,
        ClientDataActorImportFocusReport report,
        CancellationToken cancellationToken)
    {
        await using (FileStream stream = File.Create(jsonPath))
            await JsonSerializer.SerializeAsync(stream, report, JsonOptions, cancellationToken).ConfigureAwait(false);

        await File.WriteAllTextAsync(markdownPath, CreateActorImportFocusMarkdown(report), cancellationToken)
            .ConfigureAwait(false);
    }

    private static string CreateActorImportFocusMarkdown(ClientDataActorImportFocusReport report)
    {
        StringBuilder builder = new();
        builder.AppendLine("# AetherXIV Actor Import Focus");
        builder.AppendLine();
        builder.AppendLine(report.EvidencePolicy);
        builder.AppendLine();
        builder.AppendLine($"Client root: `{report.ClientRootPath}`");
        builder.AppendLine($"Generated UTC: `{report.GeneratedAtUtc:O}`");
        builder.AppendLine();
        builder.AppendLine("## Focus Summaries");
        builder.AppendLine();

        foreach (ClientDataActorImportFocusSummary summary in report.FocusSummaries)
        {
            builder.AppendLine($"### {summary.Focus}");
            builder.AppendLine();
            builder.AppendLine($"- Files: {summary.FileCount}");
            builder.AppendLine($"- String hits: {summary.StringProbeHitCount}");
            AppendList(builder, "Sample files", summary.SampleRelativePaths);
            AppendList(builder, "Sample strings", summary.SampleStrings);
            builder.AppendLine();
        }

        builder.AppendLine("## Recommended Next Steps");
        builder.AppendLine();
        foreach (string step in report.RecommendedNextSteps)
            builder.AppendLine($"- {step}");

        return builder.ToString();
    }

    private static void AppendList(StringBuilder builder, string label, IReadOnlyList<string> values)
    {
        builder.AppendLine($"- {label}:");
        if (values.Count == 0)
        {
            builder.AppendLine("  - none");
            return;
        }

        foreach (string value in values.Take(12))
            builder.AppendLine($"  - `{value}`");
    }

    private static string ToLittleEndianHex(uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed record ActorImportFocusRule(string Name, IReadOnlyList<string> Tokens)
    {
        public static IReadOnlyList<ActorImportFocusRule> Rules { get; } =
        [
            new("ActorClassOrChara", ["actor", "chara", "npc", "populace", "battle"]),
            new("LayoutPlacement", ["layout", "geb", "bgobj", "instance"]),
            new("ZoneMapAnchor", ["zone", "map", "territory", "area", "anchor"]),
            new("EventHook", ["event", "notice", "talk", "push", "emote"]),
            new("TextName", ["name", "title", "gmd"])
        ];

        public bool MatchesPath(string value)
        {
            return Tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        public bool MatchesString(string value)
        {
            return Tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string rootPath, List<string> warnings)
    {
        Stack<string> pending = new();
        pending.Push(rootPath);

        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            IReadOnlyList<string> files = TryEnumerateFiles(directory, warnings);
            foreach (string file in files)
                yield return file;

            IReadOnlyList<string> directories = TryEnumerateDirectories(directory, warnings);
            for (int i = directories.Count - 1; i >= 0; i--)
                pending.Push(directories[i]);
        }
    }

    private static IReadOnlyList<string> TryEnumerateFiles(string directory, List<string> warnings)
    {
        try
        {
            return Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal).ToArray();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            warnings.Add($"Could not enumerate files in '{directory}': {ex.Message}");
            return [];
        }
    }

    private static IReadOnlyList<string> TryEnumerateDirectories(string directory, List<string> warnings)
    {
        try
        {
            return Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal).ToArray();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or DirectoryNotFoundException)
        {
            warnings.Add($"Could not enumerate directories in '{directory}': {ex.Message}");
            return [];
        }
    }
}
