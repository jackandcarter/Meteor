using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace AetherXIV.Launcher.Core;

public sealed record PatchApplyProgress(
    int CurrentPatch,
    int TotalPatches,
    string PatchFileName,
    string Message,
    long BytesProcessed,
    long TotalBytes,
    bool LogMessage);

public sealed record PatchApplyResult(int AppliedPatchCount, IReadOnlyList<string> Messages)
{
    public bool Succeeded => AppliedPatchCount > 0;
}

public static class LegacyPatchApplier
{
    private static readonly byte[] PatchMagic =
    [
        0x91,
        (byte)'Z',
        (byte)'I',
        (byte)'P',
        (byte)'A',
        (byte)'T',
        (byte)'C',
        (byte)'H',
        0x0D,
        0x0A,
        0x1A,
        0x0A
    ];

    public static PatchApplyResult ApplyPatchChain(
        ClientInstall clientInstall,
        PatchLibraryReport patchLibraryReport,
        IProgress<PatchApplyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientInstall.RootPath) || !Directory.Exists(clientInstall.RootPath))
            throw new InvalidOperationException("Client root does not exist.");

        EnsureClientRootWritable(clientInstall.RootPath);

        if (!patchLibraryReport.IsPatchChainReady)
            throw new InvalidOperationException("Patch library is not ready.");

        List<string> messages = new();
        string clientRoot = Path.GetFullPath(clientInstall.RootPath);
        IReadOnlyList<PatchFileReport> patches = patchLibraryReport.FileReports
            .Where(report => report.PatchFileExists)
            .ToArray();
        long totalBytes = patches.Sum(report => new FileInfo(report.PatchPath).Length);
        long completedBytes = 0;

        for (int index = 0; index < patches.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PatchFileReport patch = patches[index];
            long patchLength = new FileInfo(patch.PatchPath).Length;
            PatchApplyContext context = new(
                index + 1,
                patches.Count,
                patch.Entry.PatchFileName,
                completedBytes,
                totalBytes,
                patchLength);

            ReportProgress(
                progress,
                context,
                0,
                $"Starting patch {index + 1}/{patches.Count}: {patch.Entry.PatchFileName}",
                true);

            try
            {
                ApplyPatchFile(clientRoot, patch.PatchPath, messages, progress, context, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"Failed to apply patch {index + 1}/{patches.Count} {patch.Entry.PatchFileName}: {ex.Message}",
                    ex);
            }

            completedBytes += patchLength;

            ReportProgress(
                progress,
                context,
                patchLength,
                $"Finished patch {index + 1}/{patches.Count}: {patch.Entry.PatchFileName}",
                true);
        }

        File.WriteAllText(Path.Combine(clientRoot, "boot.ver"), ClientVersionInfo.TargetBootVersion, Encoding.ASCII);
        File.WriteAllText(Path.Combine(clientRoot, "game.ver"), ClientVersionInfo.TargetGameVersion, Encoding.ASCII);
        messages.Add($"Wrote boot.ver {ClientVersionInfo.TargetBootVersion}.");
        messages.Add($"Wrote game.ver {ClientVersionInfo.TargetGameVersion}.");
        progress?.Report(new PatchApplyProgress(
            patches.Count,
            patches.Count,
            patches.Count == 0 ? "" : patches[^1].Entry.PatchFileName,
            "Patch chain complete.",
            totalBytes,
            totalBytes,
            true));

        return new PatchApplyResult(patches.Count, messages);
    }

    public static void EnsureClientRootWritable(string clientRoot)
    {
        if (string.IsNullOrWhiteSpace(clientRoot) || !Directory.Exists(clientRoot))
            throw new InvalidOperationException("Client root does not exist.");

        string normalizedRoot = Path.GetFullPath(clientRoot);
        string probePath = Path.Combine(normalizedRoot, $".aetherxiv-write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(probePath, [0x45, 0x47]);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new UnauthorizedAccessException(
                "Client folder is not writable. On Windows, patching a client under Program Files usually requires running AetherXIV Launcher as administrator or moving/copying the client to a writable folder.",
                ex);
        }
        catch (IOException ex)
        {
            throw new IOException($"Client folder write test failed: {ex.Message}", ex);
        }
        finally
        {
            try
            {
                if (File.Exists(probePath))
                    File.Delete(probePath);
            }
            catch
            {
                // A failed cleanup is non-fatal; the patcher has proven write access.
            }
        }
    }

    public static void ApplyPatchFile(
        string clientRoot,
        string patchPath,
        IList<string>? messages = null,
        IProgress<PatchApplyProgress>? progress = null,
        PatchApplyContext? context = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedRoot = Path.GetFullPath(clientRoot);
        using FileStream stream = File.OpenRead(patchPath);
        PatchApplyContext localContext = context ?? new(
            1,
            1,
            Path.GetFileName(patchPath),
            0,
            stream.Length,
            stream.Length);

        byte[] header = ReadExact(stream, PatchMagic.Length);
        if (!header.SequenceEqual(PatchMagic))
            throw new InvalidDataException("Invalid ZiPatch header.");

        while (stream.Position < stream.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte[] chunkSizeData = ReadExactOrEnd(stream, 4);
            if (chunkSizeData.Length == 0)
                break;

            if (chunkSizeData.Length != 4)
                throw new EndOfStreamException("Unexpected end of patch chunk size.");

            uint chunkSize = BinaryPrimitives.ReadUInt32BigEndian(chunkSizeData);
            string command = Encoding.ASCII.GetString(ReadExact(stream, 4));
            using LimitedReadStream chunkBody = new(stream, chunkSize);
            switch (command)
            {
                case "FHDR":
                case "DIFF":
                case "HIST":
                case "APLY":
                case "APFS":
                    break;
                case "ADIR":
                    ExecuteDirectoryCreate(chunkBody, normalizedRoot, messages, progress, localContext);
                    break;
                case "DLED":
                case "DELD":
                    ExecuteDirectoryDelete(chunkBody, normalizedRoot, messages, progress, localContext);
                    break;
                case "ETRY":
                    ExecuteFileEntry(chunkBody, normalizedRoot, messages, progress, localContext, cancellationToken);
                    break;
                default:
                    throw new InvalidDataException($"Unhandled ZiPatch command '{command}'.");
            }

            chunkBody.SkipRemaining();
            ReadExact(stream, 4);

            ReportProgress(
                progress,
                localContext,
                stream.Position,
                $"Reading {localContext.PatchFileName}: {FormatByteCount(stream.Position)}/{FormatByteCount(stream.Length)}",
                false);
        }
    }

    private static void ExecuteDirectoryCreate(
        Stream stream,
        string clientRoot,
        IList<string>? messages,
        IProgress<PatchApplyProgress>? progress,
        PatchApplyContext context)
    {
        string directoryPath = ReadPatchPath(stream, clientRoot);
        ReportProgress(progress, context, stream.Position, $"Create directory {ToDisplayPath(clientRoot, directoryPath)}", false);

        if (Directory.Exists(directoryPath))
        {
            messages?.Add($"Directory already exists: {directoryPath}");
            return;
        }

        Directory.CreateDirectory(directoryPath);
    }

    private static void ExecuteDirectoryDelete(
        Stream stream,
        string clientRoot,
        IList<string>? messages,
        IProgress<PatchApplyProgress>? progress,
        PatchApplyContext context)
    {
        string directoryPath = ReadPatchPath(stream, clientRoot);
        ReportProgress(progress, context, stream.Position, $"Delete directory {ToDisplayPath(clientRoot, directoryPath)}", false);

        if (!Directory.Exists(directoryPath))
        {
            messages?.Add($"Directory not found for deletion: {directoryPath}");
            return;
        }

        Directory.Delete(directoryPath, true);
    }

    private static void ExecuteFileEntry(
        Stream input,
        string clientRoot,
        IList<string>? messages,
        IProgress<PatchApplyProgress>? progress,
        PatchApplyContext context,
        CancellationToken cancellationToken)
    {
        string filePath = ReadPatchPath(input, clientRoot);
        string displayPath = ToDisplayPath(clientRoot, filePath);

        uint itemCount = ReadUInt32BigEndian(input);
        uint? expectedFileSize = null;
        byte[]? expectedHash = null;

        ReportProgress(
            progress,
            context,
            input.Position,
            $"Applying {displayPath} ({itemCount} chunk{(itemCount == 1 ? "" : "s")})",
            false);

        for (uint index = 0; index < itemCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            byte entryMode = ReadFourByteMode(input, "entry mode");
            if (entryMode is not (0x41 or 0x44 or 0x4D))
                throw new InvalidDataException($"Unknown ZiPatch entry mode 0x{entryMode:X2}.");

            ReadExact(input, 0x14);
            byte[] destinationHash = ReadExact(input, 0x14);

            byte compressionMode = ReadFourByteMode(input, "compression mode");
            uint compressedSize = ReadUInt32BigEndian(input);
            ReadUInt32BigEndian(input);
            uint newFileSize = ReadUInt32BigEndian(input);
            expectedFileSize = newFileSize;

            if (entryMode == 0x44)
            {
                SkipPayload(input, compressedSize, cancellationToken);
                if (File.Exists(filePath))
                    File.Delete(filePath);
                else
                    messages?.Add($"File not found for deletion: {filePath}");

                continue;
            }

            if (index != itemCount - 1 && compressedSize != 0)
                throw new InvalidDataException($"Non-final ZiPatch entry contains file data: {displayPath}.");

            if (compressedSize == 0)
                continue;

            expectedHash = destinationHash;
            using FileStream output = CreateOutputFile(filePath);

            if (compressionMode == 0x4E)
            {
                using LimitedReadStream limitedInput = new(
                    input,
                    compressedSize,
                    bytesRead => ReportProgress(
                        progress,
                        context,
                        input.Position,
                        $"Writing {displayPath}: {FormatByteCount(bytesRead)}/{FormatByteCount(compressedSize)} raw chunk {index + 1}/{itemCount}",
                        false));
                CopyToWithCancellation(limitedInput, output, cancellationToken);
                limitedInput.SkipRemaining();
            }
            else if (compressionMode == 0x5A)
            {
                using LimitedReadStream limitedInput = new(
                    input,
                    compressedSize,
                    bytesRead => ReportProgress(
                        progress,
                        context,
                        input.Position,
                        $"Writing {displayPath}: {FormatByteCount(bytesRead)}/{FormatByteCount(compressedSize)} compressed chunk {index + 1}/{itemCount}",
                        false));
                using ZLibStream zlib = new(limitedInput, CompressionMode.Decompress);
                CopyToWithCancellation(zlib, output, cancellationToken);
                limitedInput.SkipRemaining();
            }
            else
            {
                throw new InvalidDataException($"Unknown ZiPatch compression mode 0x{compressionMode:X2}.");
            }
        }

        if (expectedFileSize.HasValue && File.Exists(filePath))
        {
            long actualSize = new FileInfo(filePath).Length;
            if (actualSize != expectedFileSize.Value)
                throw new InvalidDataException(
                    $"Patched file size differs from manifest for {displayPath}: expected {expectedFileSize.Value}, got {actualSize}.");
        }

        if (expectedHash is not null && !IsAllZero(expectedHash) && File.Exists(filePath))
        {
            using FileStream verifyInput = File.OpenRead(filePath);
            byte[] actualHash = SHA1.HashData(verifyInput);
            if (!actualHash.SequenceEqual(expectedHash))
                throw new InvalidDataException($"Patched file hash differs from manifest for {displayPath}.");
        }
    }

    private static string ReadPatchPath(Stream stream, string clientRoot)
    {
        uint pathSize = ReadUInt32BigEndian(stream);
        string relativePath = Encoding.UTF8.GetString(ReadExact(stream, checked((int)pathSize)));
        string[] pathParts = relativePath
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        string fullPath = Path.GetFullPath(Path.Combine([clientRoot, .. pathParts]));

        if (!fullPath.StartsWith(clientRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(fullPath, clientRoot, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Patch path escapes client root: {relativePath}");
        }

        return fullPath;
    }

    private static FileStream CreateOutputFile(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        return File.Create(filePath);
    }

    private static byte ReadFourByteMode(Stream stream, string label)
    {
        byte[] data = ReadExact(stream, 4);
        if (data[1] != 0 || data[2] != 0 || data[3] != 0)
            throw new InvalidDataException($"Invalid ZiPatch {label} bytes.");

        return data[0];
    }

    private static bool IsAllZero(byte[] data)
    {
        foreach (byte value in data)
        {
            if (value != 0)
                return false;
        }

        return true;
    }

    private static void SkipPayload(Stream stream, uint byteCount, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[128 * 1024];
        long remaining = byteCount;
        while (remaining > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0)
                throw new EndOfStreamException("Patch payload ended early.");

            remaining -= read;
        }
    }

    private static void CopyToWithCancellation(Stream input, Stream output, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[128 * 1024];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = input.Read(buffer, 0, buffer.Length);
            if (read == 0)
                return;

            output.Write(buffer, 0, read);
        }
    }

    private static void ReportProgress(
        IProgress<PatchApplyProgress>? progress,
        PatchApplyContext context,
        long patchBytesProcessed,
        string message,
        bool logMessage)
    {
        if (progress is null)
            return;

        long boundedPatchBytes = Math.Clamp(patchBytesProcessed, 0, context.PatchLengthBytes);
        long totalBytesProcessed = Math.Clamp(context.CompletedBytesBeforePatch + boundedPatchBytes, 0, context.TotalBytes);
        if (!context.ShouldReport(totalBytesProcessed, logMessage))
            return;

        progress.Report(new PatchApplyProgress(
            context.CurrentPatch,
            context.TotalPatches,
            context.PatchFileName,
            message,
            totalBytesProcessed,
            context.TotalBytes,
            logMessage));
    }

    private static string ToDisplayPath(string clientRoot, string fullPath)
    {
        return Path.GetRelativePath(clientRoot, fullPath);
    }

    private static string FormatByteCount(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }

    private static uint ReadUInt32BigEndian(Stream stream)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(ReadExact(stream, 4));
    }

    private static uint ReadUInt32LittleEndian(Stream stream)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(ReadExact(stream, 4));
    }

    private static byte[] ReadExact(Stream stream, int length)
    {
        byte[] buffer = new byte[length];
        stream.ReadExactly(buffer);
        return buffer;
    }

    private static byte[] ReadExactOrEnd(Stream stream, int length)
    {
        byte[] buffer = new byte[length];
        int offset = 0;
        while (offset < length)
        {
            int read = stream.Read(buffer, offset, length - offset);
            if (read == 0)
            {
                if (offset == 0)
                    return [];

                return buffer[..offset];
            }

            offset += read;
        }

        return buffer;
    }
}

