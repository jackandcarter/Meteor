using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AetherXIV.ClientData;

namespace AetherXIV.ClientData.Tests;

public sealed class ClientDataMinerTests
{
    [Fact]
    public async Task MinerWritesManifestForLooseAndSqpackCandidates()
    {
        string root = CreateTempDirectory();
        string output = CreateTempDirectory();

        try
        {
            string gmdPath = Path.Combine(root, "game", "data", "zone", "uldah.gmd");
            string gebPath = Path.Combine(root, "game", "data", "layout", "uldah.geb");
            string indexPath = Path.Combine(root, "game", "sqpack", "ffxiv", "0a0000.win32.index");
            string datPath = Path.Combine(root, "game", "sqpack", "ffxiv", "0a0000.win32.dat0");
            string packedDatPath = Path.Combine(root, "game", "data", "03", "5B", "00", "00.DAT");
            Directory.CreateDirectory(Path.GetDirectoryName(gmdPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(gebPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(packedDatPath)!);

            byte[] gmdBytes = Encoding.ASCII.GetBytes("zone_actor_anchor_ul_merchant\0");
            byte[] gebBytes = Encoding.Unicode.GetBytes("layout_anchor_market");
            byte[] indexBytes = [0x53, 0x51, 0x50, 0x41, 0x43, 0x4B];
            byte[] datBytes = [0x00, 0x01, 0x02, 0x03];
            byte[] packedDatBytes = CreateSedbSscfBytes(512, offsets: [0x60]);
            await File.WriteAllBytesAsync(gmdPath, gmdBytes);
            await File.WriteAllBytesAsync(gebPath, gebBytes);
            await File.WriteAllBytesAsync(indexPath, indexBytes);
            await File.WriteAllBytesAsync(datPath, datBytes);
            await File.WriteAllBytesAsync(packedDatPath, packedDatBytes);
            await File.WriteAllTextAsync(Path.Combine(root, "readme.txt"), "not a candidate");

            ClientDataMiner miner = new();
            ClientDataMiningResult result = await miner.MineAsync(new ClientDataMiningRequest(root, output));

            Assert.True(result.Success, result.Error);
            Assert.Equal(5, result.CandidateFileCount);
            Assert.NotNull(result.ManifestPath);
            Assert.True(File.Exists(result.ManifestPath));
            Assert.NotNull(result.ActorImportFocusReportPath);
            Assert.True(File.Exists(result.ActorImportFocusReportPath));
            string focusMarkdownPath = Path.Combine(output, "actor-import-focus.md");
            Assert.True(File.Exists(focusMarkdownPath));

            ClientDataManifest manifest = await ReadManifestAsync(result.ManifestPath);
            ClientDataActorImportFocusReport focusReport = await ReadActorImportFocusReportAsync(result.ActorImportFocusReportPath);
            string focusMarkdown = await File.ReadAllTextAsync(focusMarkdownPath);

            Assert.Equal(13, manifest.SchemaVersion);
            Assert.Equal(Path.GetFullPath(root), manifest.ClientRootPath);
            Assert.Equal(Path.GetFullPath(output), manifest.OutputRootPath);
            Assert.Equal(13, focusReport.SchemaVersion);
            Assert.Contains("discovery evidence only", focusReport.EvidencePolicy);
            Assert.Contains("AetherXIV Actor Import Focus", focusMarkdown);
            Assert.Contains("LayoutPlacement", focusMarkdown);
            Assert.Empty(manifest.Warnings);
            Assert.Equal(5, manifest.Summary.TotalFileCount);
            Assert.Equal(gmdBytes.Length + gebBytes.Length + indexBytes.Length + datBytes.Length + packedDatBytes.Length, manifest.Summary.TotalSizeBytes);
            Assert.Equal(1, Assert.Single(manifest.Summary.KindCounts, count => count.Name == "PackedDatResource").Count);
            Assert.Equal(1, Assert.Single(manifest.Summary.ResourceFamilyCounts, count => count.Name == "SedbSscf").Count);
            Assert.Equal(1, manifest.Summary.ResourceProbeCount);
            Assert.Equal(0, manifest.Summary.DeclaredSizeMismatchCount);
            Assert.Equal(1, manifest.Summary.LayoutProbeCount);
            Assert.Equal(1, manifest.Summary.FilesWithUnprovenObservationsCount);
            Assert.Equal(4, manifest.Summary.UnprovenObservationCount);
            Assert.NotEmpty(manifest.Summary.ActorImportFocusSummaries);
            ClientDataActorImportFocusSummary layoutFocus = Assert.Single(
                manifest.Summary.ActorImportFocusSummaries,
                summary => summary.Focus == "LayoutPlacement");
            Assert.Contains("game/data/layout/uldah.geb", layoutFocus.SampleRelativePaths);
            Assert.Contains("layout_anchor_market", layoutFocus.SampleStrings);
            ClientDataActorImportFocusSummary actorFocus = Assert.Single(
                manifest.Summary.ActorImportFocusSummaries,
                summary => summary.Focus == "ActorClassOrChara");
            Assert.Contains("zone_actor_anchor_ul_merchant", actorFocus.SampleStrings);
            Assert.Contains(focusReport.FocusSummaries, summary => summary.Focus == "ActorClassOrChara");
            Assert.Contains(focusReport.FocusSummaries, summary => summary.Focus == "LayoutPlacement");
            ClientDataLayoutSummary layoutSummary = Assert.Single(manifest.Summary.LayoutSummaries);
            Assert.Equal("SedbSscf.EarlyAlignedOffsetCandidates", layoutSummary.Name);
            Assert.Equal(ClientDataFieldObservationStatus.ObservedUnproven, layoutSummary.Status);
            Assert.Equal(1, layoutSummary.Count);
            Assert.NotEmpty(manifest.Summary.LayoutSlotSummaries);
            Assert.NotEmpty(manifest.Summary.LayoutSlotValueSummaries);
            Assert.NotEmpty(manifest.Summary.SectionSummaries);
            ClientDataObservationSummary word0x0cSummary = Assert.Single(
                manifest.Summary.ObservationSummaries,
                summary => summary.Name == "SedbSscf.Word0x0C");
            Assert.Equal(ClientDataFieldObservationStatus.ObservedUnproven, word0x0cSummary.Status);
            Assert.Equal(1, word0x0cSummary.Count);
            Assert.Equal(1, word0x0cSummary.NonZeroCount);
            Assert.Equal(1, word0x0cSummary.DistinctRawValueCount);
            Assert.Equal("00043000", Assert.Single(word0x0cSummary.TopValues).RawHex);

            ClientDataFileRecord gmd = Assert.Single(manifest.Files, file => file.RelativePath == "game/data/zone/uldah.gmd");
            Assert.Equal(ClientDataKind.LooseGmd, gmd.Kind);
            Assert.Equal(ClientDataExtractionMode.StringProbe, gmd.ExtractionMode);
            Assert.Equal(ToSha256(gmdBytes), gmd.Sha256);
            Assert.Equal(ClientDataHeaderKind.Binary, gmd.HeaderProbe.HeaderKind);
            Assert.Contains(gmd.StringProbes, probe => probe.Value == "zone_actor_anchor_ul_merchant");

            ClientDataFileRecord geb = Assert.Single(manifest.Files, file => file.RelativePath == "game/data/layout/uldah.geb");
            Assert.Equal(ClientDataKind.LooseGeb, geb.Kind);
            Assert.Equal(Convert.ToHexString(gebBytes.AsSpan(0, 32)).ToLowerInvariant(), geb.HeaderProbe.HexPrefix);
            Assert.Contains(geb.StringProbes, probe => probe.Encoding == "utf-16le" && probe.Value == "layout_anchor_market");

            ClientDataFileRecord index = Assert.Single(manifest.Files, file => file.RelativePath == "game/sqpack/ffxiv/0a0000.win32.index");
            Assert.Equal(ClientDataKind.SqpackIndex, index.Kind);
            Assert.Equal(ClientDataExtractionMode.ArchiveCatalogOnly, index.ExtractionMode);
            Assert.Equal(ClientDataHeaderKind.ResourceMagic, index.HeaderProbe.HeaderKind);
            Assert.Contains("SQPA", index.HeaderProbe.MagicCandidates);
            Assert.Null(index.ResourceProbe);
            Assert.Empty(index.StringProbes);

            ClientDataFileRecord dat = Assert.Single(manifest.Files, file => file.RelativePath == "game/sqpack/ffxiv/0a0000.win32.dat0");
            Assert.Equal(ClientDataKind.SqpackData, dat.Kind);
            Assert.Equal(ClientDataExtractionMode.ArchiveCatalogOnly, dat.ExtractionMode);
            Assert.Equal(ClientDataHeaderKind.Binary, dat.HeaderProbe.HeaderKind);
            Assert.Null(dat.ResourceProbe);
            Assert.Equal(ToSha256(datBytes), dat.Sha256);

            ClientDataFileRecord packedDat = Assert.Single(manifest.Files, file => file.RelativePath == "game/data/03/5B/00/00.DAT");
            Assert.Equal(ClientDataKind.PackedDatResource, packedDat.Kind);
            Assert.Equal(ClientDataExtractionMode.ResourceHeaderProbe, packedDat.ExtractionMode);
            Assert.Equal(ClientDataHeaderKind.ResourceMagic, packedDat.HeaderProbe.HeaderKind);
            Assert.NotNull(packedDat.ResourceProbe);
            Assert.Equal(ClientDataResourceFamily.SedbSscf, packedDat.ResourceProbe.Family);
            Assert.Equal(512, packedDat.ResourceProbe.DeclaredSizeBytes);
            Assert.True(packedDat.ResourceProbe.DeclaredSizeMatchesFileSize);
            Assert.NotNull(packedDat.ResourceProbe.LayoutProbe);
            Assert.Equal(4, packedDat.ResourceProbe.Observations.Count);
        }
        finally
        {
            DeleteDirectory(root);
            DeleteDirectory(output);
        }
    }

    [Fact]
    public void ResourceProbeDetectsSedbSscfAndValidatesDeclaredSize()
    {
        byte[] bytes = CreateSedbSscfBytes(110_320);

        ClientDataResourceProbe? probe = ClientDataResourceProbeExtractor.Create(bytes, 110_320);

        Assert.NotNull(probe);
        Assert.Equal(ClientDataResourceFamily.SedbSscf, probe.Family);
        Assert.Equal("SEDB", probe.ContainerMagic);
        Assert.Equal("SSCF", probe.InnerMagic);
        Assert.Equal(3, probe.Version);
        Assert.Equal(110_320, probe.DeclaredSizeBytes);
        Assert.True(probe.DeclaredSizeMatchesFileSize);
        Assert.NotNull(probe.LayoutProbe);
        ClientDataFieldObservation headerWord = Assert.Single(probe.Observations, observation => observation.Name == "SedbSscf.Word0x0C");
        Assert.Equal(0x0c, headerWord.OffsetBytes);
        Assert.Equal("00043000", headerWord.RawHex);
        Assert.Equal(0x00300400, headerWord.UnsignedLittleEndianValue);
        Assert.Equal(ClientDataFieldObservationStatus.ObservedUnproven, headerWord.Status);
    }

    [Fact]
    public void SedbParserTracksVersionTwoUnknownWordsWithoutPromotingThemToFacts()
    {
        byte[] bytes = CreateSedbSscfBytes(555_600, version: 2, word0x18: 0x48646bba);

        ClientDataResourceProbe? probe = SedbSscfResourceParser.Parse(bytes, 555_600);

        Assert.NotNull(probe);
        Assert.Equal(2, probe.Version);
        Assert.True(probe.DeclaredSizeMatchesFileSize);
        ClientDataFieldObservation unknown = Assert.Single(probe.Observations, observation => observation.Name == "SedbSscf.Word0x18");
        Assert.Equal("ba6b6448", unknown.RawHex);
        Assert.Equal(0x48646bba, unknown.UnsignedLittleEndianValue);
        Assert.Equal(ClientDataFieldObservationStatus.ObservedUnproven, unknown.Status);
    }

    [Fact]
    public void SedbParserTracksEarlyAlignedOffsetCandidatesWithoutNamingTheTables()
    {
        byte[] bytes = CreateSedbSscfBytes(
            512,
            offsets: [0x60, 0x70, 0x80, 0x9999]);

        ClientDataResourceProbe? probe = SedbSscfResourceParser.Parse(bytes, 512);

        Assert.NotNull(probe);
        Assert.NotNull(probe.LayoutProbe);
        Assert.Equal("SedbSscf.EarlyAlignedOffsetCandidates", probe.LayoutProbe.Name);
        Assert.Equal(ClientDataFieldObservationStatus.ObservedUnproven, probe.LayoutProbe.Status);
        Assert.Equal(0x38, probe.LayoutProbe.ScanStartOffsetBytes);
        Assert.Equal(0x90, probe.LayoutProbe.ScanEndOffsetBytes);
        Assert.Equal(3, probe.LayoutProbe.CandidateOffsetCount);
        Assert.Equal(1, probe.LayoutProbe.InvalidNonZeroOffsetCandidateCount);
        Assert.Equal(0x60, probe.LayoutProbe.FirstCandidateOffsetBytes);
        Assert.Equal(0x80, probe.LayoutProbe.LastCandidateOffsetBytes);
        Assert.Equal([0x38, 0x3c, 0x40], probe.LayoutProbe.CandidateSlotOffsetsBytes);
        Assert.Equal([0x44], probe.LayoutProbe.InvalidNonZeroSlotOffsetsBytes);
        Assert.Equal([0x60, 0x70, 0x80], probe.LayoutProbe.SampleCandidateOffsetsBytes);
        Assert.Equal(4, probe.LayoutProbe.SlotProbes.Count);
    }

    [Fact]
    public void SedbParserProbesStableSlotSectionPrefixesWithoutNamingTables()
    {
        byte[] bytes = CreateSedbSscfBytes(
            512,
            offsets: [0x60, 0x80, 0x9999, 0, 0xA0]);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x60, 4), 5);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x64, 4), 7);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x80, 4), 0x3f800000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0xA0, 4), 0xC0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0xA4, 4), 0xD0);

        ClientDataResourceProbe? probe = SedbSscfResourceParser.Parse(bytes, 512);

        Assert.NotNull(probe);
        Assert.NotNull(probe.LayoutProbe);
        Assert.Equal(3, probe.LayoutProbe.SectionProbes.Count);

        ClientDataSectionProbe first = Assert.Single(
            probe.LayoutProbe.SectionProbes,
            section => section.SourceSlotOffsetBytes == 0x38);
        Assert.Equal(0x60, first.SectionOffsetBytes);
        Assert.Equal(0x80, first.NextSectionOffsetBytes);
        Assert.Equal(0x20, first.LengthBytes);
        Assert.Equal(5u, first.FirstUInt32LittleEndian);
        Assert.Equal("SSZZZZZZ", first.WordShape);
        Assert.Equal(ClientDataSectionPrefixKind.SmallIntegers, first.PrefixKind);
        Assert.Equal(ClientDataFieldObservationStatus.ObservedUnproven, first.Status);

        ClientDataSectionProbe second = Assert.Single(
            probe.LayoutProbe.SectionProbes,
            section => section.SourceSlotOffsetBytes == 0x3c);
        Assert.Equal(0x80, second.SectionOffsetBytes);
        Assert.Equal(ClientDataSectionPrefixKind.FloatLike, second.PrefixKind);

        ClientDataSectionProbe fifth = Assert.Single(
            probe.LayoutProbe.SectionProbes,
            section => section.SourceSlotOffsetBytes == 0x48);
        Assert.Equal(0xA0, fifth.SectionOffsetBytes);
        Assert.Equal(ClientDataSectionPrefixKind.OffsetLike, fifth.PrefixKind);
    }

    [Fact]
    public void SedbParserBoundsSectionPrefixesToKnownSectionLength()
    {
        byte[] bytes = CreateSedbSscfBytes(
            512,
            offsets: [0x60, 0x70, 0x80]);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x60, 4), 0x90);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x70, 4), 0x3f800000);

        ClientDataResourceProbe? probe = SedbSscfResourceParser.Parse(bytes, 512);

        Assert.NotNull(probe);
        Assert.NotNull(probe.LayoutProbe);
        ClientDataSectionProbe section = Assert.Single(
            probe.LayoutProbe.SectionProbes,
            candidate => candidate.SourceSlotOffsetBytes == 0x38);
        Assert.Equal(0x60, section.SectionOffsetBytes);
        Assert.Equal(0x70, section.NextSectionOffsetBytes);
        Assert.Equal(0x10, section.LengthBytes);
        Assert.Equal(32, section.PrefixHex.Length);
        Assert.Equal("OZZZ", section.WordShape);
        Assert.DoesNotContain("0000803f", section.PrefixHex, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManifestSummarizesLayoutSlotClassifications()
    {
        string root = CreateTempDirectory();
        string output = CreateTempDirectory();

        try
        {
            string firstPath = Path.Combine(root, "game", "data", "03", "5B", "00", "00.DAT");
            string secondPath = Path.Combine(root, "game", "data", "03", "5B", "00", "01.DAT");
            Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);

            await File.WriteAllBytesAsync(firstPath, CreateSedbSscfBytes(512, offsets: [0x60, 0x70]));
            await File.WriteAllBytesAsync(secondPath, CreateSedbSscfBytes(512, offsets: [0x60, 0x9999]));

            ClientDataMiner miner = new();
            ClientDataMiningResult result = await miner.MineAsync(new ClientDataMiningRequest(root, output));

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.ManifestPath);

            ClientDataManifest manifest = await ReadManifestAsync(result.ManifestPath);
            ClientDataLayoutSlotSummary slot0x38 = Assert.Single(
                manifest.Summary.LayoutSlotSummaries,
                summary => summary.SlotOffsetBytes == 0x38);
            Assert.Equal(2, slot0x38.CandidateCount);
            Assert.Equal(0, slot0x38.InvalidNonZeroCount);
            Assert.Equal(0, slot0x38.EmptyOrZeroCount);
            Assert.Equal(ClientDataLayoutSlotPattern.AlwaysCandidateOffset, slot0x38.Pattern);

            ClientDataLayoutSlotSummary slot0x3c = Assert.Single(
                manifest.Summary.LayoutSlotSummaries,
                summary => summary.SlotOffsetBytes == 0x3c);
            Assert.Equal(1, slot0x3c.CandidateCount);
            Assert.Equal(1, slot0x3c.InvalidNonZeroCount);
            Assert.Equal(0, slot0x3c.EmptyOrZeroCount);
            Assert.Equal(ClientDataLayoutSlotPattern.MixedCandidateOffsetAndScalar, slot0x3c.Pattern);

            ClientDataLayoutSlotValueSummary slot0x38Values = Assert.Single(
                manifest.Summary.LayoutSlotValueSummaries,
                summary => summary.SlotOffsetBytes == 0x38 && summary.Classification == ClientDataLayoutSlotClassification.CandidateOffset);
            Assert.Equal(2, slot0x38Values.NonZeroCount);
            Assert.Equal(1, slot0x38Values.DistinctRawValueCount);
            Assert.Equal("60000000", Assert.Single(slot0x38Values.TopValues).RawHex);

            ClientDataLayoutSlotValueSummary slot0x3cInvalidValues = Assert.Single(
                manifest.Summary.LayoutSlotValueSummaries,
                summary => summary.SlotOffsetBytes == 0x3c && summary.Classification == ClientDataLayoutSlotClassification.InvalidNonZero);
            Assert.Equal(1, slot0x3cInvalidValues.NonZeroCount);
            Assert.Equal("99990000", Assert.Single(slot0x3cInvalidValues.TopValues).RawHex);
        }
        finally
        {
            DeleteDirectory(root);
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task ManifestSummarizesSectionCandidateEvidence()
    {
        string root = CreateTempDirectory();
        string output = CreateTempDirectory();

        try
        {
            string firstPath = Path.Combine(root, "game", "data", "03", "5B", "00", "00.DAT");
            string secondPath = Path.Combine(root, "game", "data", "03", "5B", "00", "01.DAT");
            Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);

            byte[] first = CreateSedbSscfBytes(512, offsets: [0x60, 0x80]);
            BinaryPrimitives.WriteUInt32LittleEndian(first.AsSpan(0x60, 4), 5);
            BinaryPrimitives.WriteUInt32LittleEndian(first.AsSpan(0x80, 4), 0x3f800000);
            byte[] second = CreateSedbSscfBytes(512, offsets: [0x60, 0x80]);
            BinaryPrimitives.WriteUInt32LittleEndian(second.AsSpan(0x60, 4), 5);
            BinaryPrimitives.WriteUInt32LittleEndian(second.AsSpan(0x80, 4), 9);

            await File.WriteAllBytesAsync(firstPath, first);
            await File.WriteAllBytesAsync(secondPath, second);

            ClientDataMiner miner = new();
            ClientDataMiningResult result = await miner.MineAsync(new ClientDataMiningRequest(root, output));

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.ManifestPath);

            ClientDataManifest manifest = await ReadManifestAsync(result.ManifestPath);
            ClientDataSectionSummary slot0x38 = Assert.Single(
                manifest.Summary.SectionSummaries,
                summary => summary.SourceSlotOffsetBytes == 0x38);
            Assert.Equal(2, slot0x38.SectionCount);
            Assert.Equal(2, slot0x38.PrefixAvailableCount);
            Assert.Equal(1, slot0x38.DistinctPrefixCount);
            Assert.Equal(0x20, slot0x38.MinLengthBytes);
            Assert.Equal(0x20, slot0x38.MaxLengthBytes);
            Assert.Contains(slot0x38.PrefixKindCounts, count => count.Kind == ClientDataSectionPrefixKind.SmallIntegers && count.Count == 2);
            Assert.Contains(slot0x38.TopFirstUInt32Values, value => value.RawHex == "05000000" && value.Count == 2);
            ClientDataSectionWordShapeCount slot0x38Shape = Assert.Single(slot0x38.TopWordShapes);
            Assert.Equal("SZZZZZZZ", slot0x38Shape.Shape);
            Assert.Equal(2, slot0x38Shape.Count);
            ClientDataSectionPrefixValueCount slot0x38Prefix = Assert.Single(slot0x38.TopPrefixes);
            Assert.Equal(2, slot0x38Prefix.Count);
            Assert.Equal(
                [
                    "game/data/03/5B/00/00.DAT",
                    "game/data/03/5B/00/01.DAT"
                ],
                slot0x38Prefix.SampleRelativePaths);

            ClientDataSectionSummary slot0x3c = Assert.Single(
                manifest.Summary.SectionSummaries,
                summary => summary.SourceSlotOffsetBytes == 0x3c);
            Assert.Equal(2, slot0x3c.SectionCount);
            Assert.Equal(2, slot0x3c.DistinctPrefixCount);
            Assert.Contains(slot0x3c.PrefixKindCounts, count => count.Kind == ClientDataSectionPrefixKind.FloatLike && count.Count == 1);
            Assert.Contains(slot0x3c.PrefixKindCounts, count => count.Kind == ClientDataSectionPrefixKind.SmallIntegers && count.Count == 1);
        }
        finally
        {
            DeleteDirectory(root);
            DeleteDirectory(output);
        }
    }

    [Fact]
    public async Task ManifestSummarizesVariableUnprovenObservationValues()
    {
        string root = CreateTempDirectory();
        string output = CreateTempDirectory();

        try
        {
            string firstPath = Path.Combine(root, "game", "data", "03", "8D", "00", "00.DAT");
            string secondPath = Path.Combine(root, "game", "data", "03", "8D", "00", "01.DAT");
            Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);

            await File.WriteAllBytesAsync(firstPath, CreateSedbSscfBytes(128, version: 2, word0x18: 0x48646bba));
            await File.WriteAllBytesAsync(secondPath, CreateSedbSscfBytes(128, version: 2, word0x18: 0));

            ClientDataMiner miner = new();
            ClientDataMiningResult result = await miner.MineAsync(new ClientDataMiningRequest(root, output));

            Assert.True(result.Success, result.Error);
            Assert.NotNull(result.ManifestPath);

            ClientDataManifest manifest = await ReadManifestAsync(result.ManifestPath);
            ClientDataObservationSummary word0x18Summary = Assert.Single(
                manifest.Summary.ObservationSummaries,
                summary => summary.Name == "SedbSscf.Word0x18");

            Assert.Equal(2, word0x18Summary.Count);
            Assert.Equal(1, word0x18Summary.NonZeroCount);
            Assert.Equal(2, word0x18Summary.DistinctRawValueCount);
            Assert.Contains(word0x18Summary.TopValues, value => value.RawHex == "ba6b6448" && value.Count == 1);
            Assert.Contains(word0x18Summary.TopValues, value => value.RawHex == "00000000" && value.Count == 1);
        }
        finally
        {
            DeleteDirectory(root);
            DeleteDirectory(output);
        }
    }

    [Fact]
    public void ResourceProbeDetectsDirectGtexPayload()
    {
        byte[] bytes = new byte[64];
        Encoding.ASCII.GetBytes("GTEX").CopyTo(bytes, 0);

        ClientDataResourceProbe? probe = ClientDataResourceProbeExtractor.Create(bytes, 4_128);

        Assert.NotNull(probe);
        Assert.Equal(ClientDataResourceFamily.Gtex, probe.Family);
        Assert.Equal("GTEX", probe.ContainerMagic);
        Assert.Null(probe.InnerMagic);
        Assert.Equal(0, probe.PayloadOffsetBytes);
        Assert.Null(probe.LayoutProbe);
        Assert.Empty(probe.Observations);
    }

    [Fact]
    public void ResourceProbeDetectsVersWrappedGtexPayload()
    {
        byte[] bytes = new byte[64];
        Encoding.ASCII.GetBytes("VERS").CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), 4);
        Encoding.ASCII.GetBytes("GTEX").CopyTo(bytes, 12);
        Encoding.ASCII.GetBytes("GTEX").CopyTo(bytes, 20);

        ClientDataResourceProbe? probe = ClientDataResourceProbeExtractor.Create(bytes, 823_966);

        Assert.NotNull(probe);
        Assert.Equal(ClientDataResourceFamily.VersWrappedGtex, probe.Family);
        Assert.Equal("VERS", probe.ContainerMagic);
        Assert.Equal("GTEX", probe.InnerMagic);
        Assert.Equal(4, probe.Version);
        Assert.Equal(20, probe.PayloadOffsetBytes);
        Assert.Null(probe.LayoutProbe);
        Assert.Empty(probe.Observations);
    }

    [Fact]
    public async Task MinerReturnsFailureWhenClientRootIsMissing()
    {
        string output = CreateTempDirectory();
        ClientDataMiner miner = new();

        try
        {
            ClientDataMiningResult result = await miner.MineAsync(new ClientDataMiningRequest(
                Path.Combine(Path.GetTempPath(), "missing-aetherxiv-client-" + Guid.NewGuid().ToString("N")),
                output));

            Assert.False(result.Success);
            Assert.Contains("does not exist", result.Error, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(output);
        }
    }

    [Fact]
    public void HeaderProbeClassifiesEmptyFiles()
    {
        ClientDataHeaderProbe probe = ClientDataHeaderProbeExtractor.Create([]);

        Assert.Equal(String.Empty, probe.HexPrefix);
        Assert.Equal(ClientDataHeaderKind.Empty, probe.HeaderKind);
        Assert.Empty(probe.MagicCandidates);
    }

    [Fact]
    public void HeaderProbeClassifiesMostlyZeroHeaders()
    {
        byte[] bytes = new byte[32];
        bytes[4] = 0x3f;

        ClientDataHeaderProbe probe = ClientDataHeaderProbeExtractor.Create(bytes);

        Assert.Equal(ClientDataHeaderKind.MostlyZero, probe.HeaderKind);
        Assert.Empty(probe.MagicCandidates);
    }

    [Fact]
    public void HeaderProbeClassifiesHighBitPackedHeaders()
    {
        byte[] bytes = Enumerable.Repeat((byte)0xa3, 32).ToArray();

        ClientDataHeaderProbe probe = ClientDataHeaderProbeExtractor.Create(bytes);

        Assert.Equal(ClientDataHeaderKind.HighBitPacked, probe.HeaderKind);
        Assert.Empty(probe.MagicCandidates);
    }

    [Fact]
    public void HeaderProbeDetectsAlignedResourceMagics()
    {
        byte[] bytes = new byte[32];
        Encoding.ASCII.GetBytes("VERS").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("GTEX").CopyTo(bytes, 12);

        ClientDataHeaderProbe probe = ClientDataHeaderProbeExtractor.Create(bytes);

        Assert.Equal(ClientDataHeaderKind.ResourceMagic, probe.HeaderKind);
        Assert.Contains("VERS", probe.MagicCandidates);
        Assert.Contains("GTEX", probe.MagicCandidates);
    }

    private static async Task<ClientDataManifest> ReadManifestAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        ClientDataManifest? manifest = await JsonSerializer.DeserializeAsync<ClientDataManifest>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });
        return Assert.IsType<ClientDataManifest>(manifest);
    }

    private static async Task<ClientDataActorImportFocusReport> ReadActorImportFocusReportAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        ClientDataActorImportFocusReport? report = await JsonSerializer.DeserializeAsync<ClientDataActorImportFocusReport>(
            stream,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });
        return Assert.IsType<ClientDataActorImportFocusReport>(report);
    }

    private static string ToSha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static byte[] CreateSedbSscfBytes(
        int declaredSize,
        int version = 3,
        uint word0x18 = 0,
        IReadOnlyList<int>? offsets = null)
    {
        byte[] bytes = new byte[Math.Max(64, declaredSize)];
        Encoding.ASCII.GetBytes("SEDB").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("SSCF").CopyTo(bytes, 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), version);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12, 4), 0x00300400);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16, 4), (uint)declaredSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24, 4), word0x18);

        if (offsets is not null)
        {
            int writeOffset = 0x38;
            foreach (int offset in offsets)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(writeOffset, 4), (uint)offset);
                writeOffset += 4;
            }
        }

        return bytes;
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetherxiv-client-data-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}
