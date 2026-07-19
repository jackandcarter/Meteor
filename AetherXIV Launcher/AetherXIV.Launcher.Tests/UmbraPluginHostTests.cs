using System.Text.Json;
using Aether.Umbra.Framework;
using Aether.Umbra.PluginApi;
using Aether.Umbra.SamplePlugin;

namespace AetherXIV.Launcher.Tests;

public sealed class UmbraPluginHostTests
{
    [Fact]
    public void UmbraApiStartsAtVersionTwo()
    {
        Assert.Equal("2.0", UmbraFrameworkInfo.ApiVersion);
        Assert.Contains("api=2.0", UmbraFrameworkInfo.ProbeText, StringComparison.Ordinal);
        Assert.True(UmbraPluginCompatibility.SupportsApi("2.0"));
        Assert.False(UmbraPluginCompatibility.SupportsApi("1.0"));
        Assert.False(UmbraPluginCompatibility.SupportsApi("3.0"));
    }

    [Fact]
    public void UmbraClientBuildCatalogRequiresAnExactExecutableHash()
    {
        Assert.True(UmbraClientBuildCatalog.TryResolveSha256(
            "9341F2B4567440B310A4D494F5CC5599CA334BA51C8042247317FF466492F2E9",
            out UmbraClientBuildProfile? profile));
        Assert.NotNull(profile);
        Assert.Equal(UmbraClientBuildCatalog.Legacy123bBuildId, profile.Id);
        Assert.Equal("x86", profile.Architecture);
        Assert.Equal(0x00400000u, profile.PreferredImageBase);

        Assert.False(UmbraClientBuildCatalog.TryResolveSha256(new string('0', 64), out _));
        Assert.False(UmbraClientBuildCatalog.TryResolveSha256(null, out _));
    }

    [Fact]
    public void UmbraGraphicIdDecodesProvenLegacyPacking()
    {
        uint equipmentRaw = (321u << 10) | (17u << 5) | 9u;
        UmbraGraphicId equipment = new(equipmentRaw);
        Assert.False(equipment.IsWeapon);
        Assert.Equal((ushort)321, equipment.EquipmentId);
        Assert.Equal((ushort)17, equipment.VariantId);
        Assert.Equal((byte)9, equipment.ColorId);

        uint weaponRaw = (77u << 20) | (12u << 10) | 513u;
        UmbraGraphicId weapon = new(weaponRaw);
        Assert.True(weapon.IsWeapon);
        Assert.Equal((ushort)77, weapon.WeaponId);
        Assert.Equal((ushort)12, weapon.EquipmentId);
        Assert.Equal((ushort)513, weapon.VariantId);
        Assert.Equal((byte)0, weapon.ColorId);
    }

    [Fact]
    public void UmbraAppearanceSnapshotsAreFixedSizeAndDefensivelyCopied()
    {
        uint[] values = new uint[UmbraAppearanceSlots.Count];
        values[(int)UmbraAppearanceSlot.Head] = 42;
        UmbraActorAppearanceSnapshot snapshot = new(
            0x1000,
            7,
            values,
            1,
            DateTimeOffset.UnixEpoch,
            UmbraAppearanceObservationSource.NetworkPacket);

        values[(int)UmbraAppearanceSlot.Head] = 99;
        Assert.Equal(42u, snapshot.GetValue(UmbraAppearanceSlot.Head));
        Assert.True(snapshot.TryGetGraphicId(UmbraAppearanceSlot.Head, out UmbraGraphicId graphic));
        Assert.Equal(42u, graphic.RawValue);
        Assert.False(snapshot.TryGetGraphicId(UmbraAppearanceSlot.FaceInfo, out _));
        Assert.Throws<ArgumentException>(() => new UmbraActorAppearanceSnapshot(
            1, 1, new uint[27], 1, DateTimeOffset.UnixEpoch, UmbraAppearanceObservationSource.Unknown));
    }

