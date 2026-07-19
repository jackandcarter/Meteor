namespace Aether.Umbra.Framework;

public enum UmbraPluginManagerTab
{
    Installed,
    Supported,
    Available,
    Updates,
    Settings,
    Logs
}

public sealed record UmbraPluginManagerState(
    bool IsOpen,
    UmbraPluginManagerTab ActiveTab,
    UmbraPluginCatalogState Catalog,
    IReadOnlyList<UmbraRepositorySource> RepositorySources,
    bool SafeMode,
    bool DebugLoggingEnabled,
    bool DevUiEnabled,
    bool PluginExecutionEnabled)
{
    internal UmbraThirdPartyPluginHost? RuntimeHost { get; set; }

    public static UmbraPluginManagerState Default => new(
        false,
        UmbraPluginManagerTab.Installed,
        new UmbraPluginCatalogState(
            Array.Empty<UmbraPluginManifest>(),
            Array.Empty<UmbraStoreEntry>(),
            Array.Empty<UmbraStoreEntry>(),
            Array.Empty<UmbraStoreEntry>()),
        Array.Empty<UmbraRepositorySource>(),
        false,
        false,
        false,
        false);

    public IReadOnlyList<UmbraPluginManifest> InstalledPlugins => Catalog.Installed;

    public IReadOnlyList<UmbraStoreEntry> SupportedPlugins => Catalog.Supported;

    public IReadOnlyList<UmbraStoreEntry> AvailablePlugins => Catalog.Available;

    public IReadOnlyList<UmbraStoreEntry> Updates => Catalog.Updates;

    public IReadOnlyList<UmbraPluginRuntimeStatus> RuntimePlugins =>
        RuntimeHost?.Statuses ?? Array.Empty<UmbraPluginRuntimeStatus>();
}
