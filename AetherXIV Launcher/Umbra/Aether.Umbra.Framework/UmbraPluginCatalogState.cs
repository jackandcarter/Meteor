namespace Aether.Umbra.Framework;

public sealed record UmbraPluginCatalogState(
    IReadOnlyList<UmbraPluginManifest> Installed,
    IReadOnlyList<UmbraStoreEntry> Supported,
    IReadOnlyList<UmbraStoreEntry> Available,
    IReadOnlyList<UmbraStoreEntry> Updates)
{
    public static UmbraPluginCatalogState Build(
        IEnumerable<UmbraPluginManifest> installed,
        IEnumerable<UmbraStoreEntry> storeEntries)
    {
        IReadOnlyList<UmbraPluginManifest> installedList = installed
            .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(plugin => plugin.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Dictionary<string, UmbraPluginManifest> installedById = installedList
            .GroupBy(plugin => plugin.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<UmbraStoreEntry> supported = storeEntries
            .Where(entry => entry.Source == UmbraRepositorySource.Supported
                && !entry.IsHidden
                && !entry.TestingOnly
                && IsCompatible(entry))
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(SelectLatest)
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        HashSet<string> supportedIds = supported
            .Select(entry => entry.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<UmbraStoreEntry> available = storeEntries
            .Where(entry => entry.Source == UmbraRepositorySource.Custom
                && !entry.IsHidden
                && !entry.TestingOnly
                && IsCompatible(entry)
                && !supportedIds.Contains(entry.Id))
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(SelectLatest)
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        IReadOnlyList<UmbraStoreEntry> updates = supported
            .Concat(available)
            .Where(entry => installedById.TryGetValue(entry.Id, out UmbraPluginManifest? manifest)
                && IsNewerVersion(entry.Version, manifest.Version))
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Aggregate((best, candidate) =>
                IsNewerVersion(candidate.Version, best.Version) ? candidate : best))
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new UmbraPluginCatalogState(installedList, supported, available, updates);
    }

    private static bool IsNewerVersion(string candidate, string installed)
    {
        if (Version.TryParse(candidate, out Version? candidateVersion)
            && Version.TryParse(installed, out Version? installedVersion))
        {
            return candidateVersion > installedVersion;
        }

        return string.Compare(candidate, installed, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static UmbraStoreEntry SelectLatest(IEnumerable<UmbraStoreEntry> entries)
    {
        return entries
            .OrderByDescending(entry => ParseVersion(entry.Version))
            .ThenByDescending(entry => entry.Version, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.RepositoryUrl, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static Version ParseVersion(string value) =>
        Version.TryParse(value, out Version? parsed) ? parsed : new Version(0, 0);

    private static bool IsCompatible(UmbraStoreEntry entry) =>
        UmbraPluginCompatibility.SupportsApi(entry.ApiVersion)
        && UmbraPluginCompatibility.SupportsFramework(entry.MinimumFrameworkVersion);
}
