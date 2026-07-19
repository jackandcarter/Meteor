using System.Buffers.Binary;
using System.Text;

namespace AetherXIV.ClientData;

public static class SedbSscfResourceParser
{
    private static readonly int[] SafeSectionSlotOffsets =
    [
        0x38,
        0x3c,
        0x40,
        0x48,
        0x50,
        0x60
    ];

    public static ClientDataResourceProbe? Parse(ReadOnlySpan<byte> bytes, long fileSizeBytes)
    {
        if (bytes.Length < 20)
            return null;

        string containerMagic = Encoding.ASCII.GetString(bytes.Slice(0, 4));
        string innerMagic = Encoding.ASCII.GetString(bytes.Slice(4, 4));
        if (containerMagic != "SEDB" || innerMagic != "SSCF")
            return null;

        int version = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8, 4));
        long declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(16, 4));

        return new ClientDataResourceProbe(
            ClientDataResourceFamily.SedbSscf,
            containerMagic,
            innerMagic,
            version,
            declaredSize,
            declaredSize == fileSizeBytes,
            null,
            CreateLayoutProbe(bytes, declaredSize),
            CreateObservations(bytes));
    }

    private static ClientDataResourceLayoutProbe CreateLayoutProbe(ReadOnlySpan<byte> bytes, long declaredSize)
    {
        const int scanStart = 0x38;
        int scanEnd = Math.Min(bytes.Length, 0x90);
        List<int> candidateOffsets = [];
        List<int> candidateSlotOffsets = [];
        List<int> invalidNonZeroSlotOffsets = [];
        List<ClientDataLayoutSlotProbe> slotProbes = [];
        int invalidNonZeroCount = 0;

        for (int offset = scanStart; offset + 4 <= scanEnd; offset += 4)
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
            if (value == 0)
                continue;

            if (IsCandidateOffset(value, declaredSize))
            {
                int candidate = checked((int)value);
                if (!candidateOffsets.Contains(candidate))
                    candidateOffsets.Add(candidate);
                candidateSlotOffsets.Add(offset);
                slotProbes.Add(CreateSlotProbe(offset, ClientDataLayoutSlotClassification.CandidateOffset, bytes.Slice(offset, 4), value));
                continue;
            }

            invalidNonZeroCount++;
            invalidNonZeroSlotOffsets.Add(offset);
            slotProbes.Add(CreateSlotProbe(offset, ClientDataLayoutSlotClassification.InvalidNonZero, bytes.Slice(offset, 4), value));
        }

        candidateOffsets.Sort();
        IReadOnlyList<ClientDataSectionProbe> sectionProbes = CreateSectionProbes(
            bytes,
            declaredSize,
            slotProbes,
            candidateOffsets);

        return new ClientDataResourceLayoutProbe(
            "SedbSscf.EarlyAlignedOffsetCandidates",
            ClientDataFieldObservationStatus.ObservedUnproven,
            scanStart,
            scanEnd,
            candidateOffsets.Count,
            invalidNonZeroCount,
            candidateOffsets.Count == 0 ? null : candidateOffsets[0],
            candidateOffsets.Count == 0 ? null : candidateOffsets[^1],
            candidateSlotOffsets,
            invalidNonZeroSlotOffsets,
            candidateOffsets.Take(12).ToArray(),
            "Aligned in-file values observed in the early SEDB/SSCF header/table area. These are candidate offsets only; table semantics are not proven yet.")
        {
            SlotProbes = slotProbes,
            SectionProbes = sectionProbes
        };
    }

    private static IReadOnlyList<ClientDataSectionProbe> CreateSectionProbes(
        ReadOnlySpan<byte> bytes,
        long declaredSize,
        IReadOnlyList<ClientDataLayoutSlotProbe> slotProbes,
        IReadOnlyList<int> sortedCandidateOffsets)
    {
        List<ClientDataSectionProbe> sections = [];
        foreach (ClientDataLayoutSlotProbe slot in slotProbes)
        {
            if (slot.Classification != ClientDataLayoutSlotClassification.CandidateOffset)
                continue;

            if (!SafeSectionSlotOffsets.Contains(slot.SlotOffsetBytes))
                continue;

            int sectionOffset = checked((int)slot.UnsignedLittleEndianValue);
            int? nextOffset = FindNextSectionOffset(sectionOffset, declaredSize, sortedCandidateOffsets);
            int? length = nextOffset is null ? null : nextOffset.Value - sectionOffset;
            ReadOnlySpan<byte> prefix = ReadSectionPrefix(bytes, sectionOffset, length);

            sections.Add(new ClientDataSectionProbe(
                slot.SlotOffsetBytes,
                sectionOffset,
                nextOffset,
                length,
                Convert.ToHexString(prefix).ToLowerInvariant(),
                prefix.Length >= 4 ? BinaryPrimitives.ReadUInt32LittleEndian(prefix.Slice(0, 4)) : null,
                CreateWordShape(prefix, declaredSize),
                ClassifySectionPrefix(prefix, declaredSize),
                ClientDataFieldObservationStatus.ObservedUnproven,
                "Section bytes reached from a stable early SEDB/SSCF candidate-offset slot. This is parser evidence only; table semantics are not proven yet."));
        }

        return sections;
    }

    private static int? FindNextSectionOffset(
        int sectionOffset,
        long declaredSize,
        IReadOnlyList<int> sortedCandidateOffsets)
    {
        foreach (int candidate in sortedCandidateOffsets)
        {
            if (candidate > sectionOffset)
                return candidate;
        }

        return declaredSize <= Int32.MaxValue && declaredSize > sectionOffset
            ? checked((int)declaredSize)
            : null;
    }

    private static ReadOnlySpan<byte> ReadSectionPrefix(ReadOnlySpan<byte> bytes, int sectionOffset, int? length)
    {
        const int prefixLength = 32;
        if (sectionOffset < 0 || sectionOffset >= bytes.Length)
            return [];

        int maxLength = length is > 0
            ? Math.Min(prefixLength, length.Value)
            : prefixLength;
        int available = Math.Min(maxLength, bytes.Length - sectionOffset);
        return bytes.Slice(sectionOffset, available);
    }

    private static string CreateWordShape(ReadOnlySpan<byte> prefix, long declaredSize)
    {
        if (prefix.Length == 0)
            return "unavailable";

        int wordCount = prefix.Length / 4;
        if (wordCount == 0)
            return "partial";

        StringBuilder shape = new(wordCount);
        for (int i = 0; i < wordCount; i++)
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(prefix.Slice(i * 4, 4));
            if (value == 0)
                shape.Append('Z');
            else if (IsCandidateOffset(value, declaredSize))
                shape.Append('O');
            else if (value == 0x3f800000)
                shape.Append('F');
            else if (value <= UInt16.MaxValue)
                shape.Append('S');
            else
                shape.Append('B');
        }

        if (prefix.Length % 4 != 0)
            shape.Append('P');

        return shape.ToString();
    }

    private static ClientDataSectionPrefixKind ClassifySectionPrefix(ReadOnlySpan<byte> prefix, long declaredSize)
    {
        if (prefix.Length == 0)
            return ClientDataSectionPrefixKind.PrefixUnavailable;

        if (prefix.IsEmpty)
            return ClientDataSectionPrefixKind.Empty;

        int zeroCount = 0;
        foreach (byte value in prefix)
        {
            if (value == 0)
                zeroCount++;
        }

        if (zeroCount == prefix.Length)
            return ClientDataSectionPrefixKind.Empty;

        int wordCount = prefix.Length / 4;
        if (wordCount > 0)
        {
            int smallIntegerCount = 0;
            int offsetLikeCount = 0;
            bool containsFloatOne = false;

            for (int i = 0; i < wordCount; i++)
            {
                uint value = BinaryPrimitives.ReadUInt32LittleEndian(prefix.Slice(i * 4, 4));
                if (value <= UInt16.MaxValue)
                    smallIntegerCount++;

                if (IsCandidateOffset(value, declaredSize))
                    offsetLikeCount++;

                if (value == 0x3f800000)
                    containsFloatOne = true;
            }

            if (containsFloatOne)
                return ClientDataSectionPrefixKind.FloatLike;

            if (offsetLikeCount >= 2)
                return ClientDataSectionPrefixKind.OffsetLike;

            if (smallIntegerCount == wordCount)
                return ClientDataSectionPrefixKind.SmallIntegers;
        }

        if (zeroCount * 4 >= prefix.Length * 3)
            return ClientDataSectionPrefixKind.MostlyZero;

        return ClientDataSectionPrefixKind.Binary;
    }

    private static ClientDataLayoutSlotProbe CreateSlotProbe(
        int slotOffset,
        ClientDataLayoutSlotClassification classification,
        ReadOnlySpan<byte> raw,
        uint value)
    {
        return new ClientDataLayoutSlotProbe(
            slotOffset,
            classification,
            Convert.ToHexString(raw).ToLowerInvariant(),
            value);
    }

    private static bool IsCandidateOffset(uint value, long declaredSize)
    {
        return value >= 0x30
            && value < declaredSize
            && value % 0x10 == 0;
    }

    private static IReadOnlyList<ClientDataFieldObservation> CreateObservations(ReadOnlySpan<byte> bytes)
    {
        List<ClientDataFieldObservation> observations = [];
        AddUInt32Observation(
            observations,
            bytes,
            0x0c,
            "SedbSscf.Word0x0C",
            "Observed as 0x00300400 across the current client manifest; purpose is not proven yet.");
        AddUInt32Observation(
            observations,
            bytes,
            0x14,
            "SedbSscf.Word0x14",
            "Usually zero in sampled files; purpose is not proven yet.");
        AddUInt32Observation(
            observations,
            bytes,
            0x18,
            "SedbSscf.Word0x18",
            "Sometimes non-zero in version 2 files; purpose is not proven yet.");
        AddUInt32Observation(
            observations,
            bytes,
            0x1c,
            "SedbSscf.Word0x1C",
            "Usually zero in sampled files; purpose is not proven yet.");

        return observations;
    }

    private static void AddUInt32Observation(
        List<ClientDataFieldObservation> observations,
        ReadOnlySpan<byte> bytes,
        int offset,
        string name,
        string note)
    {
        if (bytes.Length < offset + 4)
            return;

        ReadOnlySpan<byte> raw = bytes.Slice(offset, 4);
        observations.Add(new ClientDataFieldObservation(
            name,
            offset,
            4,
            Convert.ToHexString(raw).ToLowerInvariant(),
            BinaryPrimitives.ReadUInt32LittleEndian(raw),
            ClientDataFieldObservationStatus.ObservedUnproven,
            note));
    }
}
