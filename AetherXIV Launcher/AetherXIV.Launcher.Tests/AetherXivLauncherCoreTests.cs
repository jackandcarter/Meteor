using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using System.Text;
using System.Text.Json;
using System.Net;
using AetherXIV.Launcher.Core;
using FrameworkPluginCatalogState = Aether.Umbra.Framework.UmbraPluginCatalogState;
using FrameworkPluginInstaller = Aether.Umbra.Framework.UmbraPluginInstaller;
using FrameworkRepositorySource = Aether.Umbra.Framework.UmbraRepositorySource;
using FrameworkRuntimeOptions = Aether.Umbra.Framework.UmbraRuntimeOptions;
using FrameworkStoreEntry = Aether.Umbra.Framework.UmbraStoreEntry;

namespace AetherXIV.Launcher.Tests;

public sealed class AetherXivLauncherCoreTests
{
    private static readonly object EnvironmentLock = new();

    [Fact]
    public void ServerXmlWriterIncludesLocalPorts()
    {
        ServerProfile profile = ServerProfile.LocalDefault();

        string xml = ServerXmlWriter.ToXml(new[] { profile });
        XElement root = XDocument.Parse(xml).Root!;
        XElement server = root.Element("Server")!;

        Assert.Equal("Servers", root.Name.LocalName);
        Assert.Equal("Localhost", (string?)server.Attribute("Name"));
        Assert.Equal("127.0.0.1", (string?)server.Attribute("Address"));
        Assert.Equal("http://127.0.0.1:8080/login/index.php", (string?)server.Attribute("LoginUrl"));
    }

    [Fact]
    public void DemiDevUnitDefaultProfileUsesPublicDeveloperServer()
    {
        LauncherProfile profile = LauncherProfile.DemiDevUnitDefault();

        Assert.Equal("https://launcher.dev.demidevunit.com/launcher", profile.LauncherServiceUrl);
        Assert.Equal("Demi Dev Unit Developer Server", profile.ServerProfile.Name);
        Assert.Equal("game.dev.demidevunit.com", profile.ServerProfile.Host);
        Assert.Equal(54994, profile.ServerProfile.LobbyPort);
        Assert.Equal(54992, profile.ServerProfile.WorldPort);
        Assert.Equal(1989, profile.ServerProfile.MapPort);
    }

    [Fact]
    public void StaticActorsLocatorFindsClientScriptFile()
    {
        string root = CreateTempDirectory();
        string scriptPath = Path.Combine(root, "client", "script");
        Directory.CreateDirectory(scriptPath);
        string sourcePath = Path.Combine(scriptPath, StaticActorsLocator.StaticActorsFileName);
        File.WriteAllText(sourcePath, "fixture");

        bool found = StaticActorsLocator.TryFindSource(root, out string result);

        Assert.True(found);
        Assert.Equal(sourcePath, result);
    }

    [Fact]
    public void StaticActorsLocatorFindsNestedPreparedStaticActorsFile()
    {
        string root = CreateTempDirectory();
        string scriptPath = Path.Combine(root, "client", "nested", "script");
        Directory.CreateDirectory(scriptPath);
        string sourcePath = Path.Combine(scriptPath, StaticActorsLocator.PreparedStaticActorsFileName);
        File.WriteAllText(sourcePath, "fixture");

        bool found = StaticActorsLocator.TryFindSource(root, out string result);

        Assert.True(found);
        Assert.Equal(sourcePath, result);
    }

    [Fact]
    public void LaunchPlanCarriesServerAndRuntimeEnvironment()
    {
        string root = CreateTempDirectory();
        string exePath = Path.Combine(root, "ffxivboot.exe");
        File.WriteAllText(exePath, "");

        ClientInstall client = ClientInstall.FromPath(root);
        ServerProfile server = ServerProfile.LocalDefault();
        WineRuntimeProfile runtime = WineRuntimeProfile.WinePrefix("Wine", "/tmp/aetherxiv-prefix");

        LaunchPlan plan = LaunchPlan.Create(client, server, runtime);

        Assert.Contains("ffxivboot.exe", plan.Arguments);
        Assert.Equal("127.0.0.1", plan.Environment["AETHERXIV_SERVER_HOST"]);
        Assert.Equal("/tmp/aetherxiv-prefix", plan.Environment["WINEPREFIX"]);
        Assert.Equal(WineRuntimeProfile.DefaultDirect3DConfig, plan.Environment["WINE_D3D_CONFIG"]);
        Assert.False(string.IsNullOrWhiteSpace(plan.LogPath));
    }

    [Fact]
    public void GameLaunchTokenCarriesSqexArgumentPrefix()
    {
        string sessionId = new('a', 56);

        GameLaunchToken token = GameLaunchTokenGenerator.Generate(sessionId, () => 12345678);
        GameLaunchToken prefixedToken = GameLaunchTokenGenerator.Generate($"sessionId={sessionId}", () => 12345678);

        Assert.Equal(12345678u, token.TickCount);
        Assert.StartsWith(" sqex0002", token.LaunchArgument, StringComparison.Ordinal);
        Assert.EndsWith("!////", token.LaunchArgument, StringComparison.Ordinal);
        Assert.DoesNotContain("+", token.Token, StringComparison.Ordinal);
        Assert.DoesNotContain("/", token.Token, StringComparison.Ordinal);
        Assert.Equal(token.Token, prefixedToken.Token);
    }

    [Fact]
    public void WinePathMapperMapsUnixRootThroughZDrive()
    {
        string mapped = WinePathMapper.ToWindowsPath("/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV/ffxivgame.exe");

        Assert.Equal("Z:\\Volumes\\Dev2\\SquareEnix\\FINAL FANTASY XIV\\ffxivgame.exe", mapped);
    }

    [Fact]
    public void ClientLaunchHelperLocatorHonorsLaunchHelperMode()
    {
        string root = CreateTempDirectory();
        string x86Directory = Path.Combine(root, "Helpers", "win-x86");
        string x64Directory = Path.Combine(root, "Helpers", "win-x64");
        string arm64Directory = Path.Combine(root, "Helpers", "win-arm64");
        Directory.CreateDirectory(x86Directory);
        Directory.CreateDirectory(x64Directory);
        Directory.CreateDirectory(arm64Directory);

        string x86Helper = Path.Combine(x86Directory, "AetherXIV.Launcher.ClientLauncher.exe");
        string x64Helper = Path.Combine(x64Directory, "AetherXIV.Launcher.ClientLauncher.exe");
        string arm64Helper = Path.Combine(arm64Directory, "AetherXIV.Launcher.ClientLauncher.exe");
        File.WriteAllText(x86Helper, "");
        File.WriteAllText(x64Helper, "");
        File.WriteAllText(arm64Helper, "");

        Assert.Equal(x64Helper, ClientLaunchHelperLocator.Find(root));
        Assert.Equal(x64Helper, ClientLaunchHelperLocator.FindLaunchHelper(root));
        Assert.Equal(x64Helper, ClientLaunchHelperLocator.FindLaunchHelper(ClientLaunchHelperMode.Automatic, root));
        Assert.Equal(x86Helper, ClientLaunchHelperLocator.FindLaunchHelper(ClientLaunchHelperMode.X86, root));
        Assert.Equal(x64Helper, ClientLaunchHelperLocator.FindLaunchHelper(ClientLaunchHelperMode.X64, root));
        Assert.Equal(arm64Helper, ClientLaunchHelperLocator.FindLaunchHelper(ClientLaunchHelperMode.Arm64, root));
    }

    [Fact]
    public void LaunchPlanWithHelperCarriesSessionAndMappedGamePath()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ffxivgame.exe"), "");
        ClientInstall client = ClientInstall.FromPath(root);
        ServerProfile server = ServerProfile.LocalDefault();
        WineRuntimeProfile runtime = WineRuntimeProfile.WinePrefix("Wine", "/tmp/aetherxiv-prefix");
        string helper = Path.Combine(root, "AetherXIV.Launcher.ClientLauncher.exe");

        LaunchPlan plan = LaunchPlan.CreateWithHelper(
            client,
            server,
            runtime,
            helper,
            new string('b', 56),
            mapClientPathsForWine: true,
            logPath: Path.Combine(root, "launch.log"));

