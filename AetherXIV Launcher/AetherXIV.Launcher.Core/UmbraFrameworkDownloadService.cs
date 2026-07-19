using System.IO.Compression;
using System.Security.Cryptography;

namespace AetherXIV.Launcher.Core;

public sealed record UmbraFrameworkDownloadProgress(
    string Message,
    long BytesDownloaded,
    long TotalBytes,
    bool LogMessage);

public sealed record UmbraFrameworkDownloadResult(UmbraFrameworkInstall Install, IReadOnlyList<string> Messages);

public static class UmbraFrameworkDownloadService
{
    public static async Task<UmbraFrameworkDownloadResult> DownloadAndInstallAsync(
        UmbraFrameworkArtifact artifact,
        HttpClient httpClient,
        IProgress<UmbraFrameworkDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default,
        string? frameworksRoot = null,
        string? cacheRoot = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (!string.Equals(artifact.ArchiveFormat, "zip", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Umbra framework archive format is not supported: {artifact.ArchiveFormat}");
        if (!artifact.UsesAetherEntrypoints)
            throw new NotSupportedException("Legacy Umbra framework entrypoints are not supported by this launcher build.");

        string frameworkRoot = Path.GetFullPath(frameworksRoot ?? UmbraInstallStore.FrameworksRoot);
        string downloadRoot = Path.GetFullPath(cacheRoot ?? UmbraInstallStore.FrameworkCacheRoot);
        Directory.CreateDirectory(frameworkRoot);
        Directory.CreateDirectory(downloadRoot);

        string archivePath = Path.Combine(downloadRoot, $"{artifact.StableId}.zip");
        await DownloadArchiveAsync(artifact, httpClient, archivePath, progress, cancellationToken);
        ValidateArchive(artifact, archivePath);

        string installRoot = UmbraInstallStore.FrameworkInstallRootFor(artifact, frameworkRoot);
        if (Directory.Exists(installRoot))
            Directory.Delete(installRoot, true);

        Directory.CreateDirectory(installRoot);
        ExtractZip(archivePath, installRoot);

        string bootstrapPath = ResolveInstalledPath(installRoot, artifact.BootstrapRelativePath);
        string frameworkPath = ResolveInstalledPath(installRoot, artifact.FrameworkRelativePath);
        if (!File.Exists(bootstrapPath))
            throw new FileNotFoundException("Umbra bootstrap DLL was not found after extraction.", bootstrapPath);
        if (!File.Exists(frameworkPath))
            throw new FileNotFoundException("Umbra managed framework entrypoint was not found after extraction.", frameworkPath);

        UmbraFrameworkInstall install = new(
            artifact.Name,
            artifact.Version,
            artifact.ApiVersion,
            artifact.PlatformRid,
            installRoot,
            bootstrapPath,
            frameworkPath,
            artifact.SupportedGameSha256,
            DateTimeOffset.UtcNow);
        UmbraInstallStore.Save(install);

        progress?.Report(new UmbraFrameworkDownloadProgress(
            $"Umbra framework installed: {artifact.Name} {artifact.Version}",
            artifact.SizeBytes,
            artifact.SizeBytes,
            true));

        return new UmbraFrameworkDownloadResult(install, new[]
        {
            $"Installed Umbra framework {artifact.Name} {artifact.Version}",
            $"Umbra bootstrap: {bootstrapPath}",
            $"Umbra framework: {frameworkPath}"
        });
    }

    private static async Task DownloadArchiveAsync(
        UmbraFrameworkArtifact artifact,
        HttpClient httpClient,
        string archivePath,
        IProgress<UmbraFrameworkDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Uri sourceUri = new(artifact.ArchiveUrl, UriKind.Absolute);
        string tempPath = $"{archivePath}.download";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        progress?.Report(new UmbraFrameworkDownloadProgress(
            $"Downloading Umbra framework: {artifact.Name} {artifact.Version}",
            0,
            artifact.SizeBytes,
            true));

        using HttpResponseMessage response = await httpClient.GetAsync(
            sourceUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream output = File.Create(tempPath);

        byte[] buffer = new byte[256 * 1024];
        long downloaded = 0;
        long lastReport = -1;
        while (true)
        {
            int read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            downloaded += read;

            if (downloaded - lastReport >= 8 * 1024 * 1024 || downloaded == artifact.SizeBytes)
            {
                lastReport = downloaded;
                progress?.Report(new UmbraFrameworkDownloadProgress(
                    $"Downloading Umbra framework: {downloaded}/{artifact.SizeBytes} bytes",
                    downloaded,
                    artifact.SizeBytes,
                    false));
            }
        }

        if (File.Exists(archivePath))
            File.Delete(archivePath);

        File.Move(tempPath, archivePath);
    }

    private static void ValidateArchive(UmbraFrameworkArtifact artifact, string archivePath)
    {
        FileInfo info = new(archivePath);
        if (info.Length != artifact.SizeBytes)
            throw new InvalidDataException($"Umbra framework archive size mismatch: expected {artifact.SizeBytes}, actual {info.Length}.");

        string actualSha256;
        using (FileStream stream = File.OpenRead(archivePath))
            actualSha256 = Convert.ToHexString(SHA256.HashData(stream));

        if (!string.Equals(actualSha256, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Umbra framework archive SHA256 mismatch.");
    }

    private static void ExtractZip(string archivePath, string installRoot)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string targetPath = ResolveInstalledPath(installRoot, entry.FullName);

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, true);
        }
    }

    private static string ResolveInstalledPath(string installRoot, string relativePath)
    {
        string[] parts = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        string root = Path.GetFullPath(installRoot);
        string target = Path.GetFullPath(Path.Combine([root, .. parts]));

        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(target, root, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Umbra framework archive path escapes install root: {relativePath}");
        }

        return target;
    }
}