    [Fact]
    public void UmbraAppearanceServiceFailsClosedUntilVerifiedAdapterActivation()
    {
        UmbraActorAppearanceService service = new();
        Assert.False(service.Availability.IsAvailable);
        Assert.Empty(service.Snapshots);
        Assert.Throws<InvalidOperationException>(() => service.Observe(
            1,
            2,
            new uint[UmbraAppearanceSlots.Count],
            DateTimeOffset.UnixEpoch,
            UmbraAppearanceObservationSource.NetworkPacket));

        service.ActivateVerifiedAdapter("legacy-packet-observer", UmbraClientBuildCatalog.Legacy123b);
        UmbraActorAppearanceSnapshot snapshot = service.Observe(
            1,
            2,
            new uint[UmbraAppearanceSlots.Count],
            DateTimeOffset.UnixEpoch,
            UmbraAppearanceObservationSource.NetworkPacket);

        Assert.True(service.Availability.IsAvailable);
        Assert.Equal(UmbraClientBuildCatalog.Legacy123bBuildId, service.Availability.ClientBuildId);
        Assert.Equal(1, snapshot.Revision);
        Assert.True(service.TryGetSnapshot(1, out UmbraActorAppearanceSnapshot? cached));
        Assert.Same(snapshot, cached);

        service.Deactivate("test shutdown");
        Assert.False(service.Availability.IsAvailable);
        Assert.Empty(service.Snapshots);
    }

    [Fact]
    public async Task UmbraRuntimeLoadsAndUnloadsEnabledApiTwoPlugin()
    {
        string root = CreateTempDirectory();
        string pluginRoot = Path.Combine(root, "Plugins");
        string installRoot = Path.Combine(pluginRoot, "sdk-sample");
        Directory.CreateDirectory(installRoot);

        string assemblyName = "Aether.Umbra.SamplePlugin.dll";
        File.Copy(typeof(SamplePlugin).Assembly.Location, Path.Combine(installRoot, assemblyName));
        WriteManifest(installRoot, new UmbraPluginManifest(
            "dev.aetherxiv.umbra.sample",
            "Umbra SDK Sample",
            "0.1.0",
            "2.0",
            assemblyName,
            "0.1.0",
            true)
        {
            EntryType = typeof(SamplePlugin).FullName,
            Capabilities =
            [
                "ui.draw",
                "configuration",
                UmbraCapabilities.CommandRegistration,
                UmbraCapabilities.ChatPrint
            ]
        });

        string logPath = Path.Combine(root, "Logs", "umbra.log");
        UmbraRuntimeOptions options = CreateOptions(root, pluginRoot, logPath, safeMode: false);
        UmbraRuntimeLog log = UmbraRuntimeLog.Open(logPath);

        using UmbraRuntime runtime = await UmbraRuntime.StartAsync(options, log);

        UmbraPluginRuntimeStatus status = Assert.Single(runtime.Plugins.Statuses);
        Assert.Equal("dev.aetherxiv.umbra.sample", status.PluginId);
        Assert.Equal(UmbraPluginRuntimeState.Running, status.State);
        Assert.NotNull(status.LoadedAt);
        Assert.True(Directory.Exists(Path.Combine(root, "Cache", "PluginConfig", status.PluginId)));
        Assert.Equal("/umbra-sample", Assert.Single(runtime.Commands.GetCommands()).Command);

        runtime.Plugins.Update(TimeSpan.FromMilliseconds(16));
        runtime.Plugins.Draw(new TestDrawContext());
        status = Assert.Single(runtime.Plugins.Statuses);
        Assert.True(status.LastUpdateDuration > TimeSpan.Zero);
        Assert.True(status.LastDrawDuration > TimeSpan.Zero);
        Assert.True(status.PeakDrawDuration >= status.LastDrawDuration);

        Assert.True(runtime.Plugins.Unload(status.PluginId));
        Assert.Empty(runtime.Commands.GetCommands());
        UmbraPluginRuntimeStatus unloaded = Assert.Single(runtime.Plugins.Statuses);
        Assert.Equal(UmbraPluginRuntimeState.Unloaded, unloaded.State);
        Assert.Contains("umbra_plugin_loaded id=dev.aetherxiv.umbra.sample", File.ReadAllText(logPath));
        Assert.Contains("umbra_plugin_unloaded id=dev.aetherxiv.umbra.sample", File.ReadAllText(logPath));
    }

