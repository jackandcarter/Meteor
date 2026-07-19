using AetherXIV.Core;
using AetherXIV.Launcher.Contracts;
using AetherXIV.Launcher.Host;

namespace AetherXIV.Launcher.Host.Tests;

public sealed class LauncherContentServiceTests
{
    [Fact]
    public async Task ConfigFallsBackToLocalDefaultsWhenRepositoryIsEmpty()
    {
        LauncherContentService service = CreateService(new FakeLauncherContentRepository());

        LauncherConfig config = await service.GetConfigAsync();

        Assert.Equal("AetherXIV 2 Local", config.ServerName);
        Assert.Equal("2012.09.19.0001", config.TargetGameVersion);
        Assert.Contains("umbra/plugin-catalog", config.PluginCatalogUrls ?? []);
    }

    [Fact]
    public async Task StatusUsesRepositoryMessageAndServiceClock()
    {
        FakeLauncherContentRepository repository = new()
        {
            Status = new LauncherStatusRecord("maintenance", "Preparing local assets.")
        };
        LauncherContentService service = CreateService(repository);

        LauncherStatus status = await service.GetStatusAsync();

        Assert.Equal("maintenance", status.State);
        Assert.Equal("Preparing local assets.", status.Message);
        Assert.Equal(new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero), status.CheckedAt);
    }

    [Fact]
    public async Task PatchManifestUsesActiveConfigAndRepositoryFiles()
    {
        FakeLauncherContentRepository repository = new()
        {
            Config = new LauncherConfig(
                2,
                "Custom Local",
                "status",
                "news",
                "patch-manifest",
                "runtime-catalog",
                "login",
                "create-account",
                "../login/index.php",
                "https://patches.example.test",
                "boot-target",
                "game-target",
                "umbra/framework-catalog",
                ["umbra/plugin-catalog"],
                "umbra/plugin-blocklist"),
            PatchFiles =
            [
                new LauncherPatchFile("ffxiv/game/patch/D2012.patch", 1234, "00abc123", "f".PadLeft(64, '0'))
            ]
        };
        LauncherContentService service = CreateService(repository);

        LauncherPatchManifest manifest = await service.GetPatchManifestAsync();

        Assert.Equal("boot-target", repository.RequestedPatchBootVersion);
        Assert.Equal("game-target", repository.RequestedPatchGameVersion);
        Assert.Equal("https://patches.example.test", manifest.PatchBaseUrl);
        Assert.Single(manifest.Files);
        Assert.Equal("ffxiv/game/patch/D2012.patch", manifest.Files[0].RelativePath);
    }

    [Fact]
    public async Task CatalogsAndNewsComeFromRepository()
    {
        FakeLauncherContentRepository repository = new()
        {
            News =
            [
                new LauncherNewsItem(
                    7,
                    "Runtime service ready",
                    "Catalog endpoints are DB-backed.",
                    null,
                    null,
                    null,
                    new DateTimeOffset(2026, 7, 8, 0, 0, 0, TimeSpan.Zero))
            ],
            ReelPresentation = new LauncherReelPresentation(
                true,
                [
                    new LauncherReelText(
                        "ffxiv-1-1.jpg",
                        "Welcome",
                        "Explore Eorzea",
                        34,
                        18,
                        "#FFFFFFFF",
                        "#FFD7E0EE",
                        true)
                ]),
            RuntimeArtifacts =
            [
                new RuntimeArtifact(
                    "Wine Local",
                    "1.0.0",
                    "osx-arm64",
                    "wine",
                    "https://runtime.example.test/wine.zip",
                    "zip",
                    100,
                    "a".PadLeft(64, '0'),
                    "bin/wine",
                    "win32",
                    new Dictionary<string, string> { ["WINEDEBUG"] = "-all" },
                    true,
                    true,
                    0)
            ],
            UmbraFrameworkArtifacts =
            [
                new UmbraFrameworkArtifact(
                    "Umbra",
                    "2.0.0",
                    "2",
                    "osx-arm64",
                    "https://umbra.example.test/umbra.zip",
                    "zip",
                    200,
                    "b".PadLeft(64, '0'),
                    "Aether.Umbra.Bootstrap.x86.dll",
                    "Aether.Umbra.Framework.dll",
                    ["c".PadLeft(64, '0')],
                    true,
                    true,
                    0)
            ],
            PluginCatalog = new UmbraPluginCatalog(
                "AetherXIV Local",
                [
                    new UmbraPluginCatalogEntry(
                        "trace-companion",
                        "Trace Companion",
                        "1.0.0",
                        "2",
                        "AetherXIV",
                        "Local diagnostics helper.",
                        "https://plugins.example.test/trace.zip",
                        300,
                        "d".PadLeft(64, '0'),
                        "2.0.0",
                        true)
                ]),
            PluginBlocks =
            [
                new UmbraPluginBlock("old-plugin", "umbra/plugin-catalog", null, "Unsupported with 2.0.")
            ]
        };
        LauncherContentService service = CreateService(repository);

        LauncherNewsFeed news = await service.GetNewsAsync();
        RuntimeCatalog runtime = await service.GetRuntimeCatalogAsync("osx-arm64");
        UmbraFrameworkCatalog framework = await service.GetUmbraFrameworkCatalogAsync("osx-arm64");
        UmbraPluginCatalog plugins = await service.GetUmbraPluginCatalogAsync();
        UmbraPluginBlocklist blocklist = await service.GetUmbraPluginBlocklistAsync();

        Assert.Equal("osx-arm64", repository.RequestedRuntimePlatform);
        Assert.Equal("osx-arm64", repository.RequestedUmbraFrameworkPlatform);
        Assert.Equal("Runtime service ready", news.Items[0].Title);
        Assert.True(news.ReelTextEnabled);
        Assert.Equal("ffxiv-1-1.jpg", Assert.Single(news.ReelText!).ImageFile);
        Assert.Equal("Wine Local", runtime.Artifacts[0].Name);
        Assert.Equal("Umbra", framework.Artifacts[0].Name);
        Assert.Equal("trace-companion", plugins.Plugins[0].Id);
        Assert.Equal("old-plugin", blocklist.Blocks[0].PluginKey);
    }

    private static LauncherContentService CreateService(FakeLauncherContentRepository repository)
    {
        return new LauncherContentService(repository, new FixedClock());
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeLauncherContentRepository : ILauncherContentRepository
    {
        public LauncherConfig? Config { get; init; }
        public LauncherStatusRecord? Status { get; init; }
        public IReadOnlyList<LauncherNewsItem> News { get; init; } = [];
        public LauncherReelPresentation ReelPresentation { get; init; } = new(false, []);
        public IReadOnlyList<LauncherPatchFile> PatchFiles { get; init; } = [];
        public IReadOnlyList<RuntimeArtifact> RuntimeArtifacts { get; init; } = [];
        public IReadOnlyList<UmbraFrameworkArtifact> UmbraFrameworkArtifacts { get; init; } = [];
        public UmbraPluginCatalog? PluginCatalog { get; init; }
        public IReadOnlyList<UmbraPluginBlock> PluginBlocks { get; init; } = [];
        public string? RequestedPatchBootVersion { get; private set; }
        public string? RequestedPatchGameVersion { get; private set; }
        public string? RequestedRuntimePlatform { get; private set; }
        public string? RequestedUmbraFrameworkPlatform { get; private set; }

        public ValueTask<LauncherConfig?> GetActiveConfigAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Config);
        }

        public ValueTask<LauncherStatusRecord?> GetStatusAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(Status);
        }

        public ValueTask<IReadOnlyList<LauncherNewsItem>> GetNewsItemsAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(News);
        }

        public ValueTask<LauncherReelPresentation> GetReelPresentationAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ReelPresentation);
        }

        public ValueTask<IReadOnlyList<LauncherPatchFile>> GetPatchFilesAsync(
            string targetBootVersion,
            string targetGameVersion,
            CancellationToken cancellationToken = default)
        {
            RequestedPatchBootVersion = targetBootVersion;
            RequestedPatchGameVersion = targetGameVersion;
            return ValueTask.FromResult(PatchFiles);
        }

        public ValueTask<IReadOnlyList<RuntimeArtifact>> GetRuntimeArtifactsAsync(
            string platformRid,
            CancellationToken cancellationToken = default)
        {
            RequestedRuntimePlatform = platformRid;
            return ValueTask.FromResult(RuntimeArtifacts);
        }

        public ValueTask<IReadOnlyList<UmbraFrameworkArtifact>> GetUmbraFrameworkArtifactsAsync(
            string platformRid,
            CancellationToken cancellationToken = default)
        {
            RequestedUmbraFrameworkPlatform = platformRid;
            return ValueTask.FromResult(UmbraFrameworkArtifacts);
        }

        public ValueTask<UmbraPluginCatalog?> GetUmbraPluginCatalogAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(PluginCatalog);
        }

        public ValueTask<IReadOnlyList<UmbraPluginBlock>> GetUmbraPluginBlocksAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(PluginBlocks);
        }
    }
}
