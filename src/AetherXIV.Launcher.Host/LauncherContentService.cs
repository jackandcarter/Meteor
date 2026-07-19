using AetherXIV.Core;
using AetherXIV.Launcher.Contracts;

namespace AetherXIV.Launcher.Host;

public interface ILauncherContentRepository
{
    ValueTask<LauncherConfig?> GetActiveConfigAsync(CancellationToken cancellationToken = default);

    ValueTask<LauncherStatusRecord?> GetStatusAsync(CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<LauncherNewsItem>> GetNewsItemsAsync(CancellationToken cancellationToken = default);

    ValueTask<LauncherReelPresentation> GetReelPresentationAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(new LauncherReelPresentation(false, []));

    ValueTask<IReadOnlyList<LauncherPatchFile>> GetPatchFilesAsync(
        string targetBootVersion,
        string targetGameVersion,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<RuntimeArtifact>> GetRuntimeArtifactsAsync(
        string platformRid,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<UmbraFrameworkArtifact>> GetUmbraFrameworkArtifactsAsync(
        string platformRid,
        CancellationToken cancellationToken = default);

    ValueTask<UmbraPluginCatalog?> GetUmbraPluginCatalogAsync(CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<UmbraPluginBlock>> GetUmbraPluginBlocksAsync(CancellationToken cancellationToken = default);
}

public sealed record LauncherStatusRecord(string State, string Message);

public sealed record LauncherReelPresentation(bool Enabled, IReadOnlyList<LauncherReelText> Items);

public sealed class LauncherContentService
{
    private readonly ILauncherContentRepository repository;
    private readonly IClock clock;

    public LauncherContentService(
        ILauncherContentRepository repository,
        IClock clock)
    {
        this.repository = repository;
        this.clock = clock;
    }

    public async ValueTask<LauncherConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        return await repository.GetActiveConfigAsync(cancellationToken).ConfigureAwait(false)
            ?? AetherXivLauncherDefaults.LocalConfig;
    }

    public async ValueTask<LauncherStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        LauncherStatusRecord? status = await repository.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        return new LauncherStatus(
            status?.State ?? "online",
            status?.Message ?? "AetherXIV local launcher service is online.",
            clock.UtcNow);
    }

    public async ValueTask<LauncherNewsFeed> GetNewsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<LauncherNewsItem> items = await repository.GetNewsItemsAsync(cancellationToken).ConfigureAwait(false);
        LauncherReelPresentation reelPresentation = await repository.GetReelPresentationAsync(cancellationToken).ConfigureAwait(false);
        return new LauncherNewsFeed(items, reelPresentation.Enabled, reelPresentation.Items);
    }

    public async ValueTask<LauncherPatchManifest> GetPatchManifestAsync(CancellationToken cancellationToken = default)
    {
        LauncherConfig config = await GetConfigAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<LauncherPatchFile> files = await repository.GetPatchFilesAsync(
            config.TargetBootVersion,
            config.TargetGameVersion,
            cancellationToken).ConfigureAwait(false);

        return new LauncherPatchManifest(
            config.TargetBootVersion,
            config.TargetGameVersion,
            config.PatchBaseUrl ?? "",
            files);
    }

    public async ValueTask<RuntimeCatalog> GetRuntimeCatalogAsync(
        string? platformRid,
        CancellationToken cancellationToken = default)
    {
        string platform = NormalizePlatform(platformRid);
        IReadOnlyList<RuntimeArtifact> artifacts = await repository.GetRuntimeArtifactsAsync(platform, cancellationToken)
            .ConfigureAwait(false);
        return new RuntimeCatalog(platform, artifacts);
    }

    public async ValueTask<UmbraFrameworkCatalog> GetUmbraFrameworkCatalogAsync(
        string? platformRid,
        CancellationToken cancellationToken = default)
    {
        string platform = NormalizePlatform(platformRid);
        IReadOnlyList<UmbraFrameworkArtifact> artifacts = await repository.GetUmbraFrameworkArtifactsAsync(
            platform,
            cancellationToken).ConfigureAwait(false);
        return new UmbraFrameworkCatalog(platform, artifacts);
    }

    public async ValueTask<UmbraPluginCatalog> GetUmbraPluginCatalogAsync(CancellationToken cancellationToken = default)
    {
        return await repository.GetUmbraPluginCatalogAsync(cancellationToken).ConfigureAwait(false)
            ?? new UmbraPluginCatalog("AetherXIV Local", []);
    }

    public async ValueTask<UmbraPluginBlocklist> GetUmbraPluginBlocklistAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<UmbraPluginBlock> blocks = await repository.GetUmbraPluginBlocksAsync(cancellationToken)
            .ConfigureAwait(false);
        return new UmbraPluginBlocklist(blocks);
    }

    private static string NormalizePlatform(string? platformRid) => (platformRid ?? "").Trim();
}
