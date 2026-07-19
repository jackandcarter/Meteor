using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;

namespace AetherXIV.Launcher.Core;

public sealed record RuntimeDownloadProgress(
    string Message,
    long BytesDownloaded,
    long TotalBytes,
    bool LogMessage);

public sealed record RuntimeDownloadResult(ManagedRuntimeInstall Install, IReadOnlyList<string> Messages);

public static class RuntimeDownloadService
{
    private sealed record ProcessRunResult(int ExitCode, string Output, string Error);

    public static async Task<RuntimeDownloadResult> DownloadAndInstallAsync(
        RuntimeArtifact artifact,
        HttpClient httpClient,
        IProgress<RuntimeDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default,
        string? runtimesRoot = null,
        string? cacheRoot = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (!IsSupportedArchiveFormat(artifact.ArchiveFormat))
            throw new NotSupportedException($"Runtime archive format is not supported: {artifact.ArchiveFormat}");

        string runtimeRoot = Path.GetFullPath(runtimesRoot ?? RuntimeInstallStore.RuntimesRoot);
        string downloadRoot = Path.GetFullPath(cacheRoot ?? RuntimeInstallStore.RuntimeCacheRoot);
        Directory.CreateDirectory(runtimeRoot);
        Directory.CreateDirectory(downloadRoot);

        string archiveExtension = string.Equals(artifact.ArchiveFormat, "zip", StringComparison.OrdinalIgnoreCase)
            ? ".zip"
            : ".tar.xz";
        string archivePath = Path.Combine(downloadRoot, $"{artifact.StableId}{archiveExtension}");
        await DownloadArchiveAsync(artifact, httpClient, archivePath, progress, cancellationToken);
        ValidateArchive(artifact, archivePath);

        string installRoot = RuntimeInstallStore.InstallRootFor(artifact, runtimeRoot);
        if (Directory.Exists(installRoot))
            Directory.Delete(installRoot, true);

        Directory.CreateDirectory(installRoot);
        try
        {
            if (string.Equals(artifact.ArchiveFormat, "zip", StringComparison.OrdinalIgnoreCase))
                ExtractZip(archivePath, installRoot);
            else
                await ExtractTarXzAsync(archivePath, installRoot, cancellationToken);
        }
        catch
        {
            if (Directory.Exists(installRoot))
                Directory.Delete(installRoot, true);
            throw;
        }

        string executablePath = ResolveInstalledPath(installRoot, artifact.ExecutableRelativePath);
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("Runtime executable was not found after extraction.", executablePath);

        TryMarkExecutable(executablePath);

        ManagedRuntimeInstall install = new(
            artifact.Name,
            artifact.Version,
            artifact.PlatformRid,
            artifact.RuntimeKind,
            installRoot,
            executablePath,
            artifact.PrefixArch,
            artifact.Environment,
            DateTimeOffset.UtcNow);
        RuntimeInstallStore.Save(install);

        progress?.Report(new RuntimeDownloadProgress(
            $"Runtime installed: {artifact.Name} {artifact.Version}",
            artifact.SizeBytes,
            artifact.SizeBytes,
            true));

        return new RuntimeDownloadResult(install, new[]
        {
            $"Installed {artifact.Name} {artifact.Version}",
            $"Runtime executable: {executablePath}"
        });
    }

    private static bool IsSupportedArchiveFormat(string archiveFormat)
    {
        return string.Equals(archiveFormat, "zip", StringComparison.OrdinalIgnoreCase)
            || string.Equals(archiveFormat, "tar.xz", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task DownloadArchiveAsync(
        RuntimeArtifact artifact,
        HttpClient httpClient,
        string archivePath,
        IProgress<RuntimeDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Uri sourceUri = new(artifact.ArchiveUrl, UriKind.Absolute);
        string tempPath = $"{archivePath}.download";
        if (File.Exists(tempPath))
            File.Delete(tempPath);
        try
        {
            progress?.Report(new RuntimeDownloadProgress(
                $"Downloading runtime: {artifact.Name} {artifact.Version}",
                0,
                artifact.SizeBytes,
                true));

            using HttpResponseMessage response = await httpClient.GetAsync(
                sourceUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using (Stream input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (FileStream output = File.Create(tempPath))
            {
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
                        progress?.Report(new RuntimeDownloadProgress(
                            $"Downloading runtime: {downloaded}/{artifact.SizeBytes} bytes",
                            downloaded,
                            artifact.SizeBytes,
                            false));
                    }
                }
            }

            if (File.Exists(archivePath))
                File.Delete(archivePath);

            File.Move(tempPath, archivePath);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    private static void ValidateArchive(RuntimeArtifact artifact, string archivePath)
    {
        FileInfo info = new(archivePath);
        if (info.Length != artifact.SizeBytes)
            throw new InvalidDataException($"Runtime archive size mismatch: expected {artifact.SizeBytes}, actual {info.Length}.");

        string actualSha256;
        using (FileStream stream = File.OpenRead(archivePath))
            actualSha256 = Convert.ToHexString(SHA256.HashData(stream));

        if (!string.Equals(actualSha256, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Runtime archive SHA256 mismatch.");
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

    private static async Task ExtractTarXzAsync(
        string archivePath,
        string installRoot,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Managed Wine tar.xz packages are only used on macOS and Linux.");

        string tarCommand = File.Exists("/usr/bin/tar") ? "/usr/bin/tar" : "tar";
        ProcessRunResult listing = await RunTarAsync(
            tarCommand,
            ["-tJf", archivePath],
            cancellationToken);
        if (listing.ExitCode != 0)
            throw new InvalidDataException($"Runtime archive listing failed: {listing.Error.Trim()}");

        foreach (string entry in listing.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            _ = ResolveInstalledPath(installRoot, entry);

        ProcessRunResult extraction = await RunTarAsync(
            tarCommand,
            ["-xJf", archivePath, "-C", installRoot],
            cancellationToken);
        if (extraction.ExitCode != 0)
            throw new InvalidDataException($"Runtime archive extraction failed: {extraction.Error.Trim()}");
    }

    private static async Task<ProcessRunResult> RunTarAsync(
        string command,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = command,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = new() { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            throw new PlatformNotSupportedException("The system tar utility is required to install the managed Wine runtime.", ex);
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return new ProcessRunResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static string ResolveInstalledPath(string installRoot, string relativePath)
    {
        string[] parts = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        string root = Path.GetFullPath(installRoot);
        string target = Path.GetFullPath(Path.Combine([root, .. parts]));

        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(target, root, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Runtime archive path escapes install root: {relativePath}");
        }

        return target;
    }

    private static void TryMarkExecutable(string executablePath)
    {
        if (OperatingSystem.IsWindows())
            return;

        try
        {
            File.SetUnixFileMode(
                executablePath,
                UnixFileMode.UserRead
                    | UnixFileMode.UserWrite
                    | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead
                    | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherRead
                    | UnixFileMode.OtherExecute);
        }
        catch
        {
            // Some filesystems do not support Unix mode changes. The later runtime validator will report executability.
        }
    }
}