        Assert.Equal(helper, plan.WindowsExecutablePath);
        Assert.DoesNotContain("explorer", plan.Arguments);
        Assert.DoesNotContain("/desktop=", plan.Arguments);
        Assert.Contains("Z:", plan.Arguments);
        Assert.Contains("AetherXIV.Launcher.ClientLauncher.exe", plan.Arguments);
        Assert.Contains("--session", plan.Arguments);
        Assert.Contains("--observe-seconds 15", plan.Arguments);
        Assert.Contains("127.0.0.1", plan.Arguments);
        Assert.Equal(Path.Combine(root, "launch.helper.log"), plan.HelperLogPath);
        Assert.Equal("/tmp/aetherxiv-prefix", plan.Environment["WINEPREFIX"]);
        Assert.Equal(WineRuntimeProfile.DefaultDirect3DConfig, plan.Environment["WINE_D3D_CONFIG"]);
    }

    [Fact]
    public void LaunchPlanWithHelperKeepsWindowsNativeArgumentsDirect()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ffxivgame.exe"), "");
        ClientInstall client = ClientInstall.FromPath(root);
        ServerProfile server = ServerProfile.LocalDefault();
        WineRuntimeProfile runtime = WineRuntimeProfile.NativeWindows();
        string helper = Path.Combine(root, "AetherXIV.Launcher.ClientLauncher.exe");

        LaunchPlan plan = LaunchPlan.CreateWithHelper(
            client,
            server,
            runtime,
            helper,
            new string('b', 56),
            mapClientPathsForWine: false);

        Assert.DoesNotContain("wine", plan.Arguments, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/desktop=", plan.Arguments);
        Assert.Equal(helper, plan.WindowsExecutablePath);
        Assert.DoesNotContain("Z:", plan.Arguments);
        Assert.Contains("--session", plan.Arguments);
        Assert.Contains("--observe-seconds 15", plan.Arguments);
    }

    [Fact]
    public void LaunchPlanWithHelperCarriesUmbraArgumentsAndMapsWinePaths()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ffxivgame.exe"), "");
        ClientInstall client = ClientInstall.FromPath(root);
        ServerProfile server = ServerProfile.LocalDefault();
        WineRuntimeProfile runtime = WineRuntimeProfile.WinePrefix("Wine", "/tmp/aetherxiv-prefix");
        string helper = Path.Combine(root, "AetherXIV.Launcher.ClientLauncher.exe");
        UmbraLaunchOptions umbra = new(
            true,
            false,
            500,
            Path.Combine(root, "Umbra", "Framework", "Aether.Umbra.Bootstrap.x86.dll"),
            Path.Combine(root, "Umbra", "Framework", "Managed", "Aether.Umbra.Framework.dll"),
            Path.Combine(root, "Umbra", "Plugins"),
            Path.Combine(root, "Umbra", "Logs", "umbra.log"),
            new[] { "https://launcher.dev.demidevunit.com/umbra/plugins.json" },
            EnableManagedOnWine: true)
        {
            RepositorySources = new[]
            {
                new UmbraRepositorySource(
                    "https://launcher.dev.demidevunit.com/umbra/plugins.json",
                    UmbraRepositorySource.Supported)
            }
        };

        LaunchPlan plan = LaunchPlan.CreateWithHelper(
            client,
            server,
            runtime,
            helper,
            new string('b', 56),
            mapClientPathsForWine: true,
            logPath: Path.Combine(root, "launch.log"),
            umbraOptions: umbra);

        Assert.True(plan.Umbra.Enabled);
        Assert.Contains("--umbra-enabled true", plan.Arguments);
        Assert.Contains("--umbra-bootstrap", plan.Arguments);
        Assert.Contains("Z:", plan.Arguments);
        Assert.Contains("Aether.Umbra.Bootstrap.x86.dll", plan.Arguments);
        Assert.Contains("--umbra-load-delay-ms 500", plan.Arguments);
        Assert.Contains("--umbra-repository-urls", plan.Arguments);
        Assert.Contains("--umbra-repositories-json", plan.Arguments);
        Assert.Contains("--umbra-enable-managed-on-wine true", plan.Arguments);
        Assert.Contains("source", plan.Arguments);
    }

    [Fact]
    public void LauncherProfileCarriesUmbraSettings()
    {
        LauncherProfile profile = LauncherProfile.LocalDefault() with
        {
            Umbra = new UmbraSettings
            {
                Enabled = true,
                LoadDelayMilliseconds = UmbraSettings.MaximumLoadDelayMilliseconds + 1,
                CustomRepositoryUrls = new[] { "https://example.com/umbra/index.json" }
            }
        };

        UmbraSettings settings = profile.EffectiveUmbra;

        Assert.True(settings.Enabled);
        Assert.Equal(UmbraSettings.MaximumLoadDelayMilliseconds, settings.LoadDelayMilliseconds);
        Assert.Contains("https://example.com/umbra/index.json", settings.CustomRepositoryUrls);
    }

    [Fact]
    public void UmbraRepositoryOptionsAllowHttpsAndLocalhostOnly()
    {
        IReadOnlyList<string> urls = UmbraRepositoryOptions.NormalizeCustomRepositoryUrls(new[]
        {
            "https://example.com/umbra/index.json",
            "http://localhost:8080/umbra/index.json",
            "http://127.0.0.1:8080/umbra/index.json"
        });

        Assert.Equal(3, urls.Count);
        Assert.Throws<ArgumentException>(() =>
            UmbraRepositoryOptions.NormalizeCustomRepositoryUrls(new[] { "http://example.com/umbra/index.json" }));
    }

    [Fact]
    public void UmbraRepositoryOptionsResolvesServiceRelativeSupportedCatalogs()
    {
        IReadOnlyList<UmbraRepositorySource> sources = UmbraRepositoryOptions.BuildEffectiveRepositorySources(
            new UmbraSettings { UseOfficialRepository = true },
            new[] { "umbra/plugin-catalog" },
            "http://127.0.0.1:8080/launcher");

        UmbraRepositorySource source = Assert.Single(sources);
        Assert.Equal("http://127.0.0.1:8080/launcher/umbra/plugin-catalog", source.Url);
        Assert.Equal(UmbraRepositorySource.Supported, source.Source);
    }

    [Fact]
    public void UmbraRepositoryOptionsKeepsCustomRepositoriesAbsolute()
    {
        Assert.Throws<ArgumentException>(() =>
            UmbraRepositoryOptions.NormalizeCustomRepositoryUrls(new[] { "umbra/plugin-catalog" }));
    }

    [Fact]
    public void UmbraRepositoryOptionsLabelsSupportedAndCustomSources()
    {
        UmbraSettings settings = new()
        {
            UseOfficialRepository = true,
            CustomRepositoryUrls = new[] { "https://example.com/custom.json" }
        };

        IReadOnlyList<UmbraRepositorySource> sources = UmbraRepositoryOptions.BuildEffectiveRepositorySources(
            settings,
            new[] { "https://launcher.example.com/supported.json" });

        Assert.Equal(2, sources.Count);
        Assert.Contains(sources, source =>
            source.Url == "https://launcher.example.com/supported.json"
            && source.Source == UmbraRepositorySource.Supported);
        Assert.Contains(sources, source =>
            source.Url == "https://example.com/custom.json"
            && source.Source == UmbraRepositorySource.Custom);
    }

    [Fact]
    public void UmbraStoreEntryParsesDalamudStyleAliases()
    {
        string json = """
            [
              {
                "InternalName": "ExamplePlugin",
                "Name": "Example Plugin",
                "AssemblyVersion": "1.2.3",
                "DalamudApiLevel": 10,
                "DownloadLinkInstall": "https://example.com/plugin.zip",
                "SizeBytes": 1234,
                "Sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "MinimumFrameworkVersion": "0.1.0",
                "IconUrl": "https://example.com/icon.png",
                "ImageUrls": ["https://example.com/one.png"]
              }
            ]
            """;

        IReadOnlyList<FrameworkStoreEntry> entries = FrameworkStoreEntry.ParseRepository(
            json,
            new FrameworkRepositorySource("https://example.com/repo.json", FrameworkRepositorySource.Supported));

        FrameworkStoreEntry entry = Assert.Single(entries);
        Assert.Equal("ExamplePlugin", entry.Id);
        Assert.Equal("1.2.3", entry.Version);
        Assert.Equal("10", entry.ApiVersion);
        Assert.True(entry.IsInstallable);
        Assert.Equal(FrameworkRepositorySource.Supported, entry.Source);
        Assert.Equal("https://example.com/icon.png", entry.IconUrl);
        Assert.Single(entry.ImageUrls);
    }

    [Fact]
    public void UmbraStoreEntryParsesAetherCatalogEnvelope()
    {
        string json = """
            {
              "repository_name": "AetherXIV Supported",
              "plugins": [
                {
                  "id": "dev.envelope",
                  "name": "Envelope Plugin",
                  "version": "1.0.0",
                  "api_version": "2.0",
                  "download_url": "https://example.com/plugin.zip",
                  "size_bytes": 1234,
                  "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                  "minimum_framework_version": "0.1.0"
                }
              ]
            }
            """;

        FrameworkStoreEntry entry = Assert.Single(FrameworkStoreEntry.ParseRepository(
            json,
            new FrameworkRepositorySource("https://example.com/repo.json", FrameworkRepositorySource.Supported)));

        Assert.Equal("dev.envelope", entry.Id);
        Assert.Equal(FrameworkRepositorySource.Supported, entry.Source);
        Assert.True(entry.IsInstallable);
    }

    [Fact]
    public void UmbraStoreEntryRejectsInstallWhenIntegrityMetadataIsMissing()
    {
        string json = """
            [
              {
                "id": "dev.nohash",
                "name": "No Hash",
                "version": "1.0.0",
                "api_version": "1.0",
                "download_url": "https://example.com/plugin.zip",
                "minimum_framework_version": "0.1.0"
              }
            ]
            """;

        Assert.Throws<InvalidDataException>(() => FrameworkStoreEntry.ParseRepository(
            json,
            new FrameworkRepositorySource("https://example.com/repo.json", FrameworkRepositorySource.Custom)));
    }

    [Fact]
    public void UmbraPluginInstallerValidatesArchiveAndRequiresMatchingManifest()
    {
        byte[] archive = CreateRuntimeArchive(
            ("ExamplePlugin.dll", Encoding.UTF8.GetBytes("placeholder")),
            ("umbra-plugin.json", Encoding.UTF8.GetBytes("""
                {
                  "id": "dev.example",
                  "name": "dev.example",
                  "version": "1.0.0",
                  "api_version": "2.0",
                  "entry": "ExamplePlugin.dll",
                  "minimum_framework_version": "0.1.0",
                  "enabled": false
                }
                """)));
        string archivePath = Path.Combine(CreateTempDirectory(), "plugin.zip");
        File.WriteAllBytes(archivePath, archive);
        string pluginDirectory = CreateTempDirectory();
        FrameworkStoreEntry entry = CreateStoreEntry(archive, "dev.example");

        Aether.Umbra.Framework.UmbraPluginInstallResult result =
            FrameworkPluginInstaller.InstallVerifiedArchive(entry, archivePath, pluginDirectory);

        Assert.Equal("dev.example", result.PluginId);
        Assert.True(File.Exists(result.ManifestPath));
        Assert.True(File.Exists(Path.Combine(result.InstallDirectory, "ExamplePlugin.dll")));
    }

    [Fact]
    public void UmbraPluginInstallerRejectsEscapingArchivePaths()
    {
        byte[] archive = CreateRuntimeArchive(("../escape.txt", Encoding.UTF8.GetBytes("bad")));
        string archivePath = Path.Combine(CreateTempDirectory(), "plugin.zip");
        File.WriteAllBytes(archivePath, archive);
        FrameworkStoreEntry entry = CreateStoreEntry(archive, "dev.escape");

        Assert.Throws<InvalidDataException>(() =>
            FrameworkPluginInstaller.InstallVerifiedArchive(entry, archivePath, CreateTempDirectory()));
    }

    [Fact]
    public void UmbraPluginInstallerStagesUpdatePreservesEnabledStateAndArchivesPreviousVersion()
    {
        byte[] archive = CreateRuntimeArchive(
            ("ExamplePlugin.dll", Encoding.UTF8.GetBytes("new")),
            ("umbra-plugin.json", Encoding.UTF8.GetBytes("""
                {
                  "id": "dev.example",
                  "name": "dev.example",
                  "version": "1.0.0",
                  "api_version": "2.0",
                  "entry": "ExamplePlugin.dll",
                  "minimum_framework_version": "0.1.0",
                  "enabled": false
                }
                """)));
        string root = CreateTempDirectory();
        string pluginDirectory = Path.Combine(root, "Plugins");
        string installDirectory = Path.Combine(pluginDirectory, "dev.example");
        string backupDirectory = Path.Combine(root, "PluginBackups");
        Directory.CreateDirectory(installDirectory);
        File.WriteAllText(Path.Combine(installDirectory, "ExamplePlugin.dll"), "old");
        File.WriteAllText(Path.Combine(installDirectory, "umbra-plugin.json"), """
            {
              "id": "dev.example",
              "name": "dev.example",
              "version": "0.9.0",
              "api_version": "1.0",
              "entry": "ExamplePlugin.dll",
              "minimum_framework_version": "0.1.0",
              "enabled": true
            }
            """);
        string archivePath = Path.Combine(root, "plugin.zip");
        File.WriteAllBytes(archivePath, archive);

        Aether.Umbra.Framework.UmbraPluginInstallResult result =
            FrameworkPluginInstaller.InstallVerifiedArchive(
                CreateStoreEntry(archive, "dev.example"),
                archivePath,
                pluginDirectory,
                backupDirectory);

        UmbraPluginManifest installed = UmbraPluginManifest.Load(result.ManifestPath);
        Assert.True(installed.Enabled);
        Assert.Equal("new", File.ReadAllText(Path.Combine(result.InstallDirectory, "ExamplePlugin.dll")));
        Assert.NotNull(result.BackupDirectory);
        Assert.Equal("old", File.ReadAllText(Path.Combine(result.BackupDirectory!, "ExamplePlugin.dll")));
    }

    [Fact]
    public void UmbraPluginCatalogStateSeparatesInstalledSupportedAvailableAndUpdates()
    {
        Aether.Umbra.Framework.UmbraPluginManifest installed = new(
            "dev.example",
            "Example",
            "1.0.0",
            "2.0",
            "Example.dll",
            "0.1.0",
            true);
        FrameworkStoreEntry supported = CreateStoreEntry(Array.Empty<byte>(), "dev.example") with
        {
            Version = "1.1.0",
            Source = FrameworkRepositorySource.Supported
        };
        FrameworkStoreEntry custom = CreateStoreEntry(Array.Empty<byte>(), "dev.custom") with
        {
            Source = FrameworkRepositorySource.Custom
        };

        FrameworkPluginCatalogState state = FrameworkPluginCatalogState.Build(
            new[] { installed },
            new[] { custom, supported });

        Assert.Single(state.Installed);
        Assert.Single(state.Supported);
        Assert.Single(state.Available);
        Assert.Single(state.Updates);
        Assert.Equal("dev.example", state.Updates[0].Id);
    }

    [Fact]
    public void UmbraPluginManifestRejectsEscapingEntry()
    {
        UmbraPluginManifest manifest = new(
            "dev.bad",
            "Bad",
            "0.1.0",
            "1.0",
            "../Bad.dll",
            "0.1.0",
            false);

        Assert.Throws<InvalidDataException>(() => manifest.Validate());
    }

    [Fact]
    public void UmbraPluginManifestAllowsDottedAssemblyNames()
    {
        UmbraPluginManifest manifest = new(
            "dev.good",
            "Good",
            "0.1.0",
            "1.0",
            "Company.Plugin.dll",
            "0.1.0",
            true);

        manifest.Validate();
    }

    [Fact]
    public void UmbraRuntimeOptionsUseOnlyAetherEnvironmentNames()
    {
        string[] keys =
        [
            "AETHER_UMBRA_LOG",
            "AETHER_UMBRA_PLUGIN_DIR",
            "AETHER_UMBRA_CACHE_DIR",
            "AETHER_UMBRA_DEV_BRIDGE",
            "AETHER_UMBRA_DEV_BRIDGE_PORT"
        ];

        lock (EnvironmentLock)
        {
            Dictionary<string, string?> old = CaptureEnvironment(keys);
            try
            {
                foreach (string key in keys)
                    Environment.SetEnvironmentVariable(key, null);

                string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
                Environment.SetEnvironmentVariable("AETHER_UMBRA_LOG", Path.Combine(root, "aether.log"));
                Environment.SetEnvironmentVariable("AETHER_UMBRA_PLUGIN_DIR", Path.Combine(root, "Plugins"));
                Environment.SetEnvironmentVariable("AETHER_UMBRA_CACHE_DIR", Path.Combine(root, "Cache"));
                Environment.SetEnvironmentVariable("AETHER_UMBRA_DEV_BRIDGE", "0");
                Environment.SetEnvironmentVariable("AETHER_UMBRA_DEV_BRIDGE_PORT", "8799");

                FrameworkRuntimeOptions options = FrameworkRuntimeOptions.FromEnvironment();

                Assert.EndsWith(Path.Combine(root, "aether.log"), options.LogPath);
                Assert.EndsWith(Path.Combine(root, "Plugins"), options.PluginDirectory);
                Assert.EndsWith(Path.Combine(root, "Cache"), options.CacheDirectory);
                Assert.EndsWith(Path.Combine(root, "Cache", "DevBridge"), options.DevBridgeDirectory);
                Assert.False(options.DevBridgeInitiallyEnabled);
                Assert.Equal(8799, options.DevBridgePort);
            }
            finally
            {
                RestoreEnvironment(old);
            }
        }
    }

    [Fact]
    public void WineRuntimeConfiguratorBuildsMacRegistrySettings()
    {
        IReadOnlyList<WineRegistrySetting> settings = WineRuntimeConfigurator.BuildRegistrySettings(
            new WineRuntimeConfigurationSettings(LauncherOperatingSystem.MacOS));

        Assert.DoesNotContain(settings, setting => setting.Key == @"HKCU\Software\Wine\Explorer\Desktops");
        Assert.Contains(settings, setting =>
            setting.Key == @"HKCU\Software\Wine\DirectInput"
            && setting.ValueName == "MouseWarpOverride"
            && setting.Data == WineRuntimeConfigurator.MouseWarpOverrideDefault);
        Assert.Contains(settings, setting =>
            setting.Key == @"HKCU\Software\Wine\Mac Driver"
            && setting.ValueName == "CaptureDisplaysForFullscreen"
            && setting.Data == "y");
    }

    [Fact]
    public void WineRuntimeConfiguratorBuildsLinuxRegistrySettings()
    {
        IReadOnlyList<WineRegistrySetting> settings = WineRuntimeConfigurator.BuildRegistrySettings(
            new WineRuntimeConfigurationSettings(LauncherOperatingSystem.Linux));

        Assert.DoesNotContain(settings, setting => setting.Key == @"HKCU\Software\Wine\Explorer\Desktops");
        Assert.Contains(settings, setting =>
            setting.Key == @"HKCU\Software\Wine\X11 Driver"
            && setting.ValueName == "GrabFullscreen"
            && setting.Data == "Y");
    }

    [Fact]
    public void WineRuntimeConfiguratorBuildsPrefixLocalDocumentsRegistrySettings()
    {
        IReadOnlyList<WineRegistrySetting> settings = WineRuntimeConfigurator.BuildRegistrySettings(
            new WineRuntimeConfigurationSettings(LauncherOperatingSystem.MacOS),
            @"C:\users\imac\AetherXIV Documents");

        Assert.Contains(settings, setting =>
            setting.Key == @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"
            && setting.ValueName == "Personal"
            && setting.Type == "REG_SZ"
            && setting.Data == @"C:\users\imac\AetherXIV Documents");
        Assert.Contains(settings, setting =>
            setting.Key == @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders"
            && setting.ValueName == "Personal"
            && setting.Type == "REG_EXPAND_SZ"
            && setting.Data == @"C:\users\imac\AetherXIV Documents");
    }

    [Fact]
    public void WineRuntimeConfiguratorCreatesPrefixLocalFfxivConfigStorage()
    {
        string prefix = CreateTempDirectory();
        string userRoot = Path.Combine(prefix, "drive_c", "users", "testuser");
        Directory.CreateDirectory(userRoot);

        bool created = WineRuntimeConfigurator.TryCreatePrefixLocalDocuments(
            prefix,
            out WineUserDocumentsTarget target,
            out string error);

        Assert.True(created, error);
        Assert.Equal(Path.Combine(userRoot, "AetherXIV Documents"), target.HostDocumentsPath);
        Assert.Equal(@"C:\users\testuser\AetherXIV Documents", target.WindowsDocumentsPath);
        Assert.True(Directory.Exists(target.HostFfxivConfigPath));
        Assert.EndsWith(
            Path.Combine("AetherXIV Documents", "My Games", "FINAL FANTASY XIV"),
            target.HostFfxivConfigPath,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FfxivSettingsStoreResolvesWineConfigStorage()
    {
        string prefix = CreateTempDirectory();
        string userRoot = Path.Combine(prefix, "drive_c", "users", "testuser");
        Directory.CreateDirectory(userRoot);
        WineRuntimeProfile profile = WineRuntimeProfile.WinePrefix("Wine", prefix);

        bool resolved = FfxivClientSettingsStore.TryResolveWineTarget(
            profile,
            managedPrefixPath: "",
            out FfxivConfigStorageTarget target,
            out string error);

        Assert.True(resolved, error);
        Assert.Equal(Path.Combine(userRoot, "AetherXIV Documents"), target.HostDocumentsPath);
        Assert.Equal(@"C:\users\testuser\AetherXIV Documents", target.WindowsDocumentsPath);
        Assert.EndsWith(
            Path.Combine("AetherXIV Documents", "My Games", "FINAL FANTASY XIV"),
            target.HostConfigDirectoryPath,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FfxivSettingsStoreWritesLanguageAndCreatesSystemConfig()
    {
        string root = CreateTempDirectory();
        string configPath = Path.Combine(root, "ffxivconfig.exe");
        byte[] systemConfig = Enumerable.Range(0, FfxivClientSettingsStore.SystemConfigLength)
            .Select(index => (byte)(index % 251 + 1))
            .ToArray();
        File.WriteAllBytes(configPath, CreateMinimalConfigExecutable(systemConfig));
        ClientInstall client = ClientInstall.FromPath(root);
        string configDirectory = Path.Combine(CreateTempDirectory(), "My Games", "FINAL FANTASY XIV");

        FfxivSettingsSaveResult result = FfxivClientSettingsStore.Save(
            client,
            configDirectory,
            new FfxivClientSettings(FfxivClientLanguage.German),
            repairSystemConfig: true);

        byte[] language = File.ReadAllBytes(Path.Combine(configDirectory, "config.lng"));
        byte[] savedSystemConfig = File.ReadAllBytes(Path.Combine(configDirectory, "config.sys"));
        Assert.Equal(8, language.Length);
        Assert.Equal((int)FfxivClientLanguage.German, BitConverter.ToInt32(language, 4));
        Assert.NotEqual(systemConfig, savedSystemConfig);
        Assert.Equal(FfxivSystemConfig.Magic, BitConverter.ToUInt32(savedSystemConfig, 0));
        Assert.Equal(1280, BitConverter.ToInt32(savedSystemConfig, 0x14));
        Assert.Equal(720, BitConverter.ToInt32(savedSystemConfig, 0x18));
        Assert.True(result.CreatedSystemConfig);
        Assert.False(result.RepairedSystemConfig);
    }

    [Fact]
    public void FfxivSettingsStoreWritesGraphicsSettings()
    {
        string root = CreateTempDirectory();
        string configPath = Path.Combine(root, "ffxivconfig.exe");
        byte[] systemConfig = Enumerable.Range(0, FfxivClientSettingsStore.SystemConfigLength)
            .Select(index => (byte)(index % 251 + 1))
            .ToArray();
        File.WriteAllBytes(configPath, CreateMinimalConfigExecutable(systemConfig));
        ClientInstall client = ClientInstall.FromPath(root);
        string configDirectory = Path.Combine(CreateTempDirectory(), "My Games", "FINAL FANTASY XIV");

        FfxivSystemConfig graphics = new(
            FfxivDisplayMode.Fullscreen,
            1920,
            1080,
            FfxivShadowMapQuality.High,
            TextureQualityIndex: 0,
            BackgroundQualityIndex: 1,
            FfxivFrameRateLimit.Fps30);

        FfxivClientSettingsStore.Save(
            client,
            configDirectory,
            new FfxivClientSettings(FfxivClientLanguage.English, graphics),
            repairSystemConfig: true);

        byte[] savedSystemConfig = File.ReadAllBytes(Path.Combine(configDirectory, "config.sys"));
        Assert.Equal(FfxivSystemConfig.Magic, BitConverter.ToUInt32(savedSystemConfig, 0));
        Assert.Equal((int)FfxivDisplayMode.Fullscreen, BitConverter.ToInt32(savedSystemConfig, 0x10));
        Assert.Equal(1920, BitConverter.ToInt32(savedSystemConfig, 0x14));
        Assert.Equal(1080, BitConverter.ToInt32(savedSystemConfig, 0x18));
        Assert.Equal((int)FfxivShadowMapQuality.High, BitConverter.ToInt32(savedSystemConfig, 0x20));
        Assert.Equal(0, BitConverter.ToInt32(savedSystemConfig, 0x3c));
        Assert.Equal(1, BitConverter.ToInt32(savedSystemConfig, 0x40));
        Assert.Equal((int)FfxivFrameRateLimit.Fps30, BitConverter.ToInt32(savedSystemConfig, 0x44));
    }

    [Fact]
    public void FfxivSettingsStoreRejectsInvalidSystemConfigMagic()
    {
        string configDirectory = CreateTempDirectory();
        string systemPath = Path.Combine(configDirectory, "config.sys");
        File.WriteAllBytes(systemPath, new byte[FfxivClientSettingsStore.SystemConfigLength]);

        Assert.False(FfxivClientSettingsStore.IsUsableSystemConfig(systemPath));
    }

    [Fact]
    public void WineRuntimeConfiguratorQuotesRegistryArguments()
    {
        WineRegistrySetting setting = new(
            @"HKCU\Software\Wine\DirectInput",
            "MouseWarpOverride",
            "REG_SZ",
            WineRuntimeConfigurator.MouseWarpOverrideDefault);

        string arguments = WineRuntimeConfigurator.BuildRegAddArguments(setting);

        Assert.Contains("reg add", arguments);
        Assert.Contains(@"HKCU\Software\Wine\DirectInput", arguments);
        Assert.Contains("/v MouseWarpOverride", arguments);
        Assert.Contains($"/d {WineRuntimeConfigurator.MouseWarpOverrideDefault}", arguments);
    }

    [Fact]
    public void WineRuntimeConfiguratorBuildsRegistryDeleteValueArguments()
    {
        string arguments = WineRuntimeConfigurator.BuildRegDeleteValueArguments(
            @"HKCU\Software\Wine\Explorer\Desktops",
            "EchoGateXIV-1920x1080");

        Assert.Contains("reg delete", arguments);
        Assert.Contains(@"HKCU\Software\Wine\Explorer\Desktops", arguments);
        Assert.Contains("/v EchoGateXIV-1920x1080", arguments);
        Assert.Contains("/f", arguments);
    }

    [Fact]
    public void WineRuntimeConfiguratorParsesOnlyLegacyDesktopValues()
    {
        string queryOutput = """

            HKEY_CURRENT_USER\Software\Wine\Explorer\Desktops
                EchoGateXIV-1600x900    REG_SZ    1600x900
                OtherGame               REG_SZ    1024x768
                EchoGateXIV-1920x1080   REG_SZ    1920x1080
            """;

        IReadOnlyList<string> valueNames = WineRuntimeConfigurator.ParseLegacyDesktopValueNames(queryOutput);

        Assert.Equal(new[] { "EchoGateXIV-1600x900", "EchoGateXIV-1920x1080" }, valueNames);
    }

    [Fact]
    public void WinePrefixPreservesExplicitDirect3DConfig()
    {
        WineRuntimeProfile runtime = WineRuntimeProfile.WinePrefix(
            "Wine",
            "/tmp/aetherxiv-prefix",
            environment: new Dictionary<string, string>
            {
                ["WINE_D3D_CONFIG"] = "renderer=vulkan"
            });

        Assert.Equal("renderer=vulkan", runtime.Environment["WINE_D3D_CONFIG"]);
    }

    [Fact]
    public void WineRuntimeProfileAppliesGraphicsTargets()
    {
        WineRuntimeProfile runtime = WineRuntimeProfile.WinePrefix("Wine", "/tmp/aetherxiv-prefix");

        Assert.Equal(
            WineRuntimeProfile.DefaultDirect3DConfig,
            runtime.WithGraphicsTarget(ClientGraphicsTarget.OpenGLCompatibility).Environment["WINE_D3D_CONFIG"]);
        Assert.Equal(
            WineRuntimeProfile.OpenGLThreadedDirect3DConfig,
            runtime.WithGraphicsTarget(ClientGraphicsTarget.OpenGLThreaded).Environment["WINE_D3D_CONFIG"]);
        Assert.Equal(
            WineRuntimeProfile.VulkanDirect3DConfig,
            runtime.WithGraphicsTarget(ClientGraphicsTarget.WineD3DVulkan).Environment["WINE_D3D_CONFIG"]);
        Assert.False(runtime.WithGraphicsTarget(ClientGraphicsTarget.WineDefault).Environment.ContainsKey("WINE_D3D_CONFIG"));
        Assert.Equal("renderer=gl", WineRuntimeProfile.DefaultDirect3DConfig);
        Assert.DoesNotContain("csmt", WineRuntimeProfile.DefaultDirect3DConfig, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WhiskyRuntimeArgumentsSeparateHelperArguments()
    {
        WineRuntimeProfile runtime = WineRuntimeProfile.WhiskyBottle(
            "Whisky - wow",
            "wow",
            "/Applications/Whisky.app/Contents/Resources/WhiskyCmd");

        string arguments = runtime.BuildArguments(
            "/path/AetherXIV.Launcher.ClientLauncher.exe",
            "--probe");

        Assert.Contains("run wow", arguments);
        Assert.Contains("/path/AetherXIV.Launcher.ClientLauncher.exe -- --probe", arguments);
    }

    [Fact]
    public void ClientInstallReportClassifiesBaseInstall()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ffxivboot.exe"), "");
        File.WriteAllText(Path.Combine(root, "ffxivupdater.exe"), "");
        File.WriteAllText(Path.Combine(root, "boot.ver"), ClientVersionInfo.BaseVersion);
        File.WriteAllText(Path.Combine(root, "game.ver"), ClientVersionInfo.BaseVersion);

        ClientInstallReport report = ClientInstall.FromPath(root).Inspect();

        Assert.Equal(ClientInstallState.BaseInstall, report.State);
        Assert.False(report.HasDirectGameExecutable);
        Assert.Contains(report.RequiredActions, action => action.Contains("patch chain", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ClientInstallReportClassifiesTargetInstall()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ffxivboot.exe"), "");
        File.WriteAllText(Path.Combine(root, "ffxivupdater.exe"), "");
        File.WriteAllText(Path.Combine(root, "ffxivgame.exe"), "");
        File.WriteAllText(Path.Combine(root, "boot.ver"), ClientVersionInfo.TargetBootVersion);
        File.WriteAllText(Path.Combine(root, "game.ver"), ClientVersionInfo.TargetGameVersion);
        Directory.CreateDirectory(Path.Combine(root, "client", "script"));
        File.WriteAllText(Path.Combine(root, "client", "script", StaticActorsLocator.StaticActorsFileName), "fixture");

        ClientInstallReport report = ClientInstall.FromPath(root).Inspect();

        Assert.Equal(ClientInstallState.Ready123b, report.State);
        Assert.True(report.IsLaunchReady);
    }

    [Fact]
    public void ClientInstallReportRequiresStaticActorsForLaunchReadiness()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "ffxivboot.exe"), "");
        File.WriteAllText(Path.Combine(root, "ffxivupdater.exe"), "");
        File.WriteAllText(Path.Combine(root, "ffxivgame.exe"), "");
        File.WriteAllText(Path.Combine(root, "boot.ver"), ClientVersionInfo.TargetBootVersion);
        File.WriteAllText(Path.Combine(root, "game.ver"), ClientVersionInfo.TargetGameVersion);

        ClientInstallReport report = ClientInstall.FromPath(root).Inspect();

        Assert.Equal(ClientInstallState.Ready123b, report.State);
        Assert.False(report.IsLaunchReady);
        Assert.Contains(report.RequiredActions, action => action.Contains("staticactors.bin", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LegacyPatchManifestMatchesKnownPatchChain()
    {
        IReadOnlyList<PatchEntry> entries = LegacyPatchManifest.Entries;

        Assert.Equal(52, entries.Count);
        Assert.Equal(PatchRepository.Boot, entries[0].Repository);
        Assert.Equal(ClientVersionInfo.TargetBootVersion, entries[0].ToVersion);
        Assert.Equal(5571687, entries[0].ExpectedSizeBytes);
        Assert.Equal(0x47DDE5EDu, entries[0].ExpectedCrc32);
        Assert.Equal(PatchRepository.Game, entries[^1].Repository);
        Assert.Equal(ClientVersionInfo.TargetGameVersion, entries[^1].ToVersion);
        Assert.Equal(20874726, entries[^1].ExpectedSizeBytes);
        Assert.Equal(0x8A775526u, entries[^1].ExpectedCrc32);
    }

    [Fact]
    public void PatchLibraryReportDetectsCompleteLibrary()
    {
        string root = CreateTempDirectory();
        foreach (PatchEntry entry in LegacyPatchManifest.Entries)
        {
            string patchPath = Path.Combine(root, entry.RelativePatchPath);
            string metainfoPath = Path.Combine(root, entry.RelativeMetainfoPath);
            Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(metainfoPath)!);
            File.WriteAllText(patchPath, "patch");
            File.WriteAllText(metainfoPath, "torrent");
        }

        PatchLibraryReport report = LegacyPatchManifest.InspectLibrary(
            root,
            PatchLibraryInspectionMode.PresenceOnly);

        Assert.True(report.IsComplete);
        Assert.Equal(52, report.PresentPatchCount);
        Assert.Equal(52, report.PresentMetainfoCount);
    }

    [Fact]
    public void PatchLibraryReportDetectsFfxivPatchesLayout()
    {
        string root = CreateTempDirectory();
        foreach (PatchEntry entry in LegacyPatchManifest.Entries)
        {
            string patchPath = Path.Combine(root, "ffxiv_patches", entry.RepositoryId, "patch", entry.PatchFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
            File.WriteAllText(patchPath, "patch");
        }

        PatchLibraryReport report = LegacyPatchManifest.InspectLibrary(
            root,
            PatchLibraryInspectionMode.PresenceOnly);

        Assert.True(report.IsPatchChainReady);
        Assert.True(report.IsComplete);
        Assert.EndsWith("ffxiv_patches", report.PatchBasePath);
        Assert.Equal(52, report.PresentPatchCount);
        Assert.Equal(0, report.PresentMetainfoCount);
        Assert.Contains("optional metainfo 0/52", report.Summary);
    }

    [Fact]
    public void PatchLibraryReportDetectsSelectedFfxivRootLayout()
    {
        string root = CreateTempDirectory();
        string ffxivRoot = Path.Combine(root, "ffxiv");
        foreach (PatchEntry entry in LegacyPatchManifest.Entries)
        {
            string patchPath = Path.Combine(ffxivRoot, entry.RepositoryId, "patch", entry.PatchFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
            File.WriteAllText(patchPath, "patch");
        }

        PatchLibraryReport report = LegacyPatchManifest.InspectLibrary(
            ffxivRoot,
            PatchLibraryInspectionMode.PresenceOnly);

        Assert.True(report.IsPatchChainReady);
        Assert.True(report.IsComplete);
        Assert.Equal(ffxivRoot, report.PatchBasePath);
        Assert.Equal(52, report.PresentPatchCount);
    }

    [Fact]
    public void PatchLibraryReportDetectsFlatPatchFolderLayout()
    {
        string root = CreateTempDirectory();
        foreach (PatchEntry entry in LegacyPatchManifest.Entries)
        {
            string patchPath = Path.Combine(root, entry.PatchFileName);
            File.WriteAllText(patchPath, "patch");
        }

        PatchLibraryReport report = LegacyPatchManifest.InspectLibrary(
            root,
            PatchLibraryInspectionMode.PresenceOnly);

        Assert.True(report.IsPatchChainReady);
        Assert.True(report.IsComplete);
        Assert.Equal(root, report.PatchBasePath);
        Assert.Equal(52, report.PresentPatchCount);
    }

    [Fact]
    public void PatchLibraryReportDetectsSelectedRepositoryPatchFolderLayout()
    {
        string root = CreateTempDirectory();
        string gamePatchRoot = Path.Combine(root, "ffxiv", "48eca647", "patch");
        foreach (PatchEntry entry in LegacyPatchManifest.Entries.Where(entry => entry.Repository == PatchRepository.Game))
        {
            string patchPath = Path.Combine(gamePatchRoot, entry.PatchFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
            File.WriteAllText(patchPath, "patch");
        }

        PatchLibraryReport report = LegacyPatchManifest.InspectLibrary(
            gamePatchRoot,
            PatchLibraryInspectionMode.PresenceOnly);

        Assert.False(report.IsPatchChainReady);
        Assert.Equal(gamePatchRoot, report.PatchBasePath);
        Assert.Equal(51, report.PresentPatchCount);
        Assert.Single(report.MissingPatchFiles);
        Assert.Equal(PatchRepository.Boot, report.MissingPatchFiles[0].Repository);
    }

    [Fact]
    public void PatchLibraryReportPrefersMoreCompletePatchLayout()
    {
        string root = CreateTempDirectory();
        PatchEntry staleEntry = LegacyPatchManifest.Entries[0];
        string stalePath = Path.Combine(root, "ffxiv", staleEntry.RepositoryId, "patch", staleEntry.PatchFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(stalePath)!);
        File.WriteAllText(stalePath, "stale");

        foreach (PatchEntry entry in LegacyPatchManifest.Entries)
        {
            string patchPath = Path.Combine(root, "ffxiv_patches", entry.RepositoryId, "patch", entry.PatchFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
            File.WriteAllText(patchPath, "patch");
        }

        PatchLibraryReport report = LegacyPatchManifest.InspectLibrary(
            root,
            PatchLibraryInspectionMode.PresenceOnly);

        Assert.EndsWith("ffxiv_patches", report.PatchBasePath);
        Assert.True(report.IsPatchChainReady);
        Assert.Equal(52, report.PresentPatchCount);
    }

    [Fact]
    public void PatchLibraryReportDetectsInvalidPatchSize()
    {
        string root = CreateTempDirectory();
        PatchEntry entry = LegacyPatchManifest.Entries[0];
        string patchPath = Path.Combine(root, entry.RelativePatchPath);
        string metainfoPath = Path.Combine(root, entry.RelativeMetainfoPath);
        Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(metainfoPath)!);
        File.WriteAllText(patchPath, "patch");
        File.WriteAllText(metainfoPath, "torrent");

        PatchLibraryReport report = LegacyPatchManifest.InspectLibrary(root);

        PatchFileReport invalid = Assert.Single(report.InvalidPatchFiles);
        Assert.Equal(entry, invalid.Entry);
        Assert.Equal(5, invalid.ActualSizeBytes);
        Assert.False(report.IsComplete);
    }

    [Fact]
    public void Crc32MatchesStandardCheckValue()
    {
        byte[] data = Encoding.ASCII.GetBytes("123456789");

        uint crc32 = Crc32.Compute(data);

        Assert.Equal(0xCBF43926u, crc32);
    }

    [Fact]
    public void LegacyPatchApplierAppliesRawFileEntry()
    {
        string root = CreateTempDirectory();
        string patchPath = Path.Combine(CreateTempDirectory(), "raw.patch");
        byte[] payload = Encoding.ASCII.GetBytes("hello");
        WritePatchFile(patchPath, "client/script/staticactors.bin", payload, compressed: false);

        LegacyPatchApplier.ApplyPatchFile(root, patchPath);

        Assert.Equal(payload, File.ReadAllBytes(Path.Combine(root, "client", "script", "staticactors.bin")));
    }

    [Fact]
    public void LegacyPatchApplierAppliesCompressedFileEntry()
    {
        string root = CreateTempDirectory();
        string patchPath = Path.Combine(CreateTempDirectory(), "compressed.patch");
        byte[] payload = Encoding.ASCII.GetBytes("compressed static actors");
        WritePatchFile(patchPath, "client/script/staticactors.bin", payload, compressed: true);

        LegacyPatchApplier.ApplyPatchFile(root, patchPath);

        Assert.Equal(payload, File.ReadAllBytes(Path.Combine(root, "client", "script", "staticactors.bin")));
    }

    [Fact]
    public void LegacyPatchApplierUsesFinalMultiItemPayload()
    {
        string root = CreateTempDirectory();
        string patchPath = Path.Combine(CreateTempDirectory(), "multi.patch");
        byte[] expected = Encoding.ASCII.GetBytes("second");

        WritePatchFileChunks(
            patchPath,
            "client/script/staticactors.bin",
            (0x41, [], false, 0),
            (0x4D, expected, true, (uint)expected.Length));

        LegacyPatchApplier.ApplyPatchFile(root, patchPath);

        Assert.Equal(expected, File.ReadAllBytes(Path.Combine(root, "client", "script", "staticactors.bin")));
    }

    [Fact]
    public void LegacyPatchApplierRejectsNonFinalMultiItemPayload()
    {
        string root = CreateTempDirectory();
        string patchPath = Path.Combine(CreateTempDirectory(), "bad-multi.patch");

        WritePatchFileChunks(
            patchPath,
            "client/script/staticactors.bin",
            (0x41, Encoding.ASCII.GetBytes("first"), false, 5),
            (0x4D, Encoding.ASCII.GetBytes("second"), true, 6));

        Assert.Throws<InvalidDataException>(() => LegacyPatchApplier.ApplyPatchFile(root, patchPath));
    }

    [Fact]
    public void LegacyPatchApplierRejectsPatchedSizeMismatch()
    {
        string root = CreateTempDirectory();
        string patchPath = Path.Combine(CreateTempDirectory(), "size-mismatch.patch");

        WritePatchFileChunks(
            patchPath,
            "client/script/staticactors.bin",
            (0x41, Encoding.ASCII.GetBytes("short"), false, 99));

        Assert.Throws<InvalidDataException>(() => LegacyPatchApplier.ApplyPatchFile(root, patchPath));
    }

    [Fact]
    public void LegacyPatchApplierAppliesPayloadForFirstHashMode()
    {
        string root = CreateTempDirectory();
        string targetPath = Path.Combine(root, "client", "script", "staticactors.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.WriteAllText(targetPath, "old bytes");
        string patchPath = Path.Combine(CreateTempDirectory(), "first-hash.patch");
        byte[] expected = Encoding.ASCII.GetBytes("replacement bytes");

        WritePatchFileChunks(
            patchPath,
            "client/script/staticactors.bin",
            (0x44, expected, false, (uint)expected.Length));

        LegacyPatchApplier.ApplyPatchFile(root, patchPath);

        Assert.Equal(expected, File.ReadAllBytes(targetPath));
    }

    [Fact]
    public void LegacyPatchApplierDeletesRetailBodylessDeleteEntryIdempotently()
    {
        string root = CreateTempDirectory();
        string targetPath = Path.Combine(root, "client", "script", "staticactors.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        byte[] source = Encoding.ASCII.GetBytes("delete me");
        File.WriteAllBytes(targetPath, source);
        string patchPath = Path.Combine(CreateTempDirectory(), "first-hash-empty.patch");

        WritePatchDeletion(
            patchPath,
            "client/script/staticactors.bin",
            source);

        LegacyPatchApplier.ApplyPatchFile(root, patchPath);
        LegacyPatchApplier.ApplyPatchFile(root, patchPath);

        Assert.False(File.Exists(targetPath));
    }

    [Fact]
    public void LegacyPatchApplierRefusesToDeleteModifiedSourceFile()
    {
        string root = CreateTempDirectory();
        string targetPath = Path.Combine(root, "client", "script", "staticactors.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        File.WriteAllText(targetPath, "locally modified");
        string patchPath = Path.Combine(CreateTempDirectory(), "delete-mismatch.patch");

        WritePatchDeletion(
            patchPath,
            "client/script/staticactors.bin",
            Encoding.ASCII.GetBytes("retail source"));

        Assert.Throws<InvalidDataException>(() => LegacyPatchApplier.ApplyPatchFile(root, patchPath));
        Assert.Equal("locally modified", File.ReadAllText(targetPath));
    }

    [Fact]
    public void LegacyPatchApplierRejectsEscapingPath()
    {
        string root = CreateTempDirectory();
        string patchPath = Path.Combine(CreateTempDirectory(), "escape.patch");
        WritePatchFile(patchPath, "../escaped.bin", Encoding.ASCII.GetBytes("nope"), compressed: false);

        Assert.Throws<InvalidDataException>(() => LegacyPatchApplier.ApplyPatchFile(root, patchPath));
    }

    [Fact]
    public void RuntimeDiscoveryFindsApprovedWineStableOnly()
    {
        IReadOnlyList<RuntimeCandidate> candidates = RuntimeDiscovery.Discover(
            path => path.Contains("Wine Stable.app", StringComparison.Ordinal)
                || path.Contains("XIV on Mac", StringComparison.Ordinal)
                || path.Contains("WhiskyCmd", StringComparison.Ordinal)
                || path.Contains("CrossOver", StringComparison.Ordinal)
                || path.Contains("game-porting-toolkit", StringComparison.Ordinal),
            _ => false,
            _ => new[] { "wow" });

        RuntimeCandidate candidate = Assert.Single(candidates);
        Assert.Equal("Homebrew Wine Stable", candidate.Name);
        Assert.Equal(WineRuntimeKind.WinePrefix, candidate.Kind);
        Assert.Contains("Wine Stable.app", candidate.Command, StringComparison.Ordinal);
        Assert.Null(candidate.BottleOrPrefix);
        Assert.DoesNotContain(candidates, runtime => runtime.Name.Contains("XIV on Mac", StringComparison.Ordinal));
        Assert.DoesNotContain(candidates, runtime => runtime.Kind == WineRuntimeKind.WhiskyBottle);
        Assert.DoesNotContain(candidates, runtime => runtime.Kind == WineRuntimeKind.CrossOverBottle);
    }

    [Fact]
    public void RuntimeSetupGuidanceUsesMacOsWineInstructions()
    {
        RuntimeSetupGuidance guidance = RuntimeSetupGuidance.ForPlatform(
            new LauncherPlatform(LauncherOperatingSystem.MacOS, "osx-arm64"));

        Assert.Contains("macOS", guidance.Title, StringComparison.Ordinal);
        Assert.Equal("wiki.winehq.org", guidance.GuideUri.Host);
        Assert.Contains("Rosetta", guidance.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSetupGuidanceRecognizesSteamOsBeforeArchFamily()
    {
        RuntimeSetupGuidance guidance = RuntimeSetupGuidance.ForPlatform(
            new LauncherPlatform(LauncherOperatingSystem.Linux, "linux-x64"),
            _ => "ID=steamos\nID_LIKE=arch\n");

        Assert.Contains("SteamOS", guidance.Title, StringComparison.Ordinal);
        Assert.Contains("persistent", guidance.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("STEAMOS.md", guidance.GuideUri.AbsoluteUri, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSetupGuidanceUsesWineHqPackagesForUbuntu()
    {
        RuntimeSetupGuidance guidance = RuntimeSetupGuidance.ForPlatform(
            new LauncherPlatform(LauncherOperatingSystem.Linux, "linux-x64"),
            _ => "ID=ubuntu\nID_LIKE=debian\n");

        Assert.Contains("Debian-Ubuntu", guidance.GuideUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains("32-bit", guidance.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeDiscoveryDoesNotAutoAdoptCommonLinuxWinePrefixes()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string dotWine = Path.Combine(home, ".wine");
        string homeWine = Path.Combine(home, "wine");

        IReadOnlyList<RuntimeCandidate> candidates = RuntimeDiscovery.Discover(
            _ => false,
            path => string.Equals(path, dotWine, StringComparison.Ordinal)
                || string.Equals(path, homeWine, StringComparison.Ordinal));

        Assert.Empty(candidates);
    }

    [Fact]
    public void RuntimeDiscoveryFindsSystemWineOnPath()
    {
        string wineDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string wineCommand = Path.Combine(wineDirectory, "wine");
        string wine64Command = Path.Combine(wineDirectory, "wine64");

        IReadOnlyList<RuntimeCandidate> candidates = RuntimeDiscovery.Discover(
            path => string.Equals(path, wineCommand, StringComparison.Ordinal)
                || string.Equals(path, wine64Command, StringComparison.Ordinal),
            _ => false,
            _ => Array.Empty<string>(),
            _ => Array.Empty<string>(),
            wineDirectory);

        Assert.Equal(2, candidates.Count);
        Assert.Contains(candidates, candidate =>
            candidate.Name == "System Wine"
            && candidate.Kind == WineRuntimeKind.WinePrefix
            && candidate.Command == wineCommand
            && candidate.BottleOrPrefix is null);
        Assert.Contains(candidates, candidate =>
            candidate.Name == "System Wine 64"
            && candidate.Kind == WineRuntimeKind.WinePrefix
            && candidate.Command == wine64Command);
    }

    [Fact]
    public void RuntimeDiscoveryDoesNotAutoAdoptCustomHomeWinePrefix()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string customPrefix = Path.Combine(home, ".wine_custom");

        IReadOnlyList<RuntimeCandidate> candidates = RuntimeDiscovery.Discover(
            _ => false,
            path => string.Equals(path, customPrefix, StringComparison.Ordinal),
            _ => Array.Empty<string>(),
            _ => new[] { customPrefix });

        Assert.Empty(candidates);
    }

    [Fact]
    public void RuntimeDiscoveryParsesWhiskyBottleList()
    {
        string output = """
            +------+-----------------+--------------------------------------------------------------------------+
            | Name | Windows Version | Path                                                                     |
            +------+-----------------+--------------------------------------------------------------------------+
            | Wow  | Windows 10      | /Volumes/Dev/604B73F2-10BC-435C-8D4A-9331841FC7B3                        |
            | wow  | Windows 10      | ~/Library/Containers/Whisky/Bottles/14A60580-0075-47F8-813E-38CE3A0CE5D4 |
            +------+-----------------+--------------------------------------------------------------------------+
            """;

        IReadOnlyList<string> bottles = RuntimeDiscovery.ParseWhiskyBottleNames(output);

        Assert.Equal(new[] { "Wow", "wow" }, bottles);
    }

    [Fact]
    public void WhiskyShellEnvResolvesWineProfile()
    {
        string root = CreateTempDirectory();
        string wineDirectory = Path.Combine(root, "Wine", "bin");
        Directory.CreateDirectory(wineDirectory);
        string winePath = Path.Combine(wineDirectory, "wine64");
        File.WriteAllText(winePath, "");

        string shellEnv = $"""
            export PATH="{wineDirectory}:$PATH"
            export WINE="wine64"
            export WINEPREFIX="~/Library/Containers/Whisky/Bottles/test"
            export WINEDEBUG="fixme-all"
            """;

        bool resolved = WhiskyRuntimeEnvironment.TryCreateWineProfileFromShellEnv(
            "Whisky - test",
            shellEnv,
            out WineRuntimeProfile profile,
            out string error);

        Assert.True(resolved, error);
        Assert.Equal(WineRuntimeKind.WinePrefix, profile.Kind);
        Assert.Equal(winePath, profile.Command);
        Assert.EndsWith(Path.Combine("Library", "Containers", "Whisky", "Bottles", "test"), profile.PrefixPath);
        Assert.Equal("fixme-all", profile.Environment["WINEDEBUG"]);
    }

    [Fact]
    public void ProfileStoreRoundTripsLocalProfile()
    {
        string path = Path.Combine(CreateTempDirectory(), "profile.json");
        string runtimeCommand = Path.Combine(Path.GetDirectoryName(path)!, "runtime", "bin", "wine64");
        string runtimePrefix = Path.Combine(Path.GetDirectoryName(path)!, "prefixes", "ffxiv");
        WineRuntimeProfile runtime = WineRuntimeProfile.WinePrefix(
            "Custom Wine",
            runtimePrefix,
            runtimeCommand,
            new Dictionary<string, string> { ["WINEDEBUG"] = "fixme-all" });
        LauncherProfile profile = new(
            "/games/ffxiv-1x",
            "/patches/ffxiv-1x",
            "https://launcher.example.test/launcher",
            "https://cdn.example.test/ffxiv_patches",
            ServerProfile.LocalDefault(),
            runtime,
            RuntimeSelectionMode.CustomRuntime,
            ClientLaunchHelperMode.X86,
            ClientGraphicsTarget.WineD3DVulkan);

        ProfileStore.Save(path, profile);
        LauncherProfile loaded = ProfileStore.Load(path);

        Assert.Equal(profile.ClientRootPath, loaded.ClientRootPath);
        Assert.Equal(profile.PatchLibraryRootPath, loaded.PatchLibraryRootPath);
        Assert.Equal(profile.LauncherServiceUrl, loaded.LauncherServiceUrl);
        Assert.Equal(profile.PatchBaseUrl, loaded.PatchBaseUrl);
        Assert.Equal(profile.ServerProfile, loaded.ServerProfile);
        Assert.Equal(profile.RuntimeProfile.Name, loaded.RuntimeProfile.Name);
        Assert.Equal(profile.RuntimeProfile.Kind, loaded.RuntimeProfile.Kind);
        Assert.Equal(runtimeCommand, loaded.RuntimeProfile.Command);
        Assert.Equal(runtimePrefix, loaded.RuntimeProfile.PrefixPath);
        Assert.Equal(runtimePrefix, loaded.RuntimeProfile.Environment["WINEPREFIX"]);
        Assert.Equal("fixme-all", loaded.RuntimeProfile.Environment["WINEDEBUG"]);
        Assert.Equal(WineRuntimeProfile.DefaultDirect3DConfig, loaded.RuntimeProfile.Environment["WINE_D3D_CONFIG"]);
        Assert.Equal(profile.RuntimeMode, loaded.RuntimeMode);
        Assert.Equal(profile.LaunchHelperMode, loaded.LaunchHelperMode);
        Assert.Equal(profile.GraphicsTarget, loaded.GraphicsTarget);
    }

    [Fact]
    public void LauncherPatchManifestUsesKnownPatchChain()
    {
        LauncherPatchManifest manifest = LauncherPatchManifest.FromKnownPatchChain("https://cdn.example.test/ffxiv_patches/");

        Assert.Equal(ClientVersionInfo.TargetBootVersion, manifest.TargetBootVersion);
        Assert.Equal(ClientVersionInfo.TargetGameVersion, manifest.TargetGameVersion);
        Assert.Equal(52, manifest.Files.Count);
        Assert.Equal("ffxiv/2d2a390f/patch/D2010.09.18.0000.patch", manifest.Files[0].RelativePath);
        Assert.Equal("47DDE5ED", manifest.Files[0].Crc32);
        Assert.Equal("https://cdn.example.test/ffxiv_patches", manifest.PatchBaseUrl);
    }

    [Fact]
    public async Task PatchDownloadServiceDownloadsAndReusesValidatedFiles()
    {
        byte[] payload = Encoding.ASCII.GetBytes("123456789");
        LauncherPatchManifest manifest = new(
            ClientVersionInfo.TargetBootVersion,
            ClientVersionInfo.TargetGameVersion,
            "https://cdn.example.test/patches",
            new[]
            {
                new LauncherPatchFile("ffxiv/48eca647/patch/test.patch", payload.Length, "CBF43926", null)
            });
        HttpClient client = new(new StaticPatchHandler(payload));
        string root = CreateTempDirectory();
        List<PatchDownloadProgress> progress = new();

        PatchDownloadResult first = await PatchDownloadService.DownloadPatchLibraryAsync(
            manifest,
            root,
            client,
            new Progress<PatchDownloadProgress>(progress.Add));
        PatchDownloadResult second = await PatchDownloadService.DownloadPatchLibraryAsync(
            manifest,
            root,
            client);

        string localPath = Path.Combine(root, "ffxiv", "48eca647", "patch", "test.patch");
        Assert.True(File.Exists(localPath));
        Assert.Equal(payload, File.ReadAllBytes(localPath));
        Assert.Equal(1, first.DownloadedFileCount);
        Assert.Equal(0, first.ReusedFileCount);
        Assert.Equal(0, second.DownloadedFileCount);
        Assert.Equal(1, second.ReusedFileCount);
        Assert.Contains(progress, update => update.LogMessage);
    }

    [Fact]
    public void RuntimeCatalogDeserializesAndSelectsDefaultArtifact()
    {
        string json = """
        {
          "platform": "osx-arm64",
          "artifacts": [
            {
              "name": "Fallback Wine",
              "version": "1.0",
              "platform_rid": "osx-arm64",
              "runtime_kind": "wine",
              "archive_url": "https://cdn.example.test/runtime-fallback.zip",
              "archive_format": "zip",
              "size_bytes": 12,
              "sha256": "ABC",
              "executable_relative_path": "bin/wine",
              "prefix_arch": "win64",
              "environment": {},
              "is_default": false,
              "is_active": true,
              "sort_order": 20
            },
            {
              "name": "AetherXIV Wine",
              "version": "2.0",
              "platform_rid": "osx-arm64",
              "runtime_kind": "wine",
              "archive_url": "https://cdn.example.test/runtime.zip",
              "archive_format": "zip",
              "size_bytes": 12,
              "sha256": "DEF",
              "executable_relative_path": "bin/wine",
              "prefix_arch": "win64",
              "environment": { "WINEDEBUG": "-all" },
              "is_default": true,
              "is_active": true,
              "sort_order": 10
            }
          ]
        }
        """;

        RuntimeCatalog catalog = JsonSerializer.Deserialize<RuntimeCatalog>(json)!;
        RuntimeArtifact selected = catalog.SelectDefault()!;

        Assert.Equal("osx-arm64", catalog.Platform);
        Assert.Equal("AetherXIV Wine", selected.Name);
        Assert.Equal("-all", selected.Environment["WINEDEBUG"]);
    }

    [Theory]
    [InlineData("osx-arm64", "11.0_1", "b50dc50ec7f41d58b115a6b685d4d1315ba3c797bd3aa0f49213f2703cb82388")]
    [InlineData("osx-x64", "11.0_1", "b50dc50ec7f41d58b115a6b685d4d1315ba3c797bd3aa0f49213f2703cb82388")]
    [InlineData("linux-x64", "11.0", "39574efa1132c3ca0d5c77dd2eddbe4a49cca0d6cc2c290ff4924493a1c40314")]
    public void BuiltInRuntimeCatalogPinsSupportedArtifacts(string runtimeIdentifier, string version, string sha256)
    {
        RuntimeArtifact artifact = BuiltInRuntimeCatalog.Find(runtimeIdentifier)!;

        Assert.NotNull(artifact);
        Assert.Equal(runtimeIdentifier, artifact.PlatformRid);
        Assert.Equal(version, artifact.Version);
        Assert.Equal(sha256, artifact.Sha256);
        Assert.Equal("tar.xz", artifact.ArchiveFormat);
        Assert.StartsWith("https://github.com/", artifact.ArchiveUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void BuiltInRuntimeCatalogRejectsUnsupportedArchitecture()
    {
        Assert.Null(BuiltInRuntimeCatalog.Find("linux-arm64"));
        Assert.Empty(BuiltInRuntimeCatalog.ForPlatform("linux-arm64").Artifacts);
    }

    [Fact]
    public void RuntimePrerequisitesRequireRosettaOnlyForAppleSiliconMacOs()
    {
        Assert.True(RuntimePlatformPrerequisites.RequiresRosetta(
            new LauncherPlatform(LauncherOperatingSystem.MacOS, "osx-arm64"),
            System.Runtime.InteropServices.Architecture.Arm64));
        Assert.False(RuntimePlatformPrerequisites.RequiresRosetta(
            new LauncherPlatform(LauncherOperatingSystem.MacOS, "osx-x64"),
            System.Runtime.InteropServices.Architecture.X64));
        Assert.False(RuntimePlatformPrerequisites.RequiresRosetta(
            new LauncherPlatform(LauncherOperatingSystem.Linux, "linux-arm64"),
            System.Runtime.InteropServices.Architecture.Arm64));
    }

    [Fact]
    public void RuntimePrerequisitesParseMissingLinuxLibrariesWithoutDuplicates()
    {
        IReadOnlyList<string> missing = RuntimePlatformPrerequisites.ParseMissingLinuxLibraries("""
            libX11.so.6 => not found
            libvulkan.so.1 => not found
            libX11.so.6 => not found
            libm.so.6 => /lib/libm.so.6 (0x01)
            """);

        Assert.Equal(new[] { "libX11.so.6", "libvulkan.so.1" }, missing);
    }

    [Theory]
    [InlineData("ID=ubuntu\nID_LIKE=debian", "apt")]
    [InlineData("ID=steamos\nID_LIKE=arch", "SteamOS/Arch")]
    [InlineData("ID=fedora", "software manager")]
    public void RuntimePrerequisitesProvideDistributionFamilyGuidance(string osRelease, string expected)
    {
        Assert.Contains(
            expected,
            RuntimePlatformPrerequisites.LinuxDependencyGuidance(osRelease),
            StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ID=ubuntu\nID_LIKE=debian", "apt-get", "wine64")]
    [InlineData("ID=arch", "pacman", "wine")]
    [InlineData("ID=fedora", "dnf", "wine")]
    public void RuntimeDependencyInstallerBuildsPlatformPackagePlan(
        string osRelease,
        string expectedManager,
        string expectedPackage)
    {
        RuntimeDependencyInstallPlan plan = RuntimeDependencyInstaller.CreateLinuxPlan(osRelease, true);

        Assert.True(plan.IsSupported);
        Assert.Contains(expectedManager, plan.PackageManagerCommand, StringComparison.Ordinal);
        Assert.Contains(expectedPackage, plan.Arguments);
        Assert.Equal("/usr/bin/pkexec", plan.ElevationCommand);
    }

    [Fact]
    public void RuntimeDependencyInstallerRequiresSupportedDistributionAndPolicyKit()
    {
        Assert.False(RuntimeDependencyInstaller.CreateLinuxPlan("ID=gentoo", true).IsSupported);
        Assert.False(RuntimeDependencyInstaller.CreateLinuxPlan("ID=steamos\nID_LIKE=arch", true).IsSupported);
        Assert.False(RuntimeDependencyInstaller.CreateLinuxPlan("ID=arch", false).IsSupported);
    }

    [Fact]
    public async Task RuntimeDownloadServiceInstallsValidatedZipArchive()
    {
        byte[] archive = CreateRuntimeArchive(("bin/wine", Encoding.ASCII.GetBytes("#!/bin/sh\n")));
        RuntimeArtifact artifact = CreateRuntimeArtifact(archive);
        string root = CreateTempDirectory();
        List<RuntimeDownloadProgress> progress = new();

        RuntimeDownloadResult result = await RuntimeDownloadService.DownloadAndInstallAsync(
            artifact,
            new HttpClient(new StaticPatchHandler(archive)),
            new Progress<RuntimeDownloadProgress>(progress.Add),
            runtimesRoot: Path.Combine(root, "runtimes"),
            cacheRoot: Path.Combine(root, "cache"));

        Assert.True(File.Exists(result.Install.ExecutablePath));
        Assert.True(File.Exists(RuntimeInstallStore.ManifestPathFor(result.Install.InstallPath)));
        Assert.Equal(artifact.Name, result.Install.Name);
        Assert.Contains(progress, update => update.LogMessage);
    }

    [Fact]
    public async Task RuntimeDownloadServiceRejectsPathTraversalArchive()
    {
        byte[] archive = CreateRuntimeArchive(("../escape.sh", Encoding.ASCII.GetBytes("nope")));
        RuntimeArtifact artifact = CreateRuntimeArtifact(archive);
        string root = CreateTempDirectory();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            RuntimeDownloadService.DownloadAndInstallAsync(
                artifact,
                new HttpClient(new StaticPatchHandler(archive)),
                runtimesRoot: Path.Combine(root, "runtimes"),
                cacheRoot: Path.Combine(root, "cache")));
    }

    [Fact]
    public async Task RuntimeDownloadServiceInstallsValidatedTarXzArchive()
    {
        if (OperatingSystem.IsWindows())
            return;

        string root = CreateTempDirectory();
        string payloadRoot = Path.Combine(root, "payload");
        string executable = Path.Combine(payloadRoot, "bin", "wine");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllText(executable, "#!/bin/sh\n");
        string archivePath = Path.Combine(root, "runtime.tar.xz");
        CreateTarXzArchive(payloadRoot, archivePath);
        byte[] archive = File.ReadAllBytes(archivePath);
        RuntimeArtifact artifact = CreateRuntimeArtifact(archive) with
        {
            ArchiveUrl = "https://cdn.example.test/runtime.tar.xz",
            ArchiveFormat = "tar.xz"
        };

        RuntimeDownloadResult result = await RuntimeDownloadService.DownloadAndInstallAsync(
            artifact,
            new HttpClient(new StaticPatchHandler(archive)),
            runtimesRoot: Path.Combine(root, "runtimes"),
            cacheRoot: Path.Combine(root, "cache"));

        Assert.True(File.Exists(result.Install.ExecutablePath));
        Assert.Equal("#!/bin/sh\n", File.ReadAllText(result.Install.ExecutablePath));
    }

    [Fact]
    public async Task UmbraFrameworkDownloadInstallsVerifiedArchive()
    {
        byte[] archive = CreateRuntimeArchive(
            ("Aether.Umbra.Bootstrap.x86.dll", Encoding.ASCII.GetBytes("bootstrap")),
            ("Managed/Aether.Umbra.Framework.dll", Encoding.ASCII.GetBytes("framework")));
        UmbraFrameworkArtifact artifact = CreateUmbraArtifact(archive);
        string root = CreateTempDirectory();

        UmbraFrameworkDownloadResult result = await UmbraFrameworkDownloadService.DownloadAndInstallAsync(
            artifact,
            new HttpClient(new StaticPatchHandler(archive)),
            frameworksRoot: Path.Combine(root, "frameworks"),
            cacheRoot: Path.Combine(root, "cache"));

        Assert.True(File.Exists(result.Install.BootstrapPath));
        Assert.True(File.Exists(result.Install.FrameworkPath));
        Assert.True(File.Exists(UmbraInstallStore.ManifestPathFor(result.Install.InstallPath)));
        Assert.True(result.Install.UsesAetherEntrypoints);
        Assert.True(result.Install.SupportsGameHash(UmbraCompatibility.Known123bGameSha256));
    }

    [Fact]
    public void UmbraFrameworkCatalogIgnoresLegacyNamedArtifacts()
    {
        byte[] archive = CreateRuntimeArchive(
            ("Legacy.Umbra.Bootstrap.x86.dll", Encoding.ASCII.GetBytes("bootstrap")),
            ("Managed/Legacy.Umbra.Framework.dll", Encoding.ASCII.GetBytes("framework")));
        UmbraFrameworkArtifact legacy = CreateUmbraArtifact(archive) with
        {
            Name = "Legacy Umbra",
            BootstrapRelativePath = "Legacy.Umbra.Bootstrap.x86.dll",
            FrameworkRelativePath = "Managed/Legacy.Umbra.Framework.dll"
        };
        UmbraFrameworkCatalog catalog = new("win-x86", new[] { legacy });

        Assert.False(legacy.UsesAetherEntrypoints);
        Assert.Null(catalog.SelectDefault(UmbraCompatibility.Known123bGameSha256));
    }

    [Fact]
    public async Task UmbraFrameworkDownloadRejectsLegacyNamedEntrypoints()
    {
        byte[] archive = CreateRuntimeArchive(
            ("Legacy.Umbra.Bootstrap.x86.dll", Encoding.ASCII.GetBytes("bootstrap")),
            ("Managed/Legacy.Umbra.Framework.dll", Encoding.ASCII.GetBytes("framework")));
        UmbraFrameworkArtifact legacy = CreateUmbraArtifact(archive) with
        {
            Name = "Legacy Umbra",
            BootstrapRelativePath = "Legacy.Umbra.Bootstrap.x86.dll",
            FrameworkRelativePath = "Managed/Legacy.Umbra.Framework.dll"
        };
        string root = CreateTempDirectory();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            UmbraFrameworkDownloadService.DownloadAndInstallAsync(
                legacy,
                new HttpClient(new StaticPatchHandler(archive)),
                frameworksRoot: Path.Combine(root, "frameworks"),
                cacheRoot: Path.Combine(root, "cache")));
    }

    [Fact]
    public async Task UmbraFrameworkDownloadRejectsPathTraversalArchive()
    {
        byte[] archive = CreateRuntimeArchive(("../escape.txt", Encoding.ASCII.GetBytes("nope")));
        UmbraFrameworkArtifact artifact = CreateUmbraArtifact(archive);
        string root = CreateTempDirectory();

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            UmbraFrameworkDownloadService.DownloadAndInstallAsync(
                artifact,
                new HttpClient(new StaticPatchHandler(archive)),
                frameworksRoot: Path.Combine(root, "frameworks"),
                cacheRoot: Path.Combine(root, "cache")));
    }

    [Fact]
    public void RuntimeInstallManifestRoundTrips()
    {
        string root = CreateTempDirectory();
        string installRoot = Path.Combine(root, "runtime");
        string executable = Path.Combine(installRoot, "bin", "wine");
        Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
        File.WriteAllText(executable, "");

        ManagedRuntimeInstall install = new(
            "AetherXIV Wine",
            "1.0",
            "osx-arm64",
            "wine",
            installRoot,
            executable,
            "win64",
            new Dictionary<string, string> { ["WINEDEBUG"] = "-all" },
            DateTimeOffset.UtcNow);

        RuntimeInstallStore.Save(install);
        ManagedRuntimeInstall loaded = RuntimeInstallStore.Load(installRoot);

        Assert.Equal(install.Name, loaded.Name);
        Assert.Equal(install.ExecutablePath, loaded.ExecutablePath);
        Assert.Equal("-all", loaded.Environment["WINEDEBUG"]);
    }

    [Fact]
    public void ManagedPrefixPathUsesApplicationDataLayout()
    {
        string prefixPath = RuntimeInstallStore.ManagedPrefixPath;

        Assert.EndsWith(Path.Combine("Prefixes", "ffxiv-1x"), prefixPath);
        Assert.Contains("Demi Dev Unit", prefixPath, StringComparison.Ordinal);
        Assert.Contains("AetherXIV Launcher", prefixPath, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeProfileResolverPrefersManagedRuntimeInAutomaticMode()
    {
        ManagedRuntimeInstall install = new(
            "AetherXIV Wine",
            "1.0",
            "osx-arm64",
            "wine",
            "/managed/runtime",
            "/managed/runtime/bin/wine",
            "win64",
            new Dictionary<string, string> { ["WINEDEBUG"] = "-all" },
            DateTimeOffset.UtcNow);
        RuntimeCandidate detected = new(
            "Detected Wine",
            WineRuntimeKind.WinePrefix,
            "/usr/local/bin/wine",
            "/tmp/detected-prefix",
            "test");
        WineRuntimeProfile custom = WineRuntimeProfile.Custom("Custom", "/custom/wine");

        WineRuntimeProfile resolved = RuntimeProfileResolver.Resolve(
            RuntimeSelectionMode.AutomaticManaged,
            install,
            new[] { detected },
            custom,
            "/managed/prefix");

        Assert.Equal("/managed/runtime/bin/wine", resolved.Command);
        Assert.Equal("/managed/prefix", resolved.Environment["WINEPREFIX"]);
        Assert.Equal("-all", resolved.Environment["WINEDEBUG"]);
        Assert.Equal(WineRuntimeProfile.DefaultDirect3DConfig, resolved.Environment["WINE_D3D_CONFIG"]);
    }

    [Fact]
    public void RuntimeProfileResolverKeepsDetectedWinePrefix()
    {
        RuntimeCandidate detected = new(
            "Default Wine prefix",
            WineRuntimeKind.WinePrefix,
            "wine",
            "/home/devunit/.wine",
            "WINEPREFIX");
        WineRuntimeProfile custom = WineRuntimeProfile.Custom("Custom", "/custom/wine");

        WineRuntimeProfile resolved = RuntimeProfileResolver.Resolve(
            RuntimeSelectionMode.DetectedRuntime,
            null,
            new[] { detected },
            custom,
            "/managed/prefix");

        Assert.Equal("wine", resolved.Command);
        Assert.Equal("/home/devunit/.wine", resolved.PrefixPath);
        Assert.Equal("/home/devunit/.wine", resolved.Environment["WINEPREFIX"]);
    }

    [Fact]
    public void RuntimeProfileResolverIsolatesDetectedWineWithoutExplicitPrefix()
    {
        RuntimeCandidate detected = new(
            "System Wine",
            WineRuntimeKind.WinePrefix,
            "/usr/bin/wine",
            null,
            "PATH");

        WineRuntimeProfile resolved = RuntimeProfileResolver.Resolve(
            RuntimeSelectionMode.AutomaticManaged,
            null,
            new[] { detected },
            WineRuntimeProfile.Custom("Custom", "/custom/wine"),
            "/managed/aetherxiv-prefix");

        Assert.Equal("/usr/bin/wine", resolved.Command);
        Assert.Equal("/managed/aetherxiv-prefix", resolved.PrefixPath);
        Assert.Equal("/managed/aetherxiv-prefix", resolved.Environment["WINEPREFIX"]);
    }

    [Fact]
    public void RuntimeLaunchDiagnosticsRedactsSessionArgument()
    {
        string arguments = "wine-helper.exe --game ffxivgame.exe --session \"sessionId=secret-session\" --server-host 127.0.0.1";

        string redacted = RuntimeLaunchDiagnostics.RedactSensitiveArguments(arguments);

        Assert.DoesNotContain("secret-session", redacted);
        Assert.Contains("--session <redacted>", redacted, StringComparison.Ordinal);
        Assert.Contains("--server-host 127.0.0.1", redacted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RuntimeValidatorFallsBackToWineBuiltinWineboot()
    {
        if (OperatingSystem.IsWindows())
            return;

        string root = CreateTempDirectory();
        string fakeWine = Path.Combine(root, "wine");
        string argsLog = Path.Combine(root, "args.log");
        await File.WriteAllTextAsync(
            fakeWine,
            $"""
            #!/bin/sh
            echo "$@" >> "{argsLog}"
            if [ "$1" = "--version" ]; then
              echo "wine-11.0"
              exit 0
            fi
            if [ "$1" = "wineboot" ]; then
              exit 0
            fi
            exit 1
            """);
        File.SetUnixFileMode(
            fakeWine,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute);

        string prefix = Path.Combine(root, "prefix");
        WineRuntimeProfile profile = WineRuntimeProfile.WinePrefix("Fake Wine", prefix, fakeWine);

        string? oldPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", root);
            RuntimeValidationResult result = await RuntimeValidator.ValidateAsync(profile, prefix);

            Assert.True(result.IsReady);
            string log = await File.ReadAllTextAsync(argsLog);
            Assert.Contains("--version", log);
            Assert.Contains("wineboot -u", log);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", oldPath);
        }
    }

    [Fact]
    public async Task RuntimeValidatorSkipsWinebootForInitializedPrefix()
    {
        if (OperatingSystem.IsWindows())
            return;

        string root = CreateTempDirectory();
        string fakeWine = Path.Combine(root, "wine");
        string argsLog = Path.Combine(root, "args.log");
        await File.WriteAllTextAsync(
            fakeWine,
            $"""
            #!/bin/sh
            echo "$@" >> "{argsLog}"
            if [ "$1" = "--version" ]; then
              echo "wine-11.0"
              exit 0
            fi
            if [ "$1" = "wineboot" ]; then
              exit 9
            fi
            exit 0
            """);
        File.SetUnixFileMode(
            fakeWine,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute);

        string prefix = Path.Combine(root, "prefix");
        Directory.CreateDirectory(prefix);
        await File.WriteAllTextAsync(Path.Combine(prefix, "system.reg"), "system");
        await File.WriteAllTextAsync(Path.Combine(prefix, "user.reg"), "user");
        WineRuntimeProfile profile = WineRuntimeProfile.WinePrefix("Fake Wine", prefix, fakeWine);

        string? oldPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", root);
            RuntimeValidationResult result = await RuntimeValidator.ValidateAsync(profile, prefix);

            Assert.True(result.IsReady);
            string log = await File.ReadAllTextAsync(argsLog);
            Assert.Contains("--version", log);
            Assert.DoesNotContain("wineboot", log);

            string validationLog = await File.ReadAllTextAsync(result.LogPath);
            Assert.Contains("prefix_already_initialized=", validationLog);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", oldPath);
        }
    }

    [Fact]
    public async Task RuntimeValidatorUsesProfileWinePrefixOverFallback()
    {
        if (OperatingSystem.IsWindows())
            return;

        string root = CreateTempDirectory();
        string fakeWine = Path.Combine(root, "wine");
        await File.WriteAllTextAsync(
            fakeWine,
            """
            #!/bin/sh
            if [ "$1" = "--version" ]; then
              echo "wine-11.0"
              exit 0
            fi
            exit 0
            """);
        File.SetUnixFileMode(
            fakeWine,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute);

        string selectedPrefix = Path.Combine(root, ".wine_custom");
        Directory.CreateDirectory(selectedPrefix);
        await File.WriteAllTextAsync(Path.Combine(selectedPrefix, "system.reg"), "system");
        await File.WriteAllTextAsync(Path.Combine(selectedPrefix, "user.reg"), "user");
        string fallbackPrefix = Path.Combine(root, "managed-prefix");
        WineRuntimeProfile profile = WineRuntimeProfile.WinePrefix("Custom Wine", selectedPrefix, fakeWine);

        RuntimeValidationResult result = await RuntimeValidator.ValidateAsync(profile, fallbackPrefix);

        Assert.True(result.IsReady);
        Assert.Equal(selectedPrefix, result.PrefixPath);
        Assert.False(Directory.Exists(fallbackPrefix));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetherxiv-launcher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WritePatchFile(string patchPath, string relativePath, byte[] payload, bool compressed)
    {
        WritePatchFileChunks(patchPath, relativePath, (0x41, payload, compressed, (uint)payload.Length));
    }

    private static void WritePatchFileChunks(
        string patchPath,
        string relativePath,
        params (uint Mode, byte[] Payload, bool Compressed, uint NewFileSize)[] chunks)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
        using FileStream stream = File.Create(patchPath);
        stream.Write([0x91, (byte)'Z', (byte)'I', (byte)'P', (byte)'A', (byte)'T', (byte)'C', (byte)'H', 0x0D, 0x0A, 0x1A, 0x0A]);

        using MemoryStream body = new();
        byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
        WriteUInt32BigEndian(body, (uint)pathBytes.Length);
        body.Write(pathBytes);
        WriteUInt32BigEndian(body, (uint)chunks.Length);

        foreach ((uint mode, byte[] payload, bool compressed, uint newFileSize) in chunks)
        {
            WriteUInt32LittleEndian(body, mode);
            body.Write(new byte[0x14]);
            body.Write(payload.Length == 0 ? new byte[0x14] : SHA1.HashData(payload));

            byte[] storedPayload = compressed ? CompressZlib(payload) : payload;
            WriteUInt32LittleEndian(body, compressed ? 0x5Au : 0x4Eu);
            WriteUInt32BigEndian(body, (uint)storedPayload.Length);
            WriteUInt32BigEndian(body, 0);
            WriteUInt32BigEndian(body, newFileSize);
            body.Write(storedPayload);
        }

        WritePatchChunk(stream, "ETRY", body.ToArray());
    }

    private static void WritePatchDeletion(string patchPath, string relativePath, byte[] source)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(patchPath)!);
        using FileStream stream = File.Create(patchPath);
        stream.Write([0x91, (byte)'Z', (byte)'I', (byte)'P', (byte)'A', (byte)'T', (byte)'C', (byte)'H', 0x0D, 0x0A, 0x1A, 0x0A]);

        using MemoryStream body = new();
        byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
        WriteUInt32BigEndian(body, (uint)pathBytes.Length);
        body.Write(pathBytes);
        WriteUInt32BigEndian(body, 1);
        WriteUInt32LittleEndian(body, 0x44);
        body.Write(SHA1.HashData(source));
        body.Write(new byte[0x14]);
        WriteUInt32LittleEndian(body, 0x4E);
        WriteUInt32BigEndian(body, 0);
        WriteUInt32BigEndian(body, (uint)source.Length);
        WriteUInt32BigEndian(body, 0);

        WritePatchChunk(stream, "ETRY", body.ToArray());
    }

    private static byte[] CompressZlib(byte[] payload)
    {
        using MemoryStream output = new();
        using (ZLibStream zlib = new(output, CompressionMode.Compress))
        {
            zlib.Write(payload);
        }

        return output.ToArray();
    }

    private static void WritePatchChunk(Stream stream, string command, byte[] body)
    {
        WriteUInt32BigEndian(stream, (uint)body.Length);
        stream.Write(Encoding.ASCII.GetBytes(command));
        stream.Write(body);
        stream.Write(new byte[4]);
    }

    private static void WriteUInt32BigEndian(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteUInt32LittleEndian(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static byte[] CreateRuntimeArchive(params (string Path, byte[] Payload)[] files)
    {
        using MemoryStream output = new();
        using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string path, byte[] payload) in files)
            {
                ZipArchiveEntry entry = archive.CreateEntry(path);
                using Stream stream = entry.Open();
                stream.Write(payload);
            }
        }

        return output.ToArray();
    }

    private static void CreateTarXzArchive(string payloadRoot, string archivePath)
    {
        string tarCommand = File.Exists("/usr/bin/tar") ? "/usr/bin/tar" : "tar";
        System.Diagnostics.ProcessStartInfo startInfo = new()
        {
            FileName = tarCommand,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-cJf");
        startInfo.ArgumentList.Add(archivePath);
        startInfo.ArgumentList.Add("-C");
        startInfo.ArgumentList.Add(payloadRoot);
        startInfo.ArgumentList.Add(".");

        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo)!;
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"tar.xz test archive creation failed: {error}");
    }

    private static RuntimeArtifact CreateRuntimeArtifact(byte[] archive)
    {
        return new RuntimeArtifact(
            "AetherXIV Wine",
            "1.0",
            "osx-arm64",
            "wine",
            "https://cdn.example.test/runtime.zip",
            "zip",
            archive.Length,
            Convert.ToHexString(SHA256.HashData(archive)),
            "bin/wine",
            "win64",
            new Dictionary<string, string> { ["WINEDEBUG"] = "-all" },
            true,
            true,
            10);
    }

    private static UmbraFrameworkArtifact CreateUmbraArtifact(byte[] archive)
    {
        return new UmbraFrameworkArtifact(
            "Aether Umbra",
            "0.1.0",
            "1.0",
            "win-x86",
            "https://cdn.example.test/umbra.zip",
            "zip",
            archive.Length,
            Convert.ToHexString(SHA256.HashData(archive)),
            "Aether.Umbra.Bootstrap.x86.dll",
            "Managed/Aether.Umbra.Framework.dll",
            new[] { UmbraCompatibility.Known123bGameSha256 },
            true,
            true,
            10);
    }

    private static FrameworkStoreEntry CreateStoreEntry(byte[] archive, string id)
    {
        return new FrameworkStoreEntry(
            id,
            id,
            "1.0.0",
            "2.0",
            "https://example.com/plugin.zip",
            archive.Length,
            Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant(),
            "0.1.0",
            "https://example.com/repo.json",
            FrameworkRepositorySource.Custom,
            "Tester",
            "Test plugin",
            "",
            null,
            null,
            Array.Empty<string>(),
            null,
            null,
            false,
            false,
            "ExamplePlugin.dll");
    }

    private static byte[] CreateMinimalConfigExecutable(byte[] systemConfig)
    {
        const int peOffset = 0x80;
        const int optionalHeaderSize = 0xe0;
        const int sectionHeaderOffset = peOffset + 0x18 + optionalHeaderSize;
        const int rawDataOffset = 0x400;
        const int sectionVirtualAddress = 0x5c000;
        const int defaultConfigRva = 0x5c600;
        byte[] executable = new byte[rawDataOffset + 0x1000];

        executable[0] = (byte)'M';
        executable[1] = (byte)'Z';
        BitConverter.GetBytes(peOffset).CopyTo(executable, 0x3c);

        executable[peOffset] = (byte)'P';
        executable[peOffset + 1] = (byte)'E';
        BitConverter.GetBytes((ushort)0x14c).CopyTo(executable, peOffset + 0x4);
        BitConverter.GetBytes((ushort)1).CopyTo(executable, peOffset + 0x6);
        BitConverter.GetBytes((ushort)optionalHeaderSize).CopyTo(executable, peOffset + 0x14);

        int optionalHeaderOffset = peOffset + 0x18;
        BitConverter.GetBytes((ushort)0x10b).CopyTo(executable, optionalHeaderOffset);
        BitConverter.GetBytes(0x400000u).CopyTo(executable, optionalHeaderOffset + 0x1c);

        Encoding.ASCII.GetBytes(".data").CopyTo(executable, sectionHeaderOffset);
        BitConverter.GetBytes(0x1000u).CopyTo(executable, sectionHeaderOffset + 0x8);
        BitConverter.GetBytes(sectionVirtualAddress).CopyTo(executable, sectionHeaderOffset + 0xc);
        BitConverter.GetBytes(0x1000u).CopyTo(executable, sectionHeaderOffset + 0x10);
        BitConverter.GetBytes(rawDataOffset).CopyTo(executable, sectionHeaderOffset + 0x14);

        systemConfig.CopyTo(executable, rawDataOffset + (defaultConfigRva - sectionVirtualAddress));
        return executable;
    }

    private static Dictionary<string, string?> CaptureEnvironment(IEnumerable<string> keys)
    {
        return keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
    }

    private static void RestoreEnvironment(IReadOnlyDictionary<string, string?> values)
    {
        foreach (KeyValuePair<string, string?> pair in values)
            Environment.SetEnvironmentVariable(pair.Key, pair.Value);
    }

    private sealed class StaticPatchHandler : HttpMessageHandler
    {
        private readonly byte[] payload;

        public StaticPatchHandler(byte[] payload)
        {
            this.payload = payload;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = new(System.Net.HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload)
            };
            return Task.FromResult(response);
        }
    }
}
