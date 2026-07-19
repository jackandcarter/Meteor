using System.Text.Json.Serialization;

namespace AetherXIV.ClientData;

public enum ClientDataKind
{
    LooseGmd,
    LooseGeb,
    SqpackIndex,
    SqpackData,
    PackedDatResource,
    SqpackOther,
    UnknownCandidate
}

public enum ClientDataExtractionMode
{
    FileInventory,
    StringProbe,
    ArchiveCatalogOnly,
    ResourceHeaderProbe
}

public enum ClientDataHeaderKind
{
    Empty,
    ResourceMagic,
    MostlyZero,
    HighBitPacked,
    Binary
}

public enum ClientDataResourceFamily
{
    Unknown,
    SedbSscf,
    Gtex,
    VersWrappedGtex
}

public enum ClientDataFieldObservationStatus
{
    ObservedUnproven
}

public enum ClientDataLayoutSlotClassification
{
    CandidateOffset,
    InvalidNonZero
}

public enum ClientDataLayoutSlotPattern
{
    AlwaysCandidateOffset,
    OptionalCandidateOffsetOrZero,
    AlwaysZero,
    MixedCandidateOffsetAndScalar,
    MixedScalarOrZero,
    SparseCandidateOffset,
    Empty
}

public enum ClientDataSectionPrefixKind
{
    PrefixUnavailable,
    Empty,
    MostlyZero,
    FloatLike,
    SmallIntegers,
    OffsetLike,
    Binary
}

public sealed record ClientDataMiningRequest(
    string ClientRootPath,
    string OutputRootPath,
    bool IncludeStringProbes = true,
    int MinStringLength = 4,
    int MaxStringProbesPerFile = 128,
    long MaxProbeBytesPerFile = 1_048_576,
    IProgress<ClientDataMiningProgress>? Progress = null);

public sealed record ClientDataMiningProgress(
    string Phase,
    int VisitedFileCount,
    int CandidateFileCount,
    string? CurrentPath);

public sealed record ClientDataMiningResult(
    bool Success,
    string? ManifestPath,
    int CandidateFileCount,
    IReadOnlyList<string> Warnings,
    string? Error,
    string? ActorImportFocusReportPath = null)
{
    public static ClientDataMiningResult Failed(string error)
    {
        return new(false, null, 0, [], error);
    }
}

public sealed record ClientDataManifest(
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string ClientRootPath,
    string OutputRootPath,
    ClientDataManifestSummary Summary,
    IReadOnlyList<ClientDataFileRecord> Files,
    IReadOnlyList<string> Warnings);

public sealed record ClientDataManifestSummary(
    int TotalFileCount,
    long TotalSizeBytes,
    int ResourceProbeCount,
    int DeclaredSizeMismatchCount,
    int LayoutProbeCount,
    int FilesWithUnprovenObservationsCount,
    int UnprovenObservationCount,
    IReadOnlyList<ClientDataSummaryCount> KindCounts,
    IReadOnlyList<ClientDataSummaryCount> HeaderKindCounts,
    IReadOnlyList<ClientDataSummaryCount> ResourceFamilyCounts,
    IReadOnlyList<ClientDataObservationSummary> ObservationSummaries,
    IReadOnlyList<ClientDataLayoutSummary> LayoutSummaries,
    IReadOnlyList<ClientDataLayoutSlotSummary> LayoutSlotSummaries,
    IReadOnlyList<ClientDataLayoutSlotValueSummary> LayoutSlotValueSummaries,
    IReadOnlyList<ClientDataSectionSummary> SectionSummaries,
    IReadOnlyList<ClientDataActorImportFocusSummary> ActorImportFocusSummaries);

public sealed record ClientDataActorImportFocusSummary(
    string Focus,
    int FileCount,
    int StringProbeHitCount,
    IReadOnlyList<string> SampleRelativePaths,
    IReadOnlyList<string> SampleStrings);

public sealed record ClientDataActorImportFocusReport(
    int SchemaVersion,
    DateTimeOffset GeneratedAtUtc,
    string ClientRootPath,
    string OutputRootPath,
    string EvidencePolicy,
    IReadOnlyList<ClientDataActorImportFocusSummary> FocusSummaries,
    IReadOnlyList<string> RecommendedNextSteps);

public sealed record ClientDataSummaryCount(
    string Name,
    int Count,
    long TotalSizeBytes);

public sealed record ClientDataObservationSummary(
    string Name,
    ClientDataFieldObservationStatus Status,
    int Count,
    int NonZeroCount,
    int DistinctRawValueCount,
    IReadOnlyList<ClientDataObservationValueCount> TopValues);

public sealed record ClientDataObservationValueCount(
    string RawHex,
    long? UnsignedLittleEndianValue,
    int Count);

