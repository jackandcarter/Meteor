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
            .Where(entry => entry.Source == UmbraRepositorySource.Supported && !entry.IsHidden)
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        IReadOnlyList<UmbraStoreEntry> available = storeEntries
            .Where(entry => entry.Source == UmbraRepositorySource.Custom && !entry.IsHidden)
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        IReadOnlyList<UmbraStoreEntry> updates = supported
            .Concat(available)
            .Where(entry => installedById.TryGetValue(entry.Id, out UmbraPluginManifest? manifest)
                && !string.Equals(manifest.Version, entry.Version, StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new UmbraPluginCatalogState(installedList, supported, available, updates);
    }
}
