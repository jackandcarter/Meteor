using System.Globalization;

namespace AetherXIV.Launcher.Core;

public sealed record PatchDownloadProgress(
    int CurrentFile,
    int TotalFiles,
    string RelativePath,
    long FileBytesDownloaded,
    long FileSizeBytes,
    long TotalBytesDownloaded,
    long TotalSizeBytes,
    string Message,
    bool LogMessage);

public sealed record PatchDownloadResult(int DownloadedFileCount, int ReusedFileCount, IReadOnlyList<string> Messages);

public static class PatchDownloadService
{
    public static async Task<PatchDownloadResult> DownloadPatchLibraryAsync(
        LauncherPatchManifest manifest,
        string patchLibraryRoot,
        HttpClient httpClient,
        IProgress<PatchDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (string.IsNullOrWhiteSpace(patchLibraryRoot))
            throw new ArgumentException("Patch library root is required.", nameof(patchLibraryRoot));

        string normalizedRoot = Path.GetFullPath(patchLibraryRoot);
        Directory.CreateDirectory(normalizedRoot);

        long totalBytes = manifest.Files.Sum(file => file.SizeBytes);
        long completedBytes = 0;
        int downloaded = 0;
        int reused = 0;
        List<string> messages = new();

        for (int index = 0; index < manifest.Files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LauncherPatchFile file = manifest.Files[index];
            string localPath = ResolveLocalPath(normalizedRoot, file.RelativePath);
            long completedBeforeFile = completedBytes;

            if (IsLocalFileValid(localPath, file))
            {
                reused++;
                completedBytes += file.SizeBytes;
                progress?.Report(new PatchDownloadProgress(
                    index + 1,
                    manifest.Files.Count,
                    file.RelativePath,
                    file.SizeBytes,
                    file.SizeBytes,
                    completedBytes,
                    totalBytes,
                    $"Verified local patch {index + 1}/{manifest.Files.Count}: {file.RelativePath}",
                    true));
                continue;
            }

            Uri sourceUri = ResolveSourceUri(manifest.PatchBaseUrl, file.RelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
            string tempPath = $"{localPath}.download";

            progress?.Report(new PatchDownloadProgress(
                index + 1,
                manifest.Files.Count,
                file.RelativePath,
                0,
                file.SizeBytes,
                completedBeforeFile,
                totalBytes,
                $"Downloading patch {index + 1}/{manifest.Files.Count}: {file.RelativePath}",
                true));

            await DownloadFileAsync(
                httpClient,
                sourceUri,
                tempPath,
                file,
                index + 1,
                manifest.Files.Count,
                completedBeforeFile,
                totalBytes,
                progress,
                cancellationToken);

            if (!IsLocalFileValid(tempPath, file))
                throw new InvalidDataException($"Downloaded patch failed validation: {file.RelativePath}");

            if (File.Exists(localPath))
                File.Delete(localPath);

            File.Move(tempPath, localPath);
            downloaded++;
            completedBytes += file.SizeBytes;
            messages.Add($"Downloaded {file.RelativePath}");
        }

        return new PatchDownloadResult(downloaded, reused, messages);
    }

    public static bool IsLocalFileValid(string path, LauncherPatchFile file)
    {
        if (!File.Exists(path))
            return false;

        FileInfo info = new(path);
        if (info.Length != file.SizeBytes)
            return false;

        if (!uint.TryParse(file.Crc32, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint expectedCrc32))
            return false;

        return Crc32.ComputeFile(path) == expectedCrc32;
    }

    private static async Task DownloadFileAsync(
        HttpClient httpClient,
        Uri sourceUri,
        string tempPath,
        LauncherPatchFile file,
        int currentFile,
        int totalFiles,
        long completedBytesBeforeFile,
        long totalBytes,
        IProgress<PatchDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(
            sourceUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream output = File.Create(tempPath);

        byte[] buffer = new byte[128 * 1024];
        long fileBytes = 0;
        long lastReportBytes = -1;

        while (true)
        {
            int read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            fileBytes += read;

            if (fileBytes - lastReportBytes >= 8 * 1024 * 1024 || fileBytes == file.SizeBytes)
            {
                lastReportBytes = fileBytes;
                progress?.Report(new PatchDownloadProgress(
                    currentFile,
                    totalFiles,
                    file.RelativePath,
                    fileBytes,
                    file.SizeBytes,
                    completedBytesBeforeFile + fileBytes,
                    totalBytes,
                    $"Downloading {file.RelativePath}: {fileBytes}/{file.SizeBytes} bytes",
                    false));
            }
        }
    }

    private static string ResolveLocalPath(string root, string relativePath)
    {
        string[] pathParts = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        string localPath = Path.GetFullPath(Path.Combine([root, .. pathParts]));

        if (!localPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(localPath, root, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Patch path escapes library root: {relativePath}");
        }

        return localPath;
    }

    private static Uri ResolveSourceUri(string patchBaseUrl, string relativePath)
    {
        string normalizedBase = patchBaseUrl.EndsWith('/') ? patchBaseUrl : $"{patchBaseUrl}/";
        string normalizedRelative = relativePath.Replace('\\', '/');
        return new Uri(new Uri(normalizedBase, UriKind.Absolute), normalizedRelative);
    }
}
