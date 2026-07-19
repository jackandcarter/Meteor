using System.Text.Json.Serialization;

namespace AetherXIV.Launcher.Core;

public sealed record UmbraFrameworkCatalog(
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<UmbraFrameworkArtifact> Artifacts)
{
    public UmbraFrameworkArtifact? SelectDefault(string? gameSha256 = null)
    {
        IEnumerable<UmbraFrameworkArtifact> candidates = Artifacts.Where(artifact =>
            artifact.IsActive
            && artifact.UsesAetherEntrypoints);
        if (!string.IsNullOrWhiteSpace(gameSha256))
        {
            candidates = candidates.Where(artifact =>
                artifact.SupportsGameHash(gameSha256)
                || artifact.SupportedGameSha256.Count == 0);
        }

        return candidates
            .OrderByDescending(artifact => artifact.IsDefault)
            .ThenBy(artifact => artifact.SortOrder)
            .FirstOrDefault();
    }
}

public sealed record UmbraFrameworkArtifact(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("api_version")] string ApiVersion,
    [property: JsonPropertyName("platform_rid")] string PlatformRid,
    [property: JsonPropertyName("archive_url")] string ArchiveUrl,
    [property: JsonPropertyName("archive_format")] string ArchiveFormat,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("bootstrap_relative_path")] string BootstrapRelativePath,
    [property: JsonPropertyName("framework_relative_path")] string FrameworkRelativePath,
    [property: JsonPropertyName("supported_game_sha256")] IReadOnlyList<string> SupportedGameSha256,
    [property: JsonPropertyName("is_default")] bool IsDefault,
    [property: JsonPropertyName("is_active")] bool IsActive,
    [property: JsonPropertyName("sort_order")] int SortOrder)
{
    public string StableId => RuntimeInstallStore.SanitizePathSegment(
        $"{PlatformRid}-{Name}-{Version}-{ShortArchiveHash}");

    public bool UsesAetherEntrypoints =>
        string.Equals(Path.GetFileName(BootstrapRelativePath), "Aether.Umbra.Bootstrap.x86.dll", StringComparison.OrdinalIgnoreCase)
        && (string.Equals(Path.GetFileName(FrameworkRelativePath), "Aether.Umbra.Framework.dll", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(FrameworkRelativePath), "Aether.Umbra.Framework.exe", StringComparison.OrdinalIgnoreCase));

    private string ShortArchiveHash => string.IsNullOrWhiteSpace(Sha256)
        ? "nohash"
        : Sha256.Trim()[..Math.Min(12, Sha256.Trim().Length)];

    public bool SupportsGameHash(string sha256)
    {
        return SupportedGameSha256.Count == 0
            || SupportedGameSha256.Any(candidate => string.Equals(candidate, sha256, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record UmbraPluginCatalog(
    [property: JsonPropertyName("repository_name")] string RepositoryName,
    [property: JsonPropertyName("plugins")] IReadOnlyList<UmbraPluginCatalogEntry> Plugins);

public sealed record UmbraPluginCatalogEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("api_version")] string ApiVersion,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("download_url")] string DownloadUrl,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("minimum_framework_version")] string MinimumFrameworkVersion,
    [property: JsonPropertyName("is_active")] bool IsActive);
