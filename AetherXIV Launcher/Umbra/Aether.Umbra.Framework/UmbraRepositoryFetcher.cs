using System.Security.Cryptography;
using System.Text;

namespace Aether.Umbra.Framework;

public static class UmbraRepositoryFetcher
{
    private const int MaximumRepositoryBytes = 2 * 1024 * 1024;

    public static async Task<IReadOnlyList<UmbraStoreEntry>> FetchAsync(
        IEnumerable<UmbraRepositorySource> repositories,
        string cacheDirectory,
        UmbraRuntimeLog log,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(cacheDirectory);
        using HttpClient client = CreateClient();

        List<UmbraStoreEntry> entries = new();
        foreach (UmbraRepositorySource repository in repositories)
        {
            try
            {
                IReadOnlyList<UmbraStoreEntry> fetched = await FetchRepositoryAsync(
                    client,
                    repository,
                    cacheDirectory,
                    cancellationToken);
                entries.AddRange(fetched);
                log.Info($"umbra_repository_fetch_success url={repository.Url}");
            }
            catch (Exception ex)
            {
                string? cached = ReadCache(cacheDirectory, repository.Url);
                if (cached is not null)
                {
                    try
                    {
                        entries.AddRange(UmbraStoreEntry.ParseRepository(cached, repository));
                        log.Warning($"umbra_repository_fetch_failed_cached url={repository.Url} error={ex.Message}");
                        continue;
                    }
                    catch (Exception cacheException)
                    {
                        log.Warning(
                            $"umbra_repository_cache_invalid url={repository.Url} error={cacheException.Message}");
                    }
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

    public static async Task<IReadOnlyList<UmbraStoreEntry>> FetchRepositoryAsync(
        UmbraRepositorySource repository,
        string cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(cacheDirectory);
        using HttpClient client = CreateClient();
        IReadOnlyList<UmbraStoreEntry> entries = await FetchRepositoryAsync(
            client,
            repository,
            cacheDirectory,
            cancellationToken);
        return entries;
    }

    private static async Task<IReadOnlyList<UmbraStoreEntry>> FetchRepositoryAsync(
        HttpClient client,
        UmbraRepositorySource repository,
        string cacheDirectory,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.GetAsync(
            repository.Url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is long contentLength
            && contentLength > MaximumRepositoryBytes)
        {
            throw new InvalidDataException(
                $"Umbra repository exceeds the {MaximumRepositoryBytes} byte limit.");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream buffer = new();
        await CopyBoundedAsync(stream, buffer, MaximumRepositoryBytes, cancellationToken);
        string json = Encoding.UTF8.GetString(buffer.ToArray());

        // Parse before caching so a malformed response can never replace a known-good index.
        IReadOnlyList<UmbraStoreEntry> entries = UmbraStoreEntry.ParseRepository(json, repository);
        WriteCache(cacheDirectory, repository.Url, json);
        return entries;
    }

    private static HttpClient CreateClient() => new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static async Task CopyBoundedAsync(
        Stream source,
        Stream destination,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[16 * 1024];
        int total = 0;
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                return;

            total += read;
            if (total > maximumBytes)
                throw new InvalidDataException($"Umbra repository exceeds the {maximumBytes} byte limit.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
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