public sealed record ClientDataLayoutSummary(
    string Name,
    ClientDataFieldObservationStatus Status,
    int Count,
    int MinCandidateOffsetCount,
    int MaxCandidateOffsetCount,
    int FilesWithInvalidNonZeroOffsetCandidatesCount);

public sealed record ClientDataLayoutSlotSummary(
    string LayoutName,
    int SlotOffsetBytes,
    ClientDataLayoutSlotPattern Pattern,
    int CandidateCount,
    int InvalidNonZeroCount,
    int EmptyOrZeroCount);

public sealed record ClientDataLayoutSlotValueSummary(
    string LayoutName,
    int SlotOffsetBytes,
    ClientDataLayoutSlotClassification Classification,
    int NonZeroCount,
    int DistinctRawValueCount,
    IReadOnlyList<ClientDataObservationValueCount> TopValues);

public sealed record ClientDataSectionSummary(
    string LayoutName,
    int SourceSlotOffsetBytes,
    ClientDataFieldObservationStatus Status,
    int SectionCount,
    int PrefixAvailableCount,
    int DistinctPrefixCount,
    int MinLengthBytes,
    int MaxLengthBytes,
    IReadOnlyList<ClientDataSectionPrefixKindCount> PrefixKindCounts,
    IReadOnlyList<ClientDataObservationValueCount> TopFirstUInt32Values,
    IReadOnlyList<ClientDataSectionPrefixValueCount> TopPrefixes,
    IReadOnlyList<ClientDataSectionWordShapeCount> TopWordShapes);

public sealed record ClientDataSectionPrefixKindCount(
    ClientDataSectionPrefixKind Kind,
    int Count);

public sealed record ClientDataSectionPrefixValueCount(
    string PrefixHex,
    ClientDataSectionPrefixKind PrefixKind,
    int Count,
    IReadOnlyList<string> SampleRelativePaths);

public sealed record ClientDataSectionWordShapeCount(
    string Shape,
    int Count,
    IReadOnlyList<string> SampleRelativePaths);

public sealed record ClientDataFileRecord(
    string RelativePath,
    ClientDataKind Kind,
    ClientDataExtractionMode ExtractionMode,
    long SizeBytes,
    DateTimeOffset LastWriteUtc,
    string Sha256,
    ClientDataHeaderProbe HeaderProbe,
    ClientDataResourceProbe? ResourceProbe,
    IReadOnlyList<ClientDataStringProbe> StringProbes);

public sealed record ClientDataStringProbe(
    long Offset,
    string Encoding,
    string Value);

public sealed record ClientDataHeaderProbe(
    string HexPrefix,
    ClientDataHeaderKind HeaderKind,
    IReadOnlyList<string> MagicCandidates);

public sealed record ClientDataResourceProbe(
    ClientDataResourceFamily Family,
    string? ContainerMagic,
    string? InnerMagic,
    int? Version,
    long? DeclaredSizeBytes,
    bool? DeclaredSizeMatchesFileSize,
    int? PayloadOffsetBytes,
    ClientDataResourceLayoutProbe? LayoutProbe,
    IReadOnlyList<ClientDataFieldObservation> Observations);

public sealed record ClientDataResourceLayoutProbe(
    string Name,
    ClientDataFieldObservationStatus Status,
    int ScanStartOffsetBytes,
    int ScanEndOffsetBytes,
    int CandidateOffsetCount,
    int InvalidNonZeroOffsetCandidateCount,
    int? FirstCandidateOffsetBytes,
    int? LastCandidateOffsetBytes,
    IReadOnlyList<int> CandidateSlotOffsetsBytes,
    IReadOnlyList<int> InvalidNonZeroSlotOffsetsBytes,
    IReadOnlyList<int> SampleCandidateOffsetsBytes,
    string Note)
{
    [JsonIgnore]
    public IReadOnlyList<ClientDataLayoutSlotProbe> SlotProbes { get; init; } = [];

    [JsonIgnore]
    public IReadOnlyList<ClientDataSectionProbe> SectionProbes { get; init; } = [];
}

public sealed record ClientDataLayoutSlotProbe(
    int SlotOffsetBytes,
    ClientDataLayoutSlotClassification Classification,
    string RawHex,
    long UnsignedLittleEndianValue);

public sealed record ClientDataSectionProbe(
    int SourceSlotOffsetBytes,
    int SectionOffsetBytes,
    int? NextSectionOffsetBytes,
    int? LengthBytes,
    string PrefixHex,
    uint? FirstUInt32LittleEndian,
    string WordShape,
    ClientDataSectionPrefixKind PrefixKind,
    ClientDataFieldObservationStatus Status,
    string Note);

public sealed record ClientDataFieldObservation(
    string Name,
    int OffsetBytes,
    int LengthBytes,
    string RawHex,
    long? UnsignedLittleEndianValue,
    ClientDataFieldObservationStatus Status,
    string Note);
