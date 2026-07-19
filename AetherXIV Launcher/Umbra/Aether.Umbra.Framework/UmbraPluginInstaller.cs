using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace Aether.Umbra.Framework;

public sealed record UmbraPluginInstallResult(
    string PluginId,
    string InstallDirectory,
    string ManifestPath);

public static class UmbraPluginInstaller
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static async Task<UmbraPluginInstallResult> DownloadAndInstallAsync(
        UmbraStoreEntry entry,
        string pluginDirectory,
        string cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        entry.ValidateInstallable();
        if (!IsAllowedDownloadUri(entry.DownloadUrl))
            throw new InvalidDataException($"Umbra plugin download URL is not allowed: {entry.DownloadUrl}");

        Directory.CreateDirectory(pluginDirectory);
        Directory.CreateDirectory(cacheDirectory);

        string archivePath = Path.Combine(cacheDirectory, $"{SanitizePathSegment(entry.Id)}-{entry.Version}.zip");
        using HttpClient client = new();
        await using (Stream source = await client.GetStreamAsync(entry.DownloadUrl, cancellationToken))
        await using (FileStream destination = File.Create(archivePath))
            await source.CopyToAsync(destination, cancellationToken);

        return InstallVerifiedArchive(entry, archivePath, pluginDirectory);
    }

    public static UmbraPluginInstallResult InstallVerifiedArchive(
        UmbraStoreEntry entry,
        string archivePath,
        string pluginDirectory)
    {
        entry.ValidateInstallable();
        ValidateArchive(entry, archivePath);

        string installDirectory = Path.GetFullPath(Path.Combine(pluginDirectory, SanitizePathSegment(entry.Id)));
        string pluginRoot = Path.GetFullPath(pluginDirectory);
        if (!installDirectory.StartsWith(pluginRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Umbra plugin install path escapes the plugin directory.");

        Directory.CreateDirectory(pluginRoot);
        if (Directory.Exists(installDirectory))
            Directory.Delete(installDirectory, recursive: true);
        Directory.CreateDirectory(installDirectory);

        ZipFile.ExtractToDirectory(archivePath, installDirectory);
        ValidateExtractedPaths(installDirectory);

        string manifestPath = FindManifest(installDirectory)
            ?? Path.Combine(installDirectory, "umbra-plugin.json");
        if (!File.Exists(manifestPath))
        {
            UmbraPluginManifest manifest = entry.ToManifest(enabled: false);
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, ManifestJsonOptions));
        }

        UmbraPluginManifest.Load(manifestPath);
        return new UmbraPluginInstallResult(entry.Id, installDirectory, manifestPath);
    }

    private static void ValidateArchive(UmbraStoreEntry entry, string archivePath)
    {
        FileInfo info = new(archivePath);
        if (!info.Exists)
            throw new FileNotFoundException("Umbra plugin archive was not found.", archivePath);
        if (info.Length != entry.SizeBytes)
            throw new InvalidDataException($"Umbra plugin archive size mismatch: expected {entry.SizeBytes}, actual {info.Length}.");

        using FileStream stream = File.OpenRead(archivePath);
        string sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(sha256, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Umbra plugin archive SHA256 mismatch.");

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        foreach (ZipArchiveEntry zipEntry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(zipEntry.FullName))
                continue;

            if (Path.IsPathRooted(zipEntry.FullName)
                || zipEntry.FullName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
            {
                throw new InvalidDataException($"Umbra plugin archive path escapes install root: {zipEntry.FullName}");
            }
        }
    }

    private static void ValidateExtractedPaths(string installDirectory)
    {
        string root = Path.GetFullPath(installDirectory);
        foreach (string path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Umbra plugin extracted path escapes install root: {path}");
        }
    }

    private static string? FindManifest(string installDirectory)
    {
        foreach (string fileName in new[] { "umbra-plugin.json", "plugin.json" })
        {
            string candidate = Path.Combine(installDirectory, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static bool IsAllowedDownloadUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
            return false;

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizePathSegment(string value)
    {
        string sanitized = new(value
            .Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "plugin" : sanitized;
    }
}