public sealed class PatchApplyContext
{
    private const long ByteReportThreshold = 8 * 1024 * 1024;
    private static readonly long TickReportThreshold = Stopwatch.Frequency / 4;

    private long lastReportedBytes = -1;
    private long lastReportedTicks;

    public PatchApplyContext(
        int currentPatch,
        int totalPatches,
        string patchFileName,
        long completedBytesBeforePatch,
        long totalBytes,
        long patchLengthBytes)
    {
        CurrentPatch = currentPatch;
        TotalPatches = totalPatches;
        PatchFileName = patchFileName;
        CompletedBytesBeforePatch = completedBytesBeforePatch;
        TotalBytes = totalBytes;
        PatchLengthBytes = patchLengthBytes;
        lastReportedTicks = Stopwatch.GetTimestamp();
    }

    public int CurrentPatch { get; }

    public int TotalPatches { get; }

    public string PatchFileName { get; }

    public long CompletedBytesBeforePatch { get; }

    public long TotalBytes { get; }

    public long PatchLengthBytes { get; }

    public bool ShouldReport(long totalBytesProcessed, bool logMessage)
    {
        if (logMessage)
        {
            lastReportedBytes = totalBytesProcessed;
            lastReportedTicks = Stopwatch.GetTimestamp();
            return true;
        }

        long now = Stopwatch.GetTimestamp();
        if (lastReportedBytes < 0
            || totalBytesProcessed >= TotalBytes
            || totalBytesProcessed - lastReportedBytes >= ByteReportThreshold
            || now - lastReportedTicks >= TickReportThreshold)
        {
            lastReportedBytes = totalBytesProcessed;
            lastReportedTicks = now;
            return true;
        }

        return false;
    }
}

internal sealed class LimitedReadStream : Stream
{
    private readonly Stream inner;
    private readonly Action<long>? onRead;
    private long remainingBytes;
    private long totalBytesRead;

    public LimitedReadStream(Stream inner, long byteLimit, Action<long>? onRead = null)
    {
        this.inner = inner;
        this.onRead = onRead;
        remainingBytes = byteLimit;
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => totalBytesRead;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (remainingBytes <= 0)
            return 0;

        int toRead = (int)Math.Min(count, remainingBytes);
        int read = inner.Read(buffer, offset, toRead);
        remainingBytes -= read;
        totalBytesRead += read;
        if (read > 0)
            onRead?.Invoke(totalBytesRead);

        return read;
    }

    public void SkipRemaining()
    {
        byte[] buffer = new byte[8192];
        while (remainingBytes > 0)
        {
            int read = Read(buffer, 0, (int)Math.Min(buffer.Length, remainingBytes));
            if (read == 0)
                throw new EndOfStreamException("Compressed patch payload ended early.");
        }
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