    [Fact]
    public async Task UmbraQuarantinesPluginAfterRepeatedCallbackFailures()
    {
        string root = CreateTempDirectory();
        string pluginRoot = Path.Combine(root, "Plugins");
        string installRoot = Path.Combine(pluginRoot, "faulting-sample");
        Directory.CreateDirectory(installRoot);

        string assemblyName = "Aether.Umbra.SamplePlugin.dll";
        File.Copy(typeof(FaultingSamplePlugin).Assembly.Location, Path.Combine(installRoot, assemblyName));
        WriteManifest(installRoot, new UmbraPluginManifest(
            "dev.aetherxiv.umbra.faulting-sample",
            "Umbra Fault Containment Fixture",
            "0.1.0",
            "2.0",
            assemblyName,
            "0.1.0",
            true)
        {
            EntryType = typeof(FaultingSamplePlugin).FullName
        });

        string logPath = Path.Combine(root, "Logs", "umbra.log");
        UmbraRuntimeOptions options = CreateOptions(root, pluginRoot, logPath, safeMode: false);
        using UmbraRuntime runtime = await UmbraRuntime.StartAsync(options, UmbraRuntimeLog.Open(logPath));

        for (int attempt = 0; attempt < UmbraThirdPartyPluginHost.MaximumConsecutiveCallbackErrors; attempt++)
            runtime.Plugins.Update(TimeSpan.FromMilliseconds(16));

        UmbraPluginRuntimeStatus status = Assert.Single(runtime.Plugins.Statuses);
        Assert.Equal(UmbraPluginRuntimeState.Faulted, status.State);
        Assert.Equal(UmbraThirdPartyPluginHost.MaximumConsecutiveCallbackErrors, status.ErrorCount);
        Assert.Contains("consecutive update failures", status.LastError, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("umbra_plugin_quarantined id=dev.aetherxiv.umbra.faulting-sample", File.ReadAllText(logPath));
    }

    [Fact]
    public async Task UmbraSafeModeDoesNotLoadThirdPartyPlugins()
    {
        string root = CreateTempDirectory();
        string pluginRoot = Path.Combine(root, "Plugins");
        string installRoot = Path.Combine(pluginRoot, "sdk-sample");
        Directory.CreateDirectory(installRoot);

        string assemblyName = "Aether.Umbra.SamplePlugin.dll";
        File.Copy(typeof(SamplePlugin).Assembly.Location, Path.Combine(installRoot, assemblyName));
        WriteManifest(installRoot, new UmbraPluginManifest(
            "dev.aetherxiv.umbra.sample",
            "Umbra SDK Sample",
            "0.1.0",
            "2.0",
            assemblyName,
            "0.1.0",
            true)
        {
            EntryType = typeof(SamplePlugin).FullName
        });

        string logPath = Path.Combine(root, "Logs", "umbra.log");
        UmbraRuntimeOptions options = CreateOptions(root, pluginRoot, logPath, safeMode: true);
        using UmbraRuntime runtime = await UmbraRuntime.StartAsync(options, UmbraRuntimeLog.Open(logPath));

        Assert.Empty(runtime.Plugins.Statuses);
        Assert.False(runtime.PluginManager.PluginExecutionEnabled);
        Assert.Contains("umbra_third_party_plugins_skipped=safe_mode", File.ReadAllText(logPath));
    }

    [Fact]
    public async Task UmbraPluginManagerPersistsEnableDisableAndReloadActions()
    {
        string root = CreateTempDirectory();
        string pluginRoot = Path.Combine(root, "Plugins");
        string installRoot = Path.Combine(pluginRoot, "sdk-sample");
        Directory.CreateDirectory(installRoot);
        File.Copy(typeof(SamplePlugin).Assembly.Location, Path.Combine(installRoot, "Aether.Umbra.SamplePlugin.dll"));
        WriteManifest(installRoot, new UmbraPluginManifest(
            "dev.aetherxiv.umbra.sample",
            "Umbra SDK Sample",
            "0.1.0",
            "2.0",
            "Aether.Umbra.SamplePlugin.dll",
            "0.1.0",
            false)
        {
            EntryType = typeof(SamplePlugin).FullName
        });

        string logPath = Path.Combine(root, "Logs", "umbra.log");
        using UmbraRuntime runtime = await UmbraRuntime.StartAsync(
            CreateOptions(root, pluginRoot, logPath, safeMode: false),
            UmbraRuntimeLog.Open(logPath));

        Assert.Empty(runtime.Plugins.Statuses);
        UmbraPluginActionResult enabled = runtime.SetPluginEnabled("dev.aetherxiv.umbra.sample", true);
        Assert.True(enabled.Succeeded, enabled.Message);
        Assert.True(UmbraPluginManifest.Load(Path.Combine(installRoot, "umbra-plugin.json")).Enabled);
        Assert.Equal(UmbraPluginRuntimeState.Running, Assert.Single(runtime.Plugins.Statuses).State);

        UmbraPluginActionResult reloaded = runtime.ReloadPlugin("dev.aetherxiv.umbra.sample");
        Assert.True(reloaded.Succeeded, reloaded.Message);
        Assert.Equal(UmbraPluginRuntimeState.Running, Assert.Single(runtime.Plugins.Statuses).State);

        UmbraPluginActionResult disabled = runtime.SetPluginEnabled("dev.aetherxiv.umbra.sample", false);
        Assert.True(disabled.Succeeded, disabled.Message);
        Assert.False(UmbraPluginManifest.Load(Path.Combine(installRoot, "umbra-plugin.json")).Enabled);
        Assert.Equal(UmbraPluginRuntimeState.Unloaded, Assert.Single(runtime.Plugins.Statuses).State);
    }

    [Fact]
    public async Task UmbraPluginManagerUninstallArchivesPluginForRecovery()
    {
        string root = CreateTempDirectory();
        string pluginRoot = Path.Combine(root, "Plugins");
        string installRoot = Path.Combine(pluginRoot, "sdk-sample");
        Directory.CreateDirectory(installRoot);
        File.Copy(typeof(SamplePlugin).Assembly.Location, Path.Combine(installRoot, "Aether.Umbra.SamplePlugin.dll"));
        WriteManifest(installRoot, new UmbraPluginManifest(
            "dev.aetherxiv.umbra.sample",
            "Umbra SDK Sample",
            "0.1.0",
            "2.0",
            "Aether.Umbra.SamplePlugin.dll",
            "0.1.0",
            true)
        {
            EntryType = typeof(SamplePlugin).FullName
        });

        string logPath = Path.Combine(root, "Logs", "umbra.log");
        using UmbraRuntime runtime = await UmbraRuntime.StartAsync(
            CreateOptions(root, pluginRoot, logPath, safeMode: false),
            UmbraRuntimeLog.Open(logPath));

        UmbraPluginActionResult result = runtime.UninstallPlugin("dev.aetherxiv.umbra.sample");
        Assert.True(result.Succeeded, result.Message);
        Assert.False(Directory.Exists(installRoot));
        Assert.Empty(runtime.PluginManager.InstalledPlugins);
        string trashRoot = Path.Combine(root, "Cache", "PluginTrash");
        string archive = Assert.Single(Directory.GetDirectories(trashRoot));
        Assert.True(File.Exists(Path.Combine(archive, "umbra-plugin.json")));
    }

    [Fact]
    public async Task UmbraPluginManagerRendersManagedCatalogState()
    {
        string root = CreateTempDirectory();
        string pluginRoot = Path.Combine(root, "Plugins");
        string logPath = Path.Combine(root, "Logs", "umbra.log");
        using UmbraRuntime runtime = await UmbraRuntime.StartAsync(
            CreateOptions(root, pluginRoot, logPath, safeMode: true),
            UmbraRuntimeLog.Open(logPath));
        runtime.RequestPluginManagerOpen();
        TestDrawContext drawContext = new();

        runtime.Draw(drawContext);

        Assert.Contains("Umbra Plugin Library###UmbraPluginManager", drawContext.WindowTitles);
        Assert.Contains("Umbra", drawContext.TextValues);
        Assert.Contains(drawContext.TextValues, value => value.Contains("No plugin manifests", StringComparison.Ordinal));

        (string _, float installedListWidth, float _) = Assert.Single(
            drawContext.Panels,
            panel => panel.Id == "##UmbraInstalledList");
        (string _, float installedDetailWidth, float _) = Assert.Single(
            drawContext.Panels,
            panel => panel.Id == "##UmbraInstalledDetails");
        Assert.True(installedListWidth > 0.0f);
        Assert.True(installedDetailWidth > 0.0f);
        Assert.True(installedListWidth + installedDetailWidth < drawContext.AvailableContentWidth);
        int installedDetailsIndex = drawContext.LayoutEvents.IndexOf("panel:##UmbraInstalledDetails");
        Assert.True(installedDetailsIndex > 0);
        Assert.Equal("same-line", drawContext.LayoutEvents[installedDetailsIndex - 1]);

        runtime.SetPluginManagerTab(UmbraPluginManagerTab.Supported);
        TestDrawContext storeDrawContext = new(520.0f);
        runtime.Draw(storeDrawContext);

        (string _, float storeListWidth, float _) = Assert.Single(
            storeDrawContext.Panels,
            panel => panel.Id == "##UmbraStoreList-Supported");
        (string _, float storeDetailWidth, float _) = Assert.Single(
            storeDrawContext.Panels,
            panel => panel.Id == "##UmbraStoreDetails-Supported");
        Assert.True(storeListWidth > 0.0f);
        Assert.True(storeDetailWidth > 0.0f);
        Assert.True(storeListWidth + storeDetailWidth < storeDrawContext.AvailableContentWidth);
        int storeDetailsIndex = storeDrawContext.LayoutEvents.IndexOf("panel:##UmbraStoreDetails-Supported");
        Assert.True(storeDetailsIndex > 0);
        Assert.Equal("same-line", storeDrawContext.LayoutEvents[storeDetailsIndex - 1]);
    }

    [Fact]
    public async Task UmbraCommandRegistryDispatchesAndReleasesPluginOwnedCommands()
    {
        string root = CreateTempDirectory();
        string logPath = Path.Combine(root, "Logs", "umbra.log");
        using UmbraRuntime runtime = await UmbraRuntime.StartAsync(
            CreateOptions(root, Path.Combine(root, "Plugins"), logPath, safeMode: true),
            UmbraRuntimeLog.Open(logPath));

        IUmbraCommandManager first = runtime.Commands.CreateScope("dev.aetherxiv.first");
        IUmbraCommandManager second = runtime.Commands.CreateScope("dev.aetherxiv.second");
        UmbraCommandInvocation? received = null;
        using IDisposable registration = first.Register(
            new UmbraCommandRegistration("/umbra-test", "Exercises the command dispatcher."),
            invocation => received = invocation);

        UmbraCommandDispatchResult result = second.Dispatch("/UMBRA-TEST alpha beta");

        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(received);
        Assert.Equal("/umbra-test", received.Command);
        Assert.Equal("alpha beta", received.Arguments);
        Assert.Equal("dev.aetherxiv.first", Assert.Single(first.Commands).PluginId);
        Assert.Throws<InvalidOperationException>(() => second.Register(
            new UmbraCommandRegistration("umbra-test"),
            _ => { }));

        runtime.Commands.Release("dev.aetherxiv.first");
        Assert.Empty(second.Commands);
        Assert.Equal(UmbraCommandDispatchStatus.NotFound, second.Dispatch("/umbra-test").Status);
    }

    [Fact]
    public async Task UmbraPluginContextGatesCommandAndChatServicesByCapability()
    {
        string root = CreateTempDirectory();
        string logPath = Path.Combine(root, "Logs", "umbra.log");
        using UmbraRuntime runtime = await UmbraRuntime.StartAsync(
            CreateOptions(root, Path.Combine(root, "Plugins"), logPath, safeMode: true),
            UmbraRuntimeLog.Open(logPath));

        UmbraPluginContext denied = new(runtime, "dev.aetherxiv.denied", []);
        Assert.Null(denied.GetService<IUmbraCommandManager>());
        Assert.Null(denied.GetService<IUmbraChat>());
        Assert.Null(denied.GetService<IUmbraActorAppearanceService>());

        UmbraPluginContext allowed = new(runtime, "dev.aetherxiv.allowed",
            [UmbraCapabilities.CommandRegistration, UmbraCapabilities.ChatPrint]);
        Assert.NotNull(allowed.GetService<IUmbraCommandManager>());
        IUmbraChat chat = Assert.IsAssignableFrom<IUmbraChat>(allowed.GetService<IUmbraChat>());
        Assert.False(chat.Availability.CanPrint);
        Assert.False(chat.Availability.CanSubmit);
        Assert.Equal("ffxiv-1.23b-unresolved", chat.Availability.ClientAdapter);

        UmbraPluginContext appearanceReader = new(runtime, "dev.aetherxiv.appearance-reader",
            [UmbraCapabilities.ActorAppearanceRead]);
        IUmbraActorAppearanceService appearance = Assert.IsAssignableFrom<IUmbraActorAppearanceService>(
            appearanceReader.GetService<IUmbraActorAppearanceService>());
        Assert.False(appearance.Availability.IsAvailable);
        Assert.Contains("No verified", appearance.Availability.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UmbraChatReportsUnresolvedNativeAdapterAndEnforcesClientBuffer()
    {
        string root = CreateTempDirectory();
        string logPath = Path.Combine(root, "Logs", "umbra.log");
        using UmbraRuntime runtime = await UmbraRuntime.StartAsync(
            CreateOptions(root, Path.Combine(root, "Plugins"), logPath, safeMode: true),
            UmbraRuntimeLog.Open(logPath));
        IUmbraChat chat = runtime.Chat.CreateScope(
            "dev.aetherxiv.chat",
            allowPrint: true,
            allowSubmit: false);

        Assert.Equal(UmbraChatDeliveryStatus.Unavailable, chat.Print("hello", "Umbra").Status);
        Assert.Equal(UmbraChatDeliveryStatus.Denied, chat.Submit("!help").Status);
        Assert.Equal(
            UmbraChatDeliveryStatus.Rejected,
            chat.Print(new string('x', UmbraChatService.MaximumMessageBytes + 1)).Status);
    }

    private static UmbraRuntimeOptions CreateOptions(
        string root,
        string pluginRoot,
        string logPath,
        bool safeMode)
    {
        string cache = Path.Combine(root, "Cache");
        string devBridge = Path.Combine(cache, "DevBridge");
        return new UmbraRuntimeOptions(
            logPath,
            pluginRoot,
            cache,
            devBridge,
            Path.Combine(devBridge, "control.json"),
            false,
            UmbraRuntimeOptions.DefaultDevBridgePort,
            safeMode,
            Array.Empty<string>(),
            Array.Empty<UmbraRepositorySource>());
    }

    private static void WriteManifest(string installRoot, UmbraPluginManifest manifest)
    {
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        };
        File.WriteAllText(Path.Combine(installRoot, "umbra-plugin.json"), JsonSerializer.Serialize(manifest, options));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetherxiv-umbra-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestDrawContext : Aether.Umbra.PluginApi.IUmbraDrawContext
    {
        private readonly float contentWidth;

        public TestDrawContext(float contentWidth = 900.0f)
        {
            this.contentWidth = contentWidth;
        }

        public List<string> WindowTitles { get; } = new();
        public List<string> TextValues { get; } = new();
        public List<(string Id, float Width, float Height)> Panels { get; } = new();
        public List<string> LayoutEvents { get; } = new();
        public ulong FrameNumber => 1;
        public TimeSpan DeltaTime => TimeSpan.FromMilliseconds(16);
        public int ViewportWidth => 1280;
        public int ViewportHeight => 720;
        public float AvailableContentWidth => contentWidth;
        public float ContentRegionWidth => contentWidth;
        public int DeviceGeneration => 0;
        public bool IsRenderThread => true;
        public bool IsPluginManagerOpen => false;
        public void RequestPluginManagerOpen() { }
        public bool BeginWindow(string title, ref bool isOpen)
        {
            WindowTitles.Add(title);
            return true;
        }
        public void EndWindow() { }
        public void Text(string text) => TextValues.Add(text);
        public void Text(string text, Aether.Umbra.PluginApi.UmbraTextTone tone) => TextValues.Add(text);
        public void Text(
            string text,
            Aether.Umbra.PluginApi.UmbraTextTone tone,
            Aether.Umbra.PluginApi.UmbraTextStyle style) => TextValues.Add(text);
        public bool InputText(string label, ref string value, string hint = "", int maximumLength = 256) => false;
        public bool Button(string label) => false;
        public bool Button(
            string label,
            Aether.Umbra.PluginApi.UmbraButtonStyle style,
            Aether.Umbra.PluginApi.UmbraIcon icon = Aether.Umbra.PluginApi.UmbraIcon.None,
            float width = 0.0f,
            float height = 0.0f) => false;
        public bool Checkbox(string label, ref bool value) => false;
        public bool Toggle(string label, ref bool value) => false;
        public void SameLine() => LayoutEvents.Add("same-line");
        public void Separator() { }
        public void Spacing(float height = 8.0f) { }
        public void Icon(
            Aether.Umbra.PluginApi.UmbraIcon icon,
            Aether.Umbra.PluginApi.UmbraTextTone tone = Aether.Umbra.PluginApi.UmbraTextTone.Normal,
            float size = 20.0f) { }
        public void Badge(
            string text,
            Aether.Umbra.PluginApi.UmbraTextTone tone,
            Aether.Umbra.PluginApi.UmbraIcon icon = Aether.Umbra.PluginApi.UmbraIcon.None) => TextValues.Add(text);
        public void Artwork(
            string seed,
            Aether.Umbra.PluginApi.UmbraIcon icon = Aether.Umbra.PluginApi.UmbraIcon.Plug,
            float size = 72.0f) { }
        public void SetNextWindowSize(float width, float height, bool firstUseOnly = true) { }
        public bool BeginChild(string id, float height, bool border = true) => true;
        public bool BeginPanel(
            string id,
            float width,
            float height,
            Aether.Umbra.PluginApi.UmbraPanelStyle style = Aether.Umbra.PluginApi.UmbraPanelStyle.Card)
        {
            Panels.Add((id, width, height));
            LayoutEvents.Add($"panel:{id}");
            return true;
        }
        public void EndChild() { }
    }
}
