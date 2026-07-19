using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AetherXIV.Operator;

internal sealed record LuaTreeManifest(
    string Schema,
    int FileCount,
    string TreeSha256,
    Dictionary<string, string> Files);

internal static class LuaTreeManifestVerifier
{
    private const string ManifestSchema = "aetherxiv.lua-tree-manifest.v1";

    public static AetherXivDependencyCheckStep Verify(string scriptsRoot, string manifestPath)
    {
        try
        {
            if (!File.Exists(manifestPath))
            {
                return Warning($"Lua tree inventory is missing: {manifestPath}");
            }

            LuaTreeManifest? manifest = JsonSerializer.Deserialize<LuaTreeManifest>(
                File.ReadAllText(manifestPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (manifest is null || manifest.Schema != ManifestSchema || manifest.Files is null)
                return Warning($"Lua tree inventory is invalid or has an unsupported schema: {manifestPath}");

            Dictionary<string, string> actualFiles = Directory
                .EnumerateFiles(scriptsRoot, "*", SearchOption.AllDirectories)
                .Select(path => new
                {
                    Path = Path.GetRelativePath(scriptsRoot, path).Replace(Path.DirectorySeparatorChar, '/'),
                    Hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()
                })
                .OrderBy(item => item.Path, StringComparer.Ordinal)
                .ToDictionary(item => item.Path, item => item.Hash, StringComparer.Ordinal);

            string[] missing = manifest.Files.Keys.Except(actualFiles.Keys, StringComparer.Ordinal).Order().ToArray();
            string[] extra = actualFiles.Keys.Except(manifest.Files.Keys, StringComparer.Ordinal).Order().ToArray();
            string[] changed = manifest.Files.Keys
                .Intersect(actualFiles.Keys, StringComparer.Ordinal)
                .Where(path => !StringComparer.OrdinalIgnoreCase.Equals(manifest.Files[path], actualFiles[path]))
                .Order()
                .ToArray();
            string actualTreeHash = ComputeTreeHash(actualFiles);
            if (manifest.FileCount != actualFiles.Count
                || missing.Length != 0
                || extra.Length != 0
                || changed.Length != 0
                || !StringComparer.OrdinalIgnoreCase.Equals(manifest.TreeSha256, actualTreeHash))
            {
                return Warning(
                    $"Lua tree inventory differs: expected {manifest.FileCount} files/{manifest.TreeSha256}, " +
                    $"actual {actualFiles.Count}/{actualTreeHash}; missing={Preview(missing)}, " +
                    $"extra={Preview(extra)}, changed={Preview(changed)}.");
            }

            return new AetherXivDependencyCheckStep(
                "scripts-integrity",
                AetherXivDependencyStatus.Passed,
                $"Lua inventory matches {actualFiles.Count} files ({actualTreeHash}).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return Warning($"Lua tree inventory could not be compared: {ex.Message}");
        }
    }

    private static string ComputeTreeHash(IReadOnlyDictionary<string, string> files)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach ((string path, string contentHash) in files.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(path));
            hash.AppendData([0]);
            hash.AppendData(Convert.FromHexString(contentHash));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Preview(IReadOnlyList<string> values) =>
        values.Count == 0 ? "none" : String.Join(',', values.Take(3));

    private static AetherXivDependencyCheckStep Warning(string message) =>
        new("scripts-integrity", AetherXivDependencyStatus.Warning, message);
}
