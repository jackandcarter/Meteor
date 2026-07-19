using System.Security.Cryptography;
using System.Text;

namespace Aether.Umbra.Framework;

public static class UmbraRepositoryFetcher
{
    public static async Task<IReadOnlyList<UmbraStoreEntry>> FetchAsync(
        IEnumerable<UmbraRepositorySource> repositories,
        string cacheDirectory,
        UmbraRuntimeLog log,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(cacheDirectory);
        using HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        List<UmbraStoreEntry> entries = new();
        foreach (UmbraRepositorySource repository in repositories)
        {
            try
            {
                string json = await client.GetStringAsync(repository.Url, cancellationToken);
                WriteCache(cacheDirectory, repository.Url, json);
                entries.AddRange(UmbraStoreEntry.ParseRepository(json, repository));
                log.Info($"umbra_repository_fetch_success url={repository.Url}");
            }
            catch (Exception ex)
            {
                string? cached = ReadCache(cacheDirectory, repository.Url);
                if (cached is not null)
                {
                    entries.AddRange(UmbraStoreEntry.ParseRepository(cached, repository));
                    log.Warning($"umbra_repository_fetch_failed_cached url={repository.Url} error={ex.Message}");
                    continue;
                }

                log.Warning($"umbra_repository_fetch_failed url={repository.Url} error={ex.Message}");
            }
        }

        return entries
            .GroupBy(entry => (entry.RepositoryUrl, entry.Id, entry.Version), new StoreEntryKeyComparer())
            .Select(group => group.First())
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void WriteCache(string cacheDirectory, string repositoryUrl, string json)
    {
        File.WriteAllText(CachePath(cacheDirectory, repositoryUrl), json);
    }

    private static string? ReadCache(string cacheDirectory, string repositoryUrl)
    {
        string path = CachePath(cacheDirectory, repositoryUrl);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static string CachePath(string cacheDirectory, string repositoryUrl)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(repositoryUrl));
        return Path.Combine(cacheDirectory, $"{Convert.ToHexString(hash).ToLowerInvariant()}.json");
    }

    private sealed class StoreEntryKeyComparer : IEqualityComparer<(string RepositoryUrl, string Id, string Version)>
    {
        public bool Equals(
            (string RepositoryUrl, string Id, string Version) x,
            (string RepositoryUrl, string Id, string Version) y)
        {
            return string.Equals(x.RepositoryUrl, y.RepositoryUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Version, y.Version, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string RepositoryUrl, string Id, string Version) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.RepositoryUrl),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Version));
        }
    }
}
