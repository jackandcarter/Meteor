using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AetherXIV.Core;
using AetherXIV.Launcher.Core;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;

namespace AetherXIV.Launcher.App;

public sealed partial class MainWindow : Window
{
    private sealed record AmbientParticle(
        Ellipse Shape,
        double X,
        double Y,
        double Speed,
        double Drift,
        double Phase);

    private sealed record HomeReelFrame(string ImageFile, Bitmap Bitmap);

    private readonly HttpClient httpClient = new();
    private readonly LauncherPlatform platform = LauncherPlatform.Current;
    private CancellationTokenSource? patchCancellation;
    private bool runtimeBusy;
    private LauncherConfig? launcherConfig;
    private RuntimeArtifact? selectedRuntimeArtifact;
    private ManagedRuntimeInstall? managedRuntimeInstall;
    private UmbraFrameworkCatalog? umbraFrameworkCatalog;
    private UmbraFrameworkArtifact? selectedUmbraFrameworkArtifact;
    private UmbraFrameworkInstall? umbraFrameworkInstall;
    private IReadOnlyList<RuntimeCandidate> runtimeCandidates = Array.Empty<RuntimeCandidate>();
    private string? currentSessionId;
    private string? currentSessionUsername;
    private bool launchInProgress;
    private bool ffxivSettingsInProgress;
    private bool isInitialized;
    private bool isLoadingProfile;
    private bool runtimeSetupPromptShown;
    private CancellationTokenSource? umbraCancellation;
    private readonly DispatcherTimer homeReelTimer;
    private readonly DispatcherTimer particleTimer;
    private readonly List<AmbientParticle> ambientParticles = [];
    private readonly Random particleRandom = new(0xA14);
    private IReadOnlyList<HomeReelFrame> homeReelImages = Array.Empty<HomeReelFrame>();
    private IReadOnlyDictionary<string, LauncherReelText> homeReelText = new Dictionary<string, LauncherReelText>();
    private bool homeReelTextEnabled;
    private int homeReelIndex;
    private bool showBuildNumber;

    private const int ServerPresetLocalhost = 0;
    private const int ServerPresetDemiDevUnit = 1;
    private const int ServerPresetCustom = 2;
    private const string DiscordInviteUrl = "https://discord.gg/9w4Bjqu3Tj";
    private const int HomeReelIntervalSeconds = 7;
    private const string HomeBackgroundAsset = "avares://AetherXIV.Launcher.App/Image/back.jpg";
    private const string HomeReelAssetFolder = "avares://AetherXIV.Launcher.App/Image/Reels/";

    public MainWindow()
    {
        InitializeComponent();
        isInitialized = true;
        MainTabs.SelectionChanged += MainTabs_SelectionChanged;
        ConfigurePlatformUi();
        homeReelTimer = CreateHomeReelTimer();
        particleTimer = CreateParticleTimer();
        CreateAmbientParticles();
        LoadHomeReelImages();
        UpdateHomeReelImage();
        homeReelTimer.Start();
        particleTimer.Start();
        LoadSavedProfile();
        AppendLog("AetherXIV Launcher initialized.");
        selectedRuntimeArtifact = BuiltInRuntimeCatalog.Find(platform.RuntimeIdentifier);
        ScanRuntimeCandidates(false);
        RefreshRuntimeSetupStatus();
        RefreshInstalledUmbraFrameworkStatus();
        ValidateClientIfSelected();
        UpdateHomeState();
        ApplyWindowSizeForSelectedTab();
        Closing += (_, _) =>
        {
            homeReelTimer.Stop();
            particleTimer.Stop();
            foreach (HomeReelFrame image in homeReelImages)
                image.Bitmap.Dispose();
            SaveCurrentProfile();
        };
        _ = InitializeAsync();
    }

    private void ProductVersionText_PointerPressed(
        object? sender,
        Avalonia.Input.PointerPressedEventArgs e)
    {
        showBuildNumber = !showBuildNumber;
        ProductVersionText.Text = showBuildNumber
            ? AetherXivBuildInfo.BuildText
            : AetherXivBuildInfo.VersionText;
    }

    private void LoadHomeReelImages()
    {
        List<Uri> assets = [new Uri(HomeBackgroundAsset)];
        assets.AddRange(Avalonia.Platform.AssetLoader
            .GetAssets(new Uri(HomeReelAssetFolder), null)
            .OrderBy(uri => uri.AbsolutePath, StringComparer.OrdinalIgnoreCase));

        List<HomeReelFrame> images = new(assets.Count);
        foreach (Uri asset in assets)
        {
            string imageFile = asset.Segments[^1];
            images.Add(new HomeReelFrame(
                imageFile,
                new Bitmap(Avalonia.Platform.AssetLoader.Open(asset))));
        }

        homeReelImages = images;
    }

    private DispatcherTimer CreateHomeReelTimer()
    {
        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromSeconds(HomeReelIntervalSeconds)
        };
        timer.Tick += (_, _) => AdvanceHomeReel(1, false);
        return timer;
    }

    private DispatcherTimer CreateParticleTimer()
    {
        DispatcherTimer timer = new()
        {
            Interval = TimeSpan.FromMilliseconds(32)
        };
        timer.Tick += (_, _) => UpdateAmbientParticles();
        return timer;
    }

    private void CreateAmbientParticles()
    {
        Color[] colors =
        [
            Color.FromArgb(245, 70, 190, 255),
            Color.FromArgb(240, 116, 111, 255),
            Color.FromArgb(235, 182, 83, 255),
            Color.FromArgb(230, 66, 231, 255)
        ];

        for (int index = 0; index < 56; index++)
        {
            Color color = colors[index % colors.Length];
            double size = 2.2 + (particleRandom.NextDouble() * 5.2);
            Ellipse shape = new()
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(color),
                Opacity = 0.58 + (particleRandom.NextDouble() * 0.34),
                Effect = new DropShadowEffect
                {
                    Color = color,
                    BlurRadius = 8 + (size * 0.8),
                    OffsetX = 0,
                    OffsetY = 0,
                    Opacity = 0.88
                },
                IsHitTestVisible = false
            };

            HomeParticleCanvas.Children.Add(shape);
            ambientParticles.Add(new AmbientParticle(
                shape,
                particleRandom.NextDouble(),
                particleRandom.NextDouble(),
                0.85 + (particleRandom.NextDouble() * 1.45),
                -0.28 + (particleRandom.NextDouble() * 0.56),
                particleRandom.NextDouble() * Math.Tau));
        }
    }

    private void UpdateAmbientParticles()
    {
        double width = Math.Max(HomeParticleCanvas.Bounds.Width, 1);
        double height = Math.Max(HomeParticleCanvas.Bounds.Height, 1);
        for (int index = 0; index < ambientParticles.Count; index++)
        {
            AmbientParticle particle = ambientParticles[index];
            double nextY = particle.Y - (particle.Speed / height);
            double nextPhase = particle.Phase + 0.052;
            if (nextY < -0.08)
            {
                nextY = 1.08;
                particle = particle with
                {
                    X = particleRandom.NextDouble(),
                    Speed = 0.85 + (particleRandom.NextDouble() * 1.45),
                    Drift = -0.28 + (particleRandom.NextDouble() * 0.56)
                };
            }

            double drift = Math.Sin(nextPhase) * particle.Drift * 18;
            Canvas.SetLeft(particle.Shape, (particle.X * width) + drift);
            Canvas.SetTop(particle.Shape, nextY * height);
            ambientParticles[index] = particle with
            {
                Y = nextY,
                Phase = nextPhase
            };
        }
    }

    private void HomeReelPrevious_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AdvanceHomeReel(-1, true);
    }

    private void HomeReelNext_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        AdvanceHomeReel(1, true);
    }

    private void AdvanceHomeReel(int delta, bool resetTimer)
    {
        if (homeReelImages.Count == 0)
            return;

        homeReelIndex = (homeReelIndex + delta) % homeReelImages.Count;
        if (homeReelIndex < 0)
            homeReelIndex += homeReelImages.Count;

        UpdateHomeReelImage();
        if (!resetTimer)
            return;

        homeReelTimer.Stop();
        homeReelTimer.Start();
    }

    private void UpdateHomeReelImage()
    {
        if (homeReelImages.Count == 0)
            return;

        HomeReelFrame frame = homeReelImages[homeReelIndex];
        HomeReelImage.Source = frame.Bitmap;
        ApplyHomeReelText(frame.ImageFile);
    }

    private void ApplyHomeReelText(string imageFile)
    {
        if (!homeReelTextEnabled ||
            !homeReelText.TryGetValue(imageFile, out LauncherReelText? reelText) ||
            !reelText.IsEnabled ||
            (String.IsNullOrWhiteSpace(reelText.HeaderText) && String.IsNullOrWhiteSpace(reelText.SubText)))
        {
            HomeReelTextOverlay.IsVisible = false;
            return;
        }

        HomeReelHeaderText.Text = reelText.HeaderText;
        HomeReelHeaderText.FontSize = Math.Clamp(reelText.HeaderSize, 12, 72);
        HomeReelHeaderText.Foreground = ParseBrush(reelText.HeaderColor, "#FFFFFFFF");
        HomeReelHeaderText.IsVisible = !String.IsNullOrWhiteSpace(reelText.HeaderText);
        HomeReelSubText.Text = reelText.SubText;
        HomeReelSubText.FontSize = Math.Clamp(reelText.SubTextSize, 10, 48);
        HomeReelSubText.Foreground = ParseBrush(reelText.SubTextColor, "#FFD7E0EE");
        HomeReelSubText.IsVisible = !String.IsNullOrWhiteSpace(reelText.SubText);
        HomeReelTextOverlay.IsVisible = true;
    }

    private async Task InitializeAsync()
    {
        await RefreshLauncherServicesAsync();
        await ShowRuntimeSetupPromptIfNeededAsync();
    }

    private async void RefreshServices_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RefreshLauncherServicesAsync();
    }

    private void SaveSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SaveCurrentProfile();
        AppendLog($"Settings saved: {ProfileStore.DefaultProfilePath}");
    }

    private void MainTabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isInitialized)
            return;

        ApplyWindowSizeForSelectedTab();
    }

    private void ApplyWindowSizeForSelectedTab()
    {
        (double minWidth, double minHeight, double targetWidth, double targetHeight) = MainTabs.SelectedIndex switch
        {
            0 => (1180, 760, 1280, 800),
            1 => (980, 680, 1080, 720),
            2 => (1040, 720, 1160, 760),
            3 => (1040, 720, 1160, 760),
            _ => (980, 680, 1080, 720)
        };

        MinWidth = minWidth;
        MinHeight = minHeight;

        if (WindowState != WindowState.Normal)
            return;

        if (Width < minWidth)
            Width = targetWidth;
        if (Height < minHeight)
            Height = targetHeight;
    }

    private void OpenDiscord_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DiscordInviteUrl,
                UseShellExecute = true
            });
            AppendLog("Discord invite opened.");
        }
        catch (Exception ex)
        {
            AppendLog($"Discord invite failed to open: {ex.Message}");
        }
    }

    private void ServerPreset_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isInitialized || isLoadingProfile)
            return;

        ApplySelectedServerPreset();
        SaveCurrentProfile();
    }

    private void ApplySelectedServerPreset()
    {
        if (ServerPresetBox.SelectedIndex == ServerPresetLocalhost)
            ApplyLocalhostServerPreset();
        else if (ServerPresetBox.SelectedIndex == ServerPresetDemiDevUnit)
            ApplyDemiDevUnitServerPreset();
        else
            ServerPresetStatus.Text = "Custom server setup. Edit the service URL, host, and ports below.";

        UpdateServerPresetUiState();
    }

    private void ApplyLocalhostServerPreset()
    {
        LauncherProfile localProfile = LauncherProfile.LocalDefault();
        ServerProfile localServer = localProfile.ServerProfile;

        LauncherServiceUrlBox.Text = localProfile.LauncherServiceUrl;
        PatchBaseUrlBox.Text = localProfile.PatchBaseUrl;
        ServerNameBox.Text = localServer.Name;
        ServerHostBox.Text = localServer.Host;
        LobbyPortBox.Value = localServer.LobbyPort;
        WorldPortBox.Value = localServer.WorldPort;
        ServerPresetStatus.Text = "Local testing on this machine.";
    }

    private void ApplyDemiDevUnitServerPreset()
    {
        LauncherProfile devUnitProfile = LauncherProfile.DemiDevUnitDefault();
        ServerProfile devUnitServer = devUnitProfile.ServerProfile;

        LauncherServiceUrlBox.Text = devUnitProfile.LauncherServiceUrl;
        PatchBaseUrlBox.Text = devUnitProfile.PatchBaseUrl;
        ServerNameBox.Text = devUnitServer.Name;
        ServerHostBox.Text = devUnitServer.Host;
        LobbyPortBox.Value = devUnitServer.LobbyPort;
        WorldPortBox.Value = devUnitServer.WorldPort;
        ServerPresetStatus.Text = "Public Demi Dev Unit developer server.";
    }

    private void SelectServerPresetForProfile(LauncherProfile profile)
    {
        LauncherProfile localProfile = LauncherProfile.LocalDefault();
        ServerProfile localServer = localProfile.ServerProfile;
        LauncherProfile devUnitProfile = LauncherProfile.DemiDevUnitDefault();
        ServerProfile devUnitServer = devUnitProfile.ServerProfile;
        bool isLocalProfile =
            IsSameText(profile.LauncherServiceUrl, localProfile.LauncherServiceUrl)
            && IsSameText(profile.ServerProfile.Host, localServer.Host)
            && profile.ServerProfile.LobbyPort == localServer.LobbyPort
            && profile.ServerProfile.WorldPort == localServer.WorldPort;
        bool isDevUnitProfile =
            IsSameText(profile.LauncherServiceUrl, devUnitProfile.LauncherServiceUrl)
            && IsSameText(profile.ServerProfile.Host, devUnitServer.Host)
            && profile.ServerProfile.LobbyPort == devUnitServer.LobbyPort
            && profile.ServerProfile.WorldPort == devUnitServer.WorldPort;

        ServerPresetBox.SelectedIndex = isLocalProfile
            ? ServerPresetLocalhost
            : isDevUnitProfile
                ? ServerPresetDemiDevUnit
                : ServerPresetCustom;
        if (isLocalProfile)
            ApplyLocalhostServerPreset();
        else if (isDevUnitProfile)
            ApplyDemiDevUnitServerPreset();
        else
            ServerPresetStatus.Text = "Custom server setup. Edit the service URL, host, and ports below.";

        UpdateServerPresetUiState();
    }

    private void UpdateServerPresetUiState()
    {
        bool isCustom = ServerPresetBox.SelectedIndex == ServerPresetCustom;
        LauncherServiceUrlBox.IsEnabled = isCustom;
        PatchBaseUrlBox.IsEnabled = isCustom;
        ServerNameBox.IsEnabled = isCustom;
        ServerHostBox.IsEnabled = isCustom;
        LobbyPortBox.IsEnabled = isCustom;
        WorldPortBox.IsEnabled = isCustom;
    }

    private static bool IsSameText(string? left, string? right) =>
        string.Equals(left ?? "", right ?? "", StringComparison.OrdinalIgnoreCase);

    private void ValidateClient_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ValidateClient();
    }

    private async void OpenFfxivSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ffxivSettingsInProgress)
            return;

        SetFfxivSettingsProgress("Preparing FFXIV settings...", 5);
        try
        {
            ffxivSettingsInProgress = true;
            FfxivSettingsButton.IsEnabled = false;

            SaveCurrentProfile();
            SetFfxivSettingsProgress("Checking selected client...", 15);
            ClientInstall clientInstall = ClientInstall.FromPath(ClientPathBox.Text ?? "");
            if (!File.Exists(clientInstall.ConfigExecutablePath))
            {
                SetFfxivSettingsProgress("FFXIV settings blocked: config tool binary was not found.", 0);
                AppendLog("FFXIV settings blocked: ffxivconfig.exe was not found in the selected client root.");
                return;
            }

            WineRuntimeProfile runtimeProfile = ReadRuntimeProfile();
            FfxivConfigStorageTarget storageTarget;
            if (platform.RequiresCompatibilityRuntime)
            {
                SetFfxivSettingsProgress("Checking Wine runtime...", 30);
                ManagedRuntimeStatus.Text = "Validating runtime before opening FFXIV settings...";
                RuntimeValidationResult validation = await RuntimeValidator.ValidateAsync(
                    runtimeProfile,
                    RuntimeInstallStore.ManagedPrefixPath);
                AppendLog($"Runtime validation: {validation.Message}");
                AppendLog($"Runtime validation log: {validation.LogPath}");
                if (!validation.IsReady)
                {
                    SetFfxivSettingsProgress("FFXIV settings blocked: Wine runtime is not ready.", 0);
                    ManagedRuntimeStatus.Text = validation.Message;
                    AppendLog("FFXIV settings blocked: runtime validation failed.");
                    return;
                }

                SetFfxivSettingsProgress("Preparing Wine prefix...", 55);
                WineRuntimeConfigurationResult configuration = await WineRuntimeConfigurator.ConfigureAsync(
                    runtimeProfile,
                    RuntimeInstallStore.ManagedPrefixPath,
                    new WineRuntimeConfigurationSettings(platform.OperatingSystem));
                AppendLog($"Runtime config: {configuration.Message}");
                AppendLog($"Runtime config target: {configuration.RuntimeTarget}");
                AppendLog($"Runtime config log: {configuration.LogPath}");
                if (!configuration.IsReady)
                {
                    SetFfxivSettingsProgress("FFXIV settings blocked: Wine setup failed.", 0);
                    ManagedRuntimeStatus.Text = configuration.Message;
                    AppendLog("FFXIV settings blocked: runtime setup failed.");
                    return;
                }

                SetFfxivSettingsProgress("Locating FFXIV config files...", 75);
                if (!FfxivClientSettingsStore.TryResolveWineTarget(
                        runtimeProfile,
                        RuntimeInstallStore.ManagedPrefixPath,
                        out storageTarget,
                        out string storageError))
                {
                    SetFfxivSettingsProgress("FFXIV settings blocked: config folder could not be located.", 0);
                    AppendLog($"FFXIV settings blocked: {storageError}");
                    return;
                }
            }
            else
            {
                SetFfxivSettingsProgress("Locating FFXIV config files...", 75);
                storageTarget = FfxivClientSettingsStore.ResolveNativeWindowsTarget();
            }

            SetFfxivSettingsProgress("Opening FFXIV settings...", 90);
            FfxivSettingsWindow dialog = new(clientInstall, storageTarget);
            bool saved = await dialog.ShowDialog<bool>(this);
            if (!saved)
            {
                SetFfxivSettingsProgress("FFXIV settings closed.", 100);
                AppendLog("FFXIV settings cancelled.");
                return;
            }

            SetFfxivSettingsProgress("FFXIV settings saved.", 100);
            AppendLog($"FFXIV settings saved: {storageTarget.HostConfigDirectoryPath}");
            if (dialog.LastSaveResult is { } result)
            {
                if (result.CreatedSystemConfig)
                    AppendLog($"FFXIV settings created config.sys: {result.SystemConfigPath}");
                else if (result.RepairedSystemConfig)
                    AppendLog($"FFXIV settings repaired config.sys: {result.SystemConfigPath}");

                foreach (string backupPath in result.BackupPaths)
                    AppendLog($"FFXIV settings backup: {backupPath}");
            }
        }
        catch (Exception ex)
        {
            SetFfxivSettingsProgress("FFXIV settings failed.", 0);
            AppendLog($"FFXIV settings failed: {ex.Message}");
        }
        finally
        {
            ffxivSettingsInProgress = false;
            FfxivSettingsButton.IsEnabled = true;
        }
    }

    private async void LocateClientFromHome_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 2;
        await SelectClientExecutableAsync();
    }

    private async void BrowseClient_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await SelectClientExecutableAsync();
    }

    private async Task SelectClientExecutableAsync()
    {
        if (!StorageProvider.CanOpen)
        {
            AppendLog("Client executable picker is not available on this platform.");
            return;
        }

        FilePickerFileType clientExecutableType = new("FFXIV 1.x executable")
        {
            Patterns = new[] { "ffxivboot.exe", "ffxivgame.exe", "*.exe" },
            AppleUniformTypeIdentifiers = new[] { "public.executable", "com.microsoft.windows-executable", "public.item" }
        };

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select ffxivboot.exe or ffxivgame.exe",
            AllowMultiple = false,
            SuggestedFileType = clientExecutableType,
            FileTypeFilter = new[] { clientExecutableType, FilePickerFileTypes.All }
        });

        if (files.Count == 0)
        {
            AppendLog("Client executable selection cancelled.");
            return;
        }

        string? selectedPath = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            AppendLog("Client executable selection failed: local path is unavailable.");
            return;
        }

        string fileName = Path.GetFileName(selectedPath);
        if (!IsKnownClientExecutable(fileName))
            AppendLog($"Client executable note: expected ffxivboot.exe or ffxivgame.exe, selected {fileName}.");

        string? clientRoot = Path.GetDirectoryName(selectedPath);
        if (string.IsNullOrWhiteSpace(clientRoot))
        {
            AppendLog("Client executable selection failed: containing folder is unavailable.");
            return;
        }

        ClientPathBox.Text = clientRoot;
        AppendLog($"Client root selected: {clientRoot}");
        ValidateClient();
        SaveCurrentProfile();
    }

    private async void BrowsePatchLibrary_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!StorageProvider.CanPickFolder)
        {
            AppendLog("Patch library folder picker is not available on this platform.");
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select FFXIV 1.x patch library folder",
            AllowMultiple = false
        });

        if (folders.Count == 0)
        {
            AppendLog("Patch library folder selection cancelled.");
            return;
        }

        string? selectedPath = folders[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            AppendLog("Patch library folder selection failed: local path is unavailable.");
            return;
        }

        PatchLibraryPathBox.Text = selectedPath;
        AppendLog($"Patch library root selected: {selectedPath}");
        ValidatePatchLibrary();
        SaveCurrentProfile();
    }

    private void ValidateClientIfSelected()
    {
        if (!string.IsNullOrWhiteSpace(ClientPathBox.Text))
            ValidateClient();
    }

    private void ValidateClient()
    {
        try
        {
            ClientInstall clientInstall = ClientInstall.FromPath(ClientPathBox.Text ?? "");
            ClientInstallReport report = clientInstall.Inspect();

            ClientVersionStatus.Text = $"{report.State}: {report.Version.DisplayText}";
            ExecutableStatus.Text = FormatExecutableStatus(report);
            StaticActorsStatus.Text = report.HasStaticActors ? clientInstall.StaticActorsSourcePath : "Not found";
            HomeClientStatus.Text = report.IsLaunchReady
                ? $"Client: ready ({report.Version.GameVersion})"
                : $"Client: {report.State}";
            HomeClientVersionStatus.Text = $"Version: {report.Version.DisplayText}";
            UpdateLaunchButtonState(report.IsLaunchReady);
            HomeLocateClientButton.IsVisible = false;

            AppendLog($"Client state: {report.State} ({report.Version.DisplayText})");
            foreach (string action in report.RequiredActions)
                AppendLog($"Client action: {action}");

            ValidatePatchLibrary();
        }
        catch (Exception ex)
        {
            ClientVersionStatus.Text = "Validation failed";
            ExecutableStatus.Text = "Validation failed";
            StaticActorsStatus.Text = "Validation failed";
            HomeClientStatus.Text = "Client: validation failed";
            HomeClientVersionStatus.Text = "Version: validation failed";
            UpdateLaunchButtonState(false);
            HomeLocateClientButton.IsVisible = true;
            AppendLog($"Client validation failed: {ex.Message}");
        }
        finally
        {
            UpdateHomeState();
        }
    }

    private async void DownloadPatches_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (patchCancellation is not null)
            return;

        patchCancellation = new CancellationTokenSource();
        ApplyPatchesButton.IsEnabled = false;
        DownloadPatchesButton.IsEnabled = false;
        CancelPatchButton.IsEnabled = true;
        PatchApplyProgressBar.Value = 0;
        HomeProgressBar.Value = 0;
        PatchApplyStatus.Text = "Preparing patch download...";

        try
        {
            CancellationToken cancellationToken = patchCancellation.Token;
            LauncherPatchManifest manifest = await ResolvePatchManifestAsync(cancellationToken);
            string patchRoot = ResolvePatchLibraryRoot();
            PatchLibraryPathBox.Text = patchRoot;

            Progress<PatchDownloadProgress> progress = new(update =>
            {
                PatchApplyStatus.Text = update.Message;
                if (update.TotalSizeBytes > 0)
                {
                    double percent = (double)update.TotalBytesDownloaded / update.TotalSizeBytes * 100;
                    PatchApplyProgressBar.Value = Math.Clamp(percent, 0, 100);
                    HomeProgressBar.Value = Math.Clamp(percent, 0, 100);
                }

                if (update.LogMessage)
                    AppendLog(update.Message);
            });

            PatchDownloadResult result = await PatchDownloadService.DownloadPatchLibraryAsync(
                manifest,
                patchRoot,
                httpClient,
                progress,
                cancellationToken);

            AppendLog($"Patch download complete: {result.DownloadedFileCount} downloaded, {result.ReusedFileCount} reused.");
            PatchApplyStatus.Text = "Patch download complete.";
            PatchApplyProgressBar.Value = 100;
            HomeProgressBar.Value = 100;
            ValidatePatchLibrary();
            SaveCurrentProfile();
        }
        catch (OperationCanceledException)
        {
            AppendLog("Patch download cancelled.");
            PatchApplyStatus.Text = "Patch download cancelled.";
        }
        catch (Exception ex)
        {
            AppendLog($"Patch download failed: {ex.Message}");
            PatchApplyStatus.Text = "Patch download failed.";
        }
        finally
        {
            ApplyPatchesButton.IsEnabled = true;
            DownloadPatchesButton.IsEnabled = true;
            CancelPatchButton.IsEnabled = false;
            patchCancellation.Dispose();
            patchCancellation = null;
        }
    }

    private async void ApplyPatches_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (patchCancellation is not null)
            return;

        patchCancellation = new CancellationTokenSource();
        ApplyPatchesButton.IsEnabled = false;
        DownloadPatchesButton.IsEnabled = false;
        CancelPatchButton.IsEnabled = true;
        PatchApplyProgressBar.Value = 0;
        PatchApplyProgressBar.IsIndeterminate = false;
        HomeProgressBar.Value = 0;
        PatchApplyStatus.Text = "Preparing patch apply...";

        try
        {
            CancellationToken cancellationToken = patchCancellation.Token;
            ClientInstall clientInstall = ClientInstall.FromPath(ClientPathBox.Text ?? "");
            ClientInstallReport clientReport = clientInstall.Inspect();
            if (clientReport.State == ClientInstallState.Ready123b)
            {
                AppendLog("Patch apply skipped: client already reports 1.23b.");
                PatchApplyStatus.Text = "Client already reports 1.23b.";
                return;
            }

            string patchLibraryPath = PatchLibraryPathBox.Text ?? "";
            PatchApplyStatus.Text = "Verifying patch library checksums...";
            PatchApplyProgressBar.IsIndeterminate = true;
            AppendLog("Patch verification started.");

            PatchLibraryReport patchReport = await Task.Run(
                () => LegacyPatchManifest.InspectLibrary(
                    patchLibraryPath,
                    PatchLibraryInspectionMode.Checksum),
                cancellationToken);

            PatchLibraryStatus.Text = patchReport.Summary;
            AppendLog($"Patch library: {patchReport.Summary}");
            AppendLog($"Patch base: {patchReport.PatchBasePath}");
            PatchApplyProgressBar.IsIndeterminate = false;
            PatchApplyProgressBar.Value = 0;

            if (!patchReport.IsPatchChainReady)
            {
                LogMissingEntries("Missing patch", patchReport.MissingPatchFiles);
                LogInvalidEntries(patchReport.InvalidPatchFiles);
                AppendLog("Patch apply blocked: patch library is not checksum-ready.");
                PatchApplyStatus.Text = "Patch library is not checksum-ready.";
                return;
            }

            Progress<PatchApplyProgress> progress = new(update =>
            {
                PatchApplyStatus.Text = update.Message;
                if (update.TotalBytes > 0)
                {
                    double percent = (double)update.BytesProcessed / update.TotalBytes * 100;
                    PatchApplyProgressBar.Value = Math.Clamp(percent, 0, 100);
                    HomeProgressBar.Value = Math.Clamp(percent, 0, 100);
                }

                if (update.LogMessage)
                    AppendLog(update.Message);
            });

            PatchApplyResult result = await Task.Run(() =>
                LegacyPatchApplier.ApplyPatchChain(clientInstall, patchReport, progress, cancellationToken),
                cancellationToken);

            AppendLog($"Patch apply complete: {result.AppliedPatchCount} patch files applied.");
            PatchApplyProgressBar.Value = 100;
            HomeProgressBar.Value = 100;
            PatchApplyStatus.Text = "Patch apply complete.";
            foreach (string message in result.Messages.Take(8))
                AppendLog(message);

            if (result.Messages.Count > 8)
                AppendLog($"Patch apply notes: {result.Messages.Count - 8} more");

            ClientInstallReport updatedReport = clientInstall.Inspect();
            ClientVersionStatus.Text = $"{updatedReport.State}: {updatedReport.Version.DisplayText}";
            ExecutableStatus.Text = FormatExecutableStatus(updatedReport);
            StaticActorsStatus.Text = updatedReport.HasStaticActors ? clientInstall.StaticActorsSourcePath : "Not found";
            HomeClientStatus.Text = updatedReport.IsLaunchReady
                ? $"Client: ready ({updatedReport.Version.GameVersion})"
                : $"Client: {updatedReport.State}";
            HomeClientVersionStatus.Text = $"Version: {updatedReport.Version.DisplayText}";
            UpdateLaunchButtonState(updatedReport.IsLaunchReady);
            HomeLocateClientButton.IsVisible = false;
            SaveCurrentProfile();
        }
        catch (OperationCanceledException)
        {
            AppendLog("Patch apply cancelled.");
            PatchApplyStatus.Text = "Patch apply cancelled.";
        }
        catch (Exception ex)
        {
            AppendLog($"Patch apply failed: {ex.Message}");
            PatchApplyStatus.Text = "Patch apply failed.";
        }
        finally
        {
            ApplyPatchesButton.IsEnabled = true;
            DownloadPatchesButton.IsEnabled = true;
            CancelPatchButton.IsEnabled = false;
            PatchApplyProgressBar.IsIndeterminate = false;
            patchCancellation.Dispose();
            patchCancellation = null;
            UpdateHomeState();
        }
    }

    private void CancelPatch_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        patchCancellation?.Cancel();
        PatchApplyStatus.Text = "Cancelling...";
        AppendLog("Patch cancel requested.");
    }

    private async void CreateAccount_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ShowCreateAccountDialogAsync();
    }

    private async Task ShowCreateAccountDialogAsync()
    {
        TextBox usernameBox = new() { PlaceholderText = "Username", Text = LoginUserBox.Text ?? "" };
        TextBox emailBox = new() { PlaceholderText = "Email" };
        TextBox passwordBox = new() { PlaceholderText = "Password", PasswordChar = '*' };
        TextBox confirmPasswordBox = new() { PlaceholderText = "Confirm password", PasswordChar = '*' };
        TextBlock statusText = new()
        {
            Text = "",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Avalonia.Media.Brushes.LightGray
        };
        Button createButton = new() { Content = "Create Account", HorizontalAlignment = HorizontalAlignment.Stretch };
        Button cancelButton = new() { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Stretch };

        Window dialog = new()
        {
            Title = "Create Account",
            Width = 420,
            Height = 360,
            MinWidth = 380,
            MinHeight = 340,
            Background = Background,
            Foreground = Foreground,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Grid
            {
                Margin = new Thickness(18),
                RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto"),
                RowSpacing = 10,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Create Account",
                        FontSize = 20,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold
                    },
                    usernameBox,
                    emailBox,
                    passwordBox,
                    confirmPasswordBox,
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            statusText,
                            createButton,
                            cancelButton
                        }
                    }
                }
            }
        };

        Grid.SetRow(usernameBox, 1);
        Grid.SetRow(emailBox, 2);
        Grid.SetRow(passwordBox, 3);
        Grid.SetRow(confirmPasswordBox, 4);
        Grid.SetRow((Control)((Grid)dialog.Content!).Children[5], 5);

        cancelButton.Click += (_, _) => dialog.Close();
        createButton.Click += async (_, _) =>
        {
            createButton.IsEnabled = false;
            statusText.Text = "Creating account...";
            try
            {
                LauncherAuthResponse response = await CreateAccountAsync(
                    usernameBox.Text ?? "",
                    emailBox.Text ?? "",
                    passwordBox.Text ?? "",
                    confirmPasswordBox.Text ?? "");

                if (!response.Success || string.IsNullOrWhiteSpace(response.SessionId))
                {
                    statusText.Text = string.IsNullOrWhiteSpace(response.Message)
                        ? "Account creation failed."
                        : response.Message;
                    return;
                }

                currentSessionId = response.SessionId;
                currentSessionUsername = response.Username ?? usernameBox.Text ?? "";
                LoginUserBox.Text = currentSessionUsername;
                LoginPasswordBox.Text = passwordBox.Text ?? "";
                HomeLoginStatus.Text = $"Signed in as {currentSessionUsername}.";
                SaveCurrentProfile();
                AppendLog($"Account created and signed in: {currentSessionUsername}");
                dialog.Close();
            }
            catch (Exception ex)
            {
                statusText.Text = ex.Message;
                AppendLog($"Account creation failed: {ex.Message}");
            }
            finally
            {
                createButton.IsEnabled = true;
            }
        };

        await dialog.ShowDialog(this);
    }

    private async Task<LauncherAuthResponse> CreateAccountAsync(
        string username,
        string email,
        string password,
        string confirmPassword)
    {
        LauncherApiClient client = new(httpClient, LauncherServiceUrlBox.Text ?? "");
        LauncherAuthResponse? response = await client.CreateAccountAsync(
            new LauncherCreateAccountRequest(username.Trim(), password, confirmPassword, email.Trim()),
            launcherConfig?.AccountCreateUrl);

        return response ?? new LauncherAuthResponse(false, "Account service unavailable.", null, null);
    }

    private async Task<string> EnsureSessionAsync()
    {
        string username = (LoginUserBox.Text ?? "").Trim();
        string password = LoginPasswordBox.Text ?? "";

        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Username is required.");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Password is required.");

        HomeLoginStatus.Text = string.IsNullOrWhiteSpace(currentSessionId)
            ? "Signing in..."
            : "Refreshing session...";
        LauncherApiClient client = new(httpClient, LauncherServiceUrlBox.Text ?? "");
        LauncherAuthResponse? response = await client.LoginAsync(
            new LauncherAuthRequest(username, password),
            launcherConfig?.LoginUrl);

        if (response is null)
            throw new InvalidOperationException("Login service unavailable.");
        if (!response.Success || string.IsNullOrWhiteSpace(response.SessionId))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(response.Message) ? "Login failed." : response.Message);

        currentSessionId = response.SessionId;
        currentSessionUsername = response.Username ?? username;
        HomeLoginStatus.Text = $"Signed in as {currentSessionUsername}.";
        SaveCurrentProfile();
        return currentSessionId;
    }

    private void ClearLog_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        LaunchLogBox.Text = "";
    }

    private void ScanRuntime_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ScanRuntimeCandidates(true);
    }

    private async void InstallRuntime_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (runtimeBusy)
            return;

        if (!platform.RequiresCompatibilityRuntime)
        {
            ManagedRuntimeStatus.Text = "Windows builds launch the client directly.";
            return;
        }

        selectedRuntimeArtifact ??= BuiltInRuntimeCatalog.Find(platform.RuntimeIdentifier);
        if (selectedRuntimeArtifact is null)
        {
            OpenRuntimeSetupGuidance();
            return;
        }

        SetRuntimeBusy(true);
        RuntimeProgressBar.IsIndeterminate = false;
        RuntimeProgressBar.Value = 0;

        try
        {
            Progress<RuntimeDownloadProgress> progress = new(update =>
            {
                ManagedRuntimeStatus.Text = update.Message;
                if (update.TotalBytes > 0)
                {
                    double percent = (double)update.BytesDownloaded / update.TotalBytes * 100;
                    RuntimeProgressBar.Value = Math.Clamp(percent, 0, 100);
                }

                if (update.LogMessage)
                    AppendLog(update.Message);
            });

            RuntimeDownloadResult installResult = await RuntimeDownloadService.DownloadAndInstallAsync(
                selectedRuntimeArtifact,
                httpClient,
                progress);
            managedRuntimeInstall = installResult.Install;
            foreach (string message in installResult.Messages)
                AppendLog(message);

            ManagedRuntimeStatus.Text = RuntimePrerequisiteStatusText("Runtime installed");
            AppendLog(ManagedRuntimeStatus.Text);
            RuntimeProgressBar.IsIndeterminate = true;
            RuntimeValidationResult validation = await RuntimeValidator.ValidateAsync(
                managedRuntimeInstall,
                RuntimeInstallStore.ManagedPrefixPath);
            RuntimeProgressBar.IsIndeterminate = false;
            RuntimeProgressBar.Value = validation.IsReady ? 100 : 0;
            ManagedRuntimeStatus.Text = validation.IsReady
                ? $"Ready: {validation.VersionText}. {validation.Message}"
                : $"Installed, but validation failed: {validation.Message}";
            AppendLog($"Managed runtime validation: {validation.Message}");
            AppendLog($"Managed runtime validation log: {validation.LogPath}");
            SaveCurrentProfile();
        }
        catch (Exception ex)
        {
            RuntimeProgressBar.IsIndeterminate = false;
            ManagedRuntimeStatus.Text = $"Runtime installation failed: {ex.Message}";
            AppendLog($"Runtime installation failed: {ex.Message}");
        }
        finally
        {
            SetRuntimeBusy(false);
            UpdateRuntimeUiState();
        }
    }

    private void OpenRuntimeSetupGuidance()
    {
        RuntimeSetupGuidance guidance = RuntimeSetupGuidance.ForCurrentPlatform();
        RuntimeCatalogStatus.Text = guidance.Summary;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = guidance.GuideUri.AbsoluteUri,
                UseShellExecute = true
            });
            ManagedRuntimeStatus.Text = "Finish Wine setup, then select Scan Runtimes and Validate Runtime.";
            AppendLog($"Opened platform Wine setup guidance: {guidance.GuideUri}");
        }
        catch (Exception ex)
        {
            ManagedRuntimeStatus.Text = "Could not open the Wine setup guide.";
            AppendLog($"Wine setup guide failed to open: {ex.Message}");
        }
    }

    private async void ValidateRuntime_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!platform.RequiresCompatibilityRuntime)
        {
            ManagedRuntimeStatus.Text = "Windows builds launch the client directly.";
            return;
        }

        SetRuntimeBusy(true);
        RuntimeProgressBar.IsIndeterminate = true;

        try
        {
            WineRuntimeProfile runtimeProfile = ReadRuntimeProfile();
            ManagedRuntimeStatus.Text = RuntimePrerequisiteStatusText("Validating runtime");
            AppendLog(ManagedRuntimeStatus.Text);
            RuntimeValidationResult result = await RuntimeValidator.ValidateAsync(
                runtimeProfile,
                RuntimeInstallStore.ManagedPrefixPath);

            ManagedRuntimeStatus.Text = result.IsReady
                ? $"Ready: {result.VersionText}. {result.Message}"
                : result.Message;
            AppendLog($"Runtime validation: {result.Message}");
            AppendLog($"Runtime validation log: {result.LogPath}");
        }
        catch (Exception ex)
        {
            ManagedRuntimeStatus.Text = "Runtime validation failed.";
            AppendLog($"Runtime validation failed: {ex.Message}");
        }
        finally
        {
            RuntimeProgressBar.IsIndeterminate = false;
            SetRuntimeBusy(false);
            UpdateRuntimeUiState();
        }
    }

    private async void VerifyDependencies_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (runtimeBusy)
            return;

        if (!platform.RequiresCompatibilityRuntime)
        {
            ManagedRuntimeStatus.Text = "Windows native runtime dependencies are ready.";
            return;
        }

        SetRuntimeBusy(true);
        RuntimeProgressBar.IsIndeterminate = true;
        try
        {
            WineRuntimeProfile runtimeProfile = ReadRuntimeProfile();
            RuntimePrerequisiteResult result = await RuntimePlatformPrerequisites.CheckAsync(runtimeProfile.Command);
            AppendLog($"Runtime dependency verification: {result.Message}");
            foreach (string warning in result.Warnings)
                AppendLog($"Runtime dependency warning: {warning}");

            if (result.IsReady)
            {
                ManagedRuntimeStatus.Text = result.Warnings.Count == 0
                    ? result.Message
                    : $"{result.Message} {string.Join(" ", result.Warnings)}";
                RuntimeProgressBar.Value = 100;
                return;
            }

            ManagedRuntimeStatus.Text = result.Message;
            RuntimeProgressBar.Value = 0;
            if (!OperatingSystem.IsLinux() || result.MissingLibraries.Count == 0)
                return;

            RuntimeDependencyInstallPlan plan = RuntimeDependencyInstaller.CreateCurrentLinuxPlan();
            if (!plan.IsSupported)
            {
                ManagedRuntimeStatus.Text = $"{result.Message} {plan.Description}";
                AppendLog(plan.Description);
                return;
            }

            bool install = await ConfirmDependencyInstallAsync(result, plan);
            if (!install)
            {
                AppendLog("Runtime dependency installation was declined.");
                return;
            }

            ManagedRuntimeStatus.Text = "Installing required runtime dependencies...";
            AppendLog(plan.Description);
            RuntimeDependencyInstallResult installResult = await RuntimeDependencyInstaller.InstallAsync(plan);
            AppendLog(installResult.Message);
            if (!installResult.Succeeded)
            {
                ManagedRuntimeStatus.Text = installResult.Message;
                return;
            }

            ManagedRuntimeStatus.Text = "Dependency installation completed; verifying again...";
            result = await RuntimePlatformPrerequisites.CheckAsync(runtimeProfile.Command);
            ManagedRuntimeStatus.Text = result.Message;
            RuntimeProgressBar.Value = result.IsReady ? 100 : 0;
            AppendLog($"Runtime dependency recheck: {result.Message}");
        }
        catch (Exception ex)
        {
            ManagedRuntimeStatus.Text = $"Dependency verification failed: {ex.Message}";
            AppendLog(ManagedRuntimeStatus.Text);
        }
        finally
        {
            RuntimeProgressBar.IsIndeterminate = false;
            SetRuntimeBusy(false);
            UpdateRuntimeUiState();
        }
    }

    private async Task<bool> ConfirmDependencyInstallAsync(
        RuntimePrerequisiteResult result,
        RuntimeDependencyInstallPlan plan)
    {
        Button installButton = new() { Content = "Install", HorizontalAlignment = HorizontalAlignment.Stretch };
        Button cancelButton = new() { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Stretch };
        Window dialog = new()
        {
            Title = "Install Runtime Dependencies",
            Width = 560,
            Height = 300,
            MinWidth = 500,
            MinHeight = 270,
            Background = Background,
            Foreground = Foreground,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Required runtime dependencies are missing.",
                        FontSize = 20,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = $"Missing: {string.Join(", ", result.MissingLibraries)}\n\n{plan.Description}. Your system will show an administrator authentication prompt. Verification resumes automatically after installation.",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Foreground = Avalonia.Media.Brushes.LightGray
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        Children = { installButton, cancelButton }
                    }
                }
            }
        };
        installButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);
        return await dialog.ShowDialog<bool>(this);
    }

    private void SaveRuntimeSettings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        SaveCurrentProfile();
        ManagedRuntimeStatus.Text = "Runtime settings saved. Select Validate Runtime to test this executable and prefix.";
        AppendLog($"Runtime settings saved: {ProfileStore.DefaultProfilePath}");
    }

    private async void BrowseRuntime_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!StorageProvider.CanOpen)
        {
            AppendLog("Runtime executable picker is not available on this platform.");
            return;
        }

        IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Wine runtime executable",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.All }
        });
        string? selectedPath = files.FirstOrDefault()?.TryGetLocalPath();
        if (String.IsNullOrWhiteSpace(selectedPath))
            return;

        RuntimeCommandBox.Text = selectedPath;
        SaveCurrentProfile();
        AppendLog($"Custom runtime executable selected: {selectedPath}");
    }

    private async void BrowseRuntimePrefix_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!StorageProvider.CanPickFolder)
        {
            AppendLog("Wine prefix folder picker is not available on this platform.");
            return;
        }

        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Wine prefix folder",
            AllowMultiple = false
        });
        string? selectedPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (String.IsNullOrWhiteSpace(selectedPath))
            return;

        RuntimeValueBox.Text = selectedPath;
        SaveCurrentProfile();
        AppendLog($"Custom Wine prefix selected: {selectedPath}");
    }

    private void ResetPrefix_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            RuntimeInstallStore.ResetManagedPrefix();
            ManagedRuntimeStatus.Text = "Runtime prefix reset. Validate the selected Wine runtime to recreate it.";
            AppendLog($"Runtime prefix reset: {RuntimeInstallStore.ManagedPrefixPath}");
        }
        catch (Exception ex)
        {
            ManagedRuntimeStatus.Text = "Runtime prefix reset failed.";
            AppendLog($"Runtime prefix reset failed: {ex.Message}");
        }
    }

    private void RuntimeMode_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isInitialized || isLoadingProfile)
            return;

        UpdateRuntimeUiState();
        SaveCurrentProfile();
    }

    private void ClientLaunchSettings_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!isInitialized || isLoadingProfile)
            return;

        UpdateRuntimeUiState();
        SaveCurrentProfile();
    }

    private void UmbraSettings_Changed(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!isInitialized || isLoadingProfile)
            return;

        SaveCurrentProfile();
    }

    private async void InstallUmbra_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RefreshUmbraFrameworkCatalogAsync();
        await InstallSelectedUmbraFrameworkAsync();
    }

    private async void LaunchGame_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (launchInProgress)
            return;

        SetLaunchInProgress("Preparing...", "Preparing launch...");
        try
        {
            SaveCurrentProfile();
            ClientInstall clientInstall = ClientInstall.FromPath(ClientPathBox.Text ?? "");
            ClientInstallReport report = clientInstall.Inspect();
            if (!report.IsLaunchReady)
            {
                AppendLog("Launch blocked: client is not ready.");
                HomeLoginStatus.Text = "Client is not ready to launch.";
                UpdateHomeState();
                return;
            }

            ServerProfile serverProfile = ReadServerProfile();
            WineRuntimeProfile runtimeProfile = ReadRuntimeProfile();
            ClientGraphicsTarget graphicsTarget = ReadGraphicsTarget();
            if (platform.RequiresCompatibilityRuntime)
                runtimeProfile = runtimeProfile.WithGraphicsTarget(graphicsTarget);

            Directory.CreateDirectory(Path.GetDirectoryName(RuntimeInstallStore.ServerProfilePath)!);
            ServerXmlWriter.Write(RuntimeInstallStore.ServerProfilePath, new[] { serverProfile });
            SetLaunchInProgress("Signing in...", "Signing in...");
            string sessionId = await EnsureSessionAsync();

            if (platform.RequiresCompatibilityRuntime)
            {
                SetLaunchInProgress("Checking runtime...", "Checking Wine runtime...");
                ManagedRuntimeStatus.Text = "Validating runtime before launch...";
                RuntimeValidationResult validation = await RuntimeValidator.ValidateAsync(
                    runtimeProfile,
                    RuntimeInstallStore.ManagedPrefixPath);
                AppendLog($"Runtime validation: {validation.Message}");
                AppendLog($"Runtime validation log: {validation.LogPath}");

                if (!validation.IsReady)
                {
                    ManagedRuntimeStatus.Text = validation.Message;
                    AppendLog("Launch blocked: runtime validation failed.");
                    HomeLoginStatus.Text = "Runtime validation failed.";
                    return;
                }
            }

            SetLaunchInProgress("Launching game...", "Launching game...");
            ClientLaunchHelperMode helperMode = ReadLaunchHelperMode();
            UmbraLaunchOptions umbraLaunchOptions = await ResolveUmbraLaunchOptionsForLaunchAsync(clientInstall);
            ClientLaunchHelperMode effectiveHelperMode = ClientLaunchHelperLocator.ResolveEffectiveMode(
                helperMode,
                platform.RequiresCompatibilityRuntime);
            if (effectiveHelperMode != helperMode)
            {
                AppendLog(platform.RequiresCompatibilityRuntime
                    ? "Wine uses the 64-bit managed helper for reliable .NET hosting; x86 game and Umbra work remains in the native x86 processes."
                    : "Native Windows uses the 32-bit helper to match the FFXIV 1.23b client.");
                helperMode = effectiveHelperMode;
            }

            if (platform.RequiresCompatibilityRuntime)
            {
                SetLaunchInProgress("Configuring Wine...", "Configuring Wine runtime...");
                WineRuntimeConfigurationResult configuration = await WineRuntimeConfigurator.ConfigureAsync(
                    runtimeProfile,
                    RuntimeInstallStore.ManagedPrefixPath,
                    new WineRuntimeConfigurationSettings(platform.OperatingSystem));
                AppendLog($"Runtime config: {configuration.Message}");
                AppendLog($"Runtime config target: {configuration.RuntimeTarget}");
                AppendLog($"Runtime config log: {configuration.LogPath}");

                if (!configuration.IsReady)
                {
                    ManagedRuntimeStatus.Text = configuration.Message;
                    AppendLog("Launch blocked: runtime setup failed.");
                    HomeLoginStatus.Text = "Runtime setup failed.";
                    return;
                }
            }

            SetLaunchInProgress("Launching game...", "Launching game...");
            string helperPath = ClientLaunchHelperLocator.FindLaunchHelperRequired(helperMode);
            LaunchPlan plan = LaunchPlan.CreateWithHelper(
                clientInstall,
                serverProfile,
                runtimeProfile,
                helperPath,
                sessionId,
                platform.RequiresCompatibilityRuntime,
                umbraOptions: umbraLaunchOptions);
            ProcessStartInfo startInfo = BuildProcessStartInfo(plan);
            RuntimeLaunchResult result = RuntimeLaunchDiagnostics.StartWithLogging(startInfo, plan.LogPath);
            AppendLog($"Launch helper: {FormatLaunchHelperMode(helperMode)} ({helperPath})");
            AppendLog($"Graphics target: {FormatGraphicsTarget(graphicsTarget)}");
            AppendLog($"Launch started: pid {result.ProcessId}");
            AppendLog($"Launch log: {result.LogPath}");
            if (!string.IsNullOrWhiteSpace(plan.HelperLogPath))
                AppendLog($"Launch helper log: {plan.HelperLogPath}");
            HomeLoginStatus.Text = platform.RequiresCompatibilityRuntime
                ? "Launch sent to Wine. Watch for the game window."
                : "Launch sent to Windows. Watch for the game window.";
            HomeProgressBar.Value = 100;
            _ = MonitorLaunchHelperAsync(plan.HelperLogPath);
        }
        catch (Exception ex)
        {
            AppendLog($"Launch failed: {ex.Message}");
            HomeLoginStatus.Text = FormatLaunchFailure(ex.Message);
        }
        finally
        {
            ClearLaunchInProgress();
        }
    }

    private async Task MonitorLaunchHelperAsync(string? helperLogPath)
    {
        if (string.IsNullOrWhiteSpace(helperLogPath))
            return;

        try
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                await Task.Delay(500);
                if (!File.Exists(helperLogPath))
                    continue;

                Dictionary<string, string> values = ReadKeyValueLog(helperLogPath);
                if (values.TryGetValue("launch_error", out string? launchError))
                {
                    string exitDetail = values.TryGetValue("exit_code", out string? exitCode)
                        ? $" ({exitCode})"
                        : "";
                    string detail = values.TryGetValue("launch_error_detail", out string? launchErrorDetail)
                        ? $" [{launchErrorDetail}]"
                        : "";
                    AppendLog($"Launch helper reported: {launchError}{exitDetail}{detail}");
                    HomeLoginStatus.Text = launchError == "game_exited_during_observation"
                        ? $"Game exited during startup{exitDetail}{detail}."
                        : $"Launch helper reported {launchError}{exitDetail}{detail}.";
                    HomeProgressBar.Value = 0;
                    return;
                }

                if (values.TryGetValue("helper_error_message", out string? helperErrorMessage)
                    || values.TryGetValue("helper_error", out helperErrorMessage))
                {
                    string helperErrorType = values.TryGetValue("helper_error_type", out string? errorType)
                        ? $"{errorType}: "
                        : "";
                    AppendLog($"Launch helper error: {helperErrorType}{helperErrorMessage}");
                    HomeLoginStatus.Text = $"Launch helper error: {helperErrorMessage}";
                    HomeProgressBar.Value = 0;
                    return;
                }

                if (values.TryGetValue("exited_during_observation", out string? observedExit)
                    && bool.TryParse(observedExit, out bool exitedDuringObservation)
                    && !exitedDuringObservation)
                {
                    HomeLoginStatus.Text = "Game is running.";
                    HomeProgressBar.Value = 100;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Launch helper monitor failed: {ex.Message}");
        }
    }

    private async Task<UmbraLaunchOptions> ResolveUmbraLaunchOptionsForLaunchAsync(ClientInstall clientInstall)
    {
        UmbraSettings settings = ReadUmbraSettings();
        if (!settings.Enabled)
            return UmbraLaunchOptions.Disabled;

        if (!File.Exists(clientInstall.DirectGameExecutablePath))
        {
            AppendLog("Umbra disabled for this launch: ffxivgame.exe was not found.");
            return UmbraLaunchOptions.Disabled;
        }

        string gameSha256 = UmbraCompatibility.ComputeSha256(clientInstall.DirectGameExecutablePath);
        if (!UmbraCompatibility.IsKnownGameHash(gameSha256))
        {
            AppendLog($"Umbra disabled for this launch: unsupported ffxivgame.exe SHA256 {gameSha256}.");
            return UmbraLaunchOptions.Disabled;
        }

        Directory.CreateDirectory(settings.PluginDirectory);
        Directory.CreateDirectory(UmbraInstallStore.LogsRoot);

        UmbraFrameworkInstall? install = await EnsureUmbraFrameworkForLaunchAsync(gameSha256);
        if (install is null)
        {
            AppendLog("Umbra disabled for this launch: no verified framework is installed.");
            return UmbraLaunchOptions.Disabled;
        }

        string logPath = UmbraInstallStore.CreateLogPath("umbra-launch");
        AppendLog($"Umbra enabled for launch: {install.Name} {install.Version}");
        AppendLog($"Umbra log: {logPath}");

        IReadOnlyList<UmbraRepositorySource> repositorySources = UmbraRepositoryOptions.BuildEffectiveRepositorySources(
            settings,
            launcherConfig?.PluginCatalogUrls ?? Array.Empty<string>(),
            LauncherServiceUrlBox.Text);

        return new UmbraLaunchOptions(
            true,
            settings.SafeMode,
            settings.LoadDelayMilliseconds,
            install.BootstrapPath,
            install.FrameworkPath,
            settings.PluginDirectory,
            logPath,
            repositorySources.Select(source => source.Url).ToArray(),
            EnableManagedOnWine: platform.RequiresCompatibilityRuntime)
        {
            RepositorySources = repositorySources
        };
    }

    private async Task<UmbraFrameworkInstall?> EnsureUmbraFrameworkForLaunchAsync(string gameSha256)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
        await RefreshUmbraFrameworkCatalogAsync(timeout.Token);

        if (selectedUmbraFrameworkArtifact is not null
            && selectedUmbraFrameworkArtifact.SupportsGameHash(gameSha256))
        {
            UmbraFrameworkInstall? selectedInstall = UmbraInstallStore.FindInstalled(selectedUmbraFrameworkArtifact);
            if (selectedInstall is not null)
            {
                umbraFrameworkInstall = selectedInstall;
                RefreshInstalledUmbraFrameworkStatus();
                return selectedInstall;
            }

            UmbraFrameworkInstall? installed = await InstallSelectedUmbraFrameworkAsync();
            if (installed is not null && installed.SupportsGameHash(gameSha256))
                return installed;
        }

        UmbraFrameworkInstall? latest = UmbraInstallStore.FindLatestInstalled();
        if (latest is not null && latest.SupportsGameHash(gameSha256))
        {
            umbraFrameworkInstall = latest;
            RefreshInstalledUmbraFrameworkStatus();
            AppendLog($"Umbra using installed framework fallback: {latest.Name} {latest.Version}");
            return latest;
        }

        UmbraFrameworkInstall? bundled = UmbraInstallStore.FindBundled();
        if (bundled is not null && bundled.SupportsGameHash(gameSha256))
        {
            umbraFrameworkInstall = bundled;
            RefreshInstalledUmbraFrameworkStatus();
            AppendLog($"Umbra using bundled framework fallback: {bundled.Name} {bundled.Version}");
            return bundled;
        }

        return null;
    }

    private static Dictionary<string, string> ReadKeyValueLog(string path)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in File.ReadLines(path))
        {
            int equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                continue;

            values[line[..equalsIndex]] = line[(equalsIndex + 1)..];
        }

        return values;
    }

    private ServerProfile ReadServerProfile()
    {
        ServerProfile profile = new(
            ServerNameBox.Text ?? "",
            ServerHostBox.Text ?? "",
            Convert.ToInt32(LobbyPortBox.Value ?? 54994),
            Convert.ToInt32(WorldPortBox.Value ?? 54992),
            1989,
            ResolveLauncherEndpoint(launcherConfig?.ClientLoginUrl, "../login/index.php"));

        profile.Validate();
        return profile;
    }

    private void SetLaunchInProgress(string buttonText, string statusText)
    {
        launchInProgress = true;
        PlayGameButton.Content = buttonText;
        PlayGameButton.IsEnabled = false;
        HomeProgressBar.Value = 0;
        HomeProgressBar.IsIndeterminate = true;
        HomeLoginStatus.Text = statusText;
    }

    private void ClearLaunchInProgress()
    {
        launchInProgress = false;
        HomeProgressBar.IsIndeterminate = false;
        UpdateHomeState();
    }

    private void SetFfxivSettingsProgress(string statusText, double value)
    {
        FfxivSettingsStatus.Text = statusText;
        FfxivSettingsProgressBar.Value = Math.Clamp(value, 0, 100);
    }

    private void UpdateLaunchButtonState(bool? clientReady = null)
    {
        if (launchInProgress)
        {
            PlayGameButton.IsEnabled = false;
            return;
        }

        PlayGameButton.Content = "Log In & Play";
        PlayGameButton.IsEnabled = clientReady ?? IsSelectedClientLaunchReady();
    }

    private bool IsSelectedClientLaunchReady()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ClientPathBox.Text))
                return false;

            return ClientInstall.FromPath(ClientPathBox.Text).Inspect().IsLaunchReady;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatLaunchFailure(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Launch failed.";

        if (message.Contains("username", StringComparison.OrdinalIgnoreCase)
            || message.Contains("password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Login", StringComparison.OrdinalIgnoreCase))
        {
            return $"Login failed: {message}";
        }

        return $"Launch failed: {message}";
    }

    private WineRuntimeProfile ReadRuntimeProfile()
    {
        if (platform.UsesNativeWindowsClient)
            return WineRuntimeProfile.NativeWindows();

        RuntimeSelectionMode mode = ReadRuntimeMode();
        if (mode == RuntimeSelectionMode.DetectedRuntime)
        {
            int index = DetectedRuntimeBox.SelectedIndex;
            if (index >= 0 && index < runtimeCandidates.Count)
                return ApplyGraphicsTarget(NormalizeRuntimeProfile(RuntimeProfileResolver.CandidateToProfile(runtimeCandidates[index], RuntimeInstallStore.ManagedPrefixPath)));
        }

        WineRuntimeProfile profile = RuntimeProfileResolver.Resolve(
            mode,
            managedRuntimeInstall,
            runtimeCandidates,
            ReadCustomRuntimeProfile(),
            RuntimeInstallStore.ManagedPrefixPath);
        return ApplyGraphicsTarget(NormalizeRuntimeProfile(profile));
    }

    private static WineRuntimeProfile NormalizeRuntimeProfile(WineRuntimeProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Command)
            && !Path.IsPathFullyQualified(profile.Command))
        {
            string? resolvedCommand = RuntimeDiscovery.ResolveExecutable(
                profile.Command,
                File.Exists,
                RuntimeDiscovery.BuildExecutableSearchPath());
            if (!string.IsNullOrWhiteSpace(resolvedCommand))
                profile = profile with { Command = resolvedCommand };
        }

        if (profile.Kind == WineRuntimeKind.WhiskyBottle
            && !string.IsNullOrWhiteSpace(profile.BottleName)
            && WhiskyRuntimeEnvironment.TryCreateWineProfile(
                profile.Command,
                profile.BottleName,
                out WineRuntimeProfile whiskyWineProfile,
                out _))
        {
            return whiskyWineProfile;
        }

        return profile;
    }

    private WineRuntimeProfile ApplyGraphicsTarget(WineRuntimeProfile profile)
    {
        if (!platform.RequiresCompatibilityRuntime || profile.Kind == WineRuntimeKind.NativeWindows)
            return profile;

        return profile.WithGraphicsTarget(ReadGraphicsTarget());
    }

    private WineRuntimeProfile ReadCustomRuntimeProfile()
    {
        string name = RuntimeNameBox.Text ?? "Local Wine Runtime";
        string command = RuntimeCommandBox.Text ?? "wine";
        string value = RuntimeValueBox.Text ?? "";

        return CustomRuntimeKindBox.SelectedIndex switch
        {
            0 => WineRuntimeProfile.WinePrefix(
                name,
                string.IsNullOrWhiteSpace(value) ? RuntimeInstallStore.ManagedPrefixPath : value,
                command),
            _ => WineRuntimeProfile.Custom(name, command)
        };
    }

    private RuntimeSelectionMode ReadRuntimeMode()
    {
        if (platform.UsesNativeWindowsClient)
            return RuntimeSelectionMode.CustomRuntime;

        return RuntimeModeBox.SelectedIndex switch
        {
            1 => RuntimeSelectionMode.CustomRuntime,
            _ => RuntimeSelectionMode.AutomaticManaged
        };
    }

    private void ValidatePatchLibrary()
    {
        string patchLibraryPath = PatchLibraryPathBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(patchLibraryPath))
        {
            PatchLibraryStatus.Text = "Not selected";
            AppendLog("Patch library path is not selected.");
            return;
        }

        PatchLibraryReport report = LegacyPatchManifest.InspectLibrary(patchLibraryPath);
        PatchLibraryStatus.Text = report.Summary;
        AppendLog($"Patch library: {report.Summary}");
        AppendLog($"Patch base: {report.PatchBasePath}");
        AppendLog($"Metainfo base: {report.MetainfoBasePath}");

        LogMissingEntries("Missing patch", report.MissingPatchFiles);
        LogOptionalMetainfo(report);
        LogInvalidEntries(report.InvalidPatchFiles);
    }

    private async Task RefreshLauncherServicesAsync()
    {
        string serviceUrl = LauncherServiceUrlBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            HeaderServiceStatus.Text = "Launcher services not configured";
            HomeServerStatus.Text = "Server: services not configured";
            return;
        }

        try
        {
            HeaderServiceStatus.Text = "Checking launcher services...";
            HomeServerStatus.Text = "Server: checking";
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            LauncherApiClient client = new(httpClient, serviceUrl);
            launcherConfig = await client.GetConfigAsync(timeout.Token);
            if (launcherConfig is not null)
            {
                HeaderServiceStatus.Text = $"{launcherConfig.ServerName}: service online";
                if (!string.IsNullOrWhiteSpace(launcherConfig.PatchBaseUrl))
                    PatchBaseUrlBox.Text = launcherConfig.PatchBaseUrl;
            }

            LauncherStatus? status = await client.GetStatusAsync(timeout.Token);
            if (status is not null)
            {
                HomeServerStatus.Text = $"Server: {status.State} - {status.Message}";
            }
            else
            {
                HomeServerStatus.Text = launcherConfig is null
                    ? "Server: service unavailable"
                    : "Server: status unavailable";
            }

            LauncherNewsFeed? news = await client.GetNewsAsync(timeout.Token);
            await ApplyNewsFeedAsync(news, timeout.Token);
            SaveCurrentProfile();
        }
        catch (Exception ex)
        {
            HeaderServiceStatus.Text = "Launcher services unavailable";
            HomeServerStatus.Text = "Server: service unavailable";
            AppendLog($"Launcher service refresh failed: {ex.Message}");
            await ApplyNewsFeedAsync(null);
        }

        RefreshRuntimeSetupStatus();
    }

    private async Task RefreshUmbraFrameworkCatalogAsync(CancellationToken cancellationToken = default)
    {
        string serviceUrl = LauncherServiceUrlBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            RefreshInstalledUmbraFrameworkStatus();
            return;
        }

        try
        {
            AppendLog("Umbra framework package check started.");
            LauncherApiClient client = new(httpClient, serviceUrl);
            umbraFrameworkCatalog = await client.GetUmbraFrameworkCatalogAsync(
                "win-x86",
                launcherConfig?.ClientPluginFrameworkCatalogUrl,
                cancellationToken);
            selectedUmbraFrameworkArtifact = umbraFrameworkCatalog?.SelectDefault();

            if (selectedUmbraFrameworkArtifact is null)
            {
                AppendLog("Umbra framework package check: no package is available.");
                RefreshInstalledUmbraFrameworkStatus();
            }
            else
            {
                AppendLog($"Umbra framework package available: {FormatUmbraFrameworkArtifact(selectedUmbraFrameworkArtifact)}");
                RefreshInstalledUmbraFrameworkStatus();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AppendLog($"Umbra framework package check failed: {ex.Message}");
            RefreshInstalledUmbraFrameworkStatus();
        }
        finally
        {
            UpdateUmbraUiState();
        }
    }

    private async Task<UmbraFrameworkInstall?> InstallSelectedUmbraFrameworkAsync()
    {
        if (umbraCancellation is not null)
            return null;

        if (selectedUmbraFrameworkArtifact is null)
            await RefreshUmbraFrameworkCatalogAsync();

        if (selectedUmbraFrameworkArtifact is null)
        {
            AppendLog("Umbra framework install blocked: catalog did not provide a package.");
            return null;
        }

        umbraCancellation = new CancellationTokenSource();
        SetUmbraBusy(true);

        try
        {
            Progress<UmbraFrameworkDownloadProgress> progress = new(update =>
            {
                if (update.LogMessage)
                    AppendLog(update.Message);
            });

            UmbraFrameworkDownloadResult result = await UmbraFrameworkDownloadService.DownloadAndInstallAsync(
                selectedUmbraFrameworkArtifact,
                httpClient,
                progress,
                umbraCancellation.Token);

            umbraFrameworkInstall = result.Install;
            foreach (string message in result.Messages)
                AppendLog(message);
            return result.Install;
        }
        catch (OperationCanceledException)
        {
            AppendLog("Umbra framework install cancelled.");
            return null;
        }
        catch (Exception ex)
        {
            AppendLog($"Umbra framework install failed: {ex.Message}");
            return null;
        }
        finally
        {
            umbraCancellation.Dispose();
            umbraCancellation = null;
            SetUmbraBusy(false);
            UpdateUmbraUiState();
        }
    }

    private async Task ShowRuntimeSetupPromptIfNeededAsync()
    {
        if (runtimeSetupPromptShown
            || !platform.RequiresCompatibilityRuntime
            || ReadRuntimeMode() != RuntimeSelectionMode.AutomaticManaged
            || managedRuntimeInstall is not null
            || runtimeCandidates.Count > 0)
        {
            return;
        }

        runtimeSetupPromptShown = true;

        Button installButton = new()
        {
            Content = "Install Runtime",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Button customButton = new()
        {
            Content = "Custom Runtime",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        Window dialog = new()
        {
            Title = "Runtime Required",
            Width = 460,
            Height = 250,
            MinWidth = 420,
            MinHeight = 230,
            Background = Background,
            Foreground = Foreground,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new Grid
            {
                Margin = new Thickness(18),
                RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"),
                RowSpacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "No Wine runtime was found.",
                        FontSize = 20,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold
                    },
                    new TextBlock
                    {
                        Text = "AetherXIV Launcher can download, verify, install, and validate its tested Wine runtime for this computer. You can also point it at a custom runtime.",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Foreground = Avalonia.Media.Brushes.LightGray
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        Children =
                        {
                            installButton,
                            customButton
                        }
                    }
                }
            }
        };

        Grid contentGrid = (Grid)dialog.Content!;
        Grid.SetRow(contentGrid.Children[1], 1);
        Grid.SetRow(contentGrid.Children[2], 3);

        installButton.Click += (_, _) => dialog.Close("install");
        customButton.Click += (_, _) => dialog.Close("custom");

        string? choice = await dialog.ShowDialog<string?>(this);
        if (choice == "install")
        {
            MainTabs.SelectedItem = RuntimeTab;
            InstallRuntime_Click(this, new Avalonia.Interactivity.RoutedEventArgs());
        }
        else if (choice == "custom")
        {
            RuntimeModeBox.SelectedIndex = 1;
            MainTabs.SelectedItem = RuntimeTab;
            UpdateRuntimeUiState();
            SaveCurrentProfile();
        }
    }

    private Task ApplyNewsFeedAsync(
        LauncherNewsFeed? feed,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        NewsItemsPanel.Children.Clear();

        homeReelTextEnabled = feed?.ReelTextEnabled == true;
        homeReelText = feed?.ReelText?
            .GroupBy(item => item.ImageFile, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, LauncherReelText>(StringComparer.OrdinalIgnoreCase);
        UpdateHomeReelImage();

        IReadOnlyList<LauncherNewsItem> items = feed?.Items?
            .OrderByDescending(item => item.PublishedAt)
            .ToArray()
            ?? Array.Empty<LauncherNewsItem>();

        if (items.Count == 0)
        {
            NewsItemsPanel.Children.Add(CreateNewsPlaceholder());
            return Task.CompletedTask;
        }

        foreach (LauncherNewsItem item in items)
            NewsItemsPanel.Children.Add(CreateNewsItemView(item));

        return Task.CompletedTask;
    }

    private static Control CreateNewsPlaceholder()
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#D020262E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3B4652")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(14),
            Child = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Awaiting launcher news service",
                        FontSize = 17,
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "News from Demi Dev Unit will appear here when the launcher service is available.",
                        Foreground = new SolidColorBrush(Color.Parse("#AEB7C2")),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
    }

    private static Control CreateNewsItemView(LauncherNewsItem item)
    {
        StackPanel content = new()
        {
            Spacing = 5
        };

        Grid header = new()
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 14
        };

        header.Children.Add(new TextBlock
        {
            Text = item.Title,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = ParseBrush(item.TitleColor, "#F2F4FA"),
            TextWrapping = TextWrapping.Wrap
        });

        TextBlock date = new()
        {
            Text = item.PublishedAt.ToLocalTime().ToString("MMM d, yyyy"),
            Foreground = new SolidColorBrush(Color.Parse("#AEB7C2")),
            FontStyle = FontStyle.Italic,
            VerticalAlignment = VerticalAlignment.Top
        };
        Grid.SetColumn(date, 1);
        header.Children.Add(date);
        content.Children.Add(header);

        if (!string.IsNullOrWhiteSpace(item.Summary))
        {
            content.Children.Add(new TextBlock
            {
                Text = item.Summary,
                Foreground = ParseBrush(item.SummaryColor, "#D6DCE3"),
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (!string.IsNullOrWhiteSpace(item.Body))
        {
            content.Children.Add(new TextBlock
            {
                Text = item.Body,
                Foreground = ParseBrush(item.BodyColor, "#AEB7C2"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            });
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#D020262E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#3B4652")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(14),
            Child = content
        };
    }

    private static IBrush ParseBrush(string? value, string fallback)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(String.IsNullOrWhiteSpace(value) ? fallback : value));
        }
        catch (FormatException)
        {
            return new SolidColorBrush(Color.Parse(fallback));
        }
    }

    private async Task<LauncherPatchManifest> ResolvePatchManifestAsync(CancellationToken cancellationToken)
    {
        string serviceUrl = LauncherServiceUrlBox.Text ?? "";
        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            LauncherApiClient client = new(httpClient, serviceUrl);
            LauncherPatchManifest? manifest = await client.GetPatchManifestAsync(cancellationToken);
            if (manifest is not null)
                return manifest;
        }

        string patchBaseUrl = PatchBaseUrlBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(patchBaseUrl))
            throw new InvalidOperationException("Patch base URL is not configured.");

        return LauncherPatchManifest.FromKnownPatchChain(patchBaseUrl);
    }

    private string ResolvePatchLibraryRoot()
    {
        string selected = PatchLibraryPathBox.Text ?? "";
        if (!string.IsNullOrWhiteSpace(selected))
            return selected;

        string? profileDir = Path.GetDirectoryName(ProfileStore.DefaultProfilePath);
        return Path.Combine(profileDir ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PatchLibrary");
    }

    private void ConfigurePlatformUi()
    {
        RuntimePlatformStatus.Text = $"{platform.OperatingSystem} ({platform.RuntimeIdentifier})";
        RuntimeTab.IsVisible = platform.RequiresCompatibilityRuntime;
        if (platform.UsesNativeWindowsClient)
            RuntimePlatformStatus.Text = "Windows native client launch";
        UpdateRuntimeUiState();
    }

    private void LoadSavedProfile()
    {
        isLoadingProfile = true;
        try
        {
            LauncherProfile profile = ProfileStore.LoadDefaultOrCreate();
            ClientPathBox.Text = profile.ClientRootPath ?? "";
            PatchLibraryPathBox.Text = profile.PatchLibraryRootPath ?? "";
            LauncherServiceUrlBox.Text = string.IsNullOrWhiteSpace(profile.LauncherServiceUrl)
                ? LauncherProfile.LocalDefault().LauncherServiceUrl
                : profile.LauncherServiceUrl;
            PatchBaseUrlBox.Text = profile.PatchBaseUrl ?? "";
            ServerNameBox.Text = profile.ServerProfile.Name;
            ServerHostBox.Text = profile.ServerProfile.Host;
            LobbyPortBox.Value = profile.ServerProfile.LobbyPort;
            WorldPortBox.Value = profile.ServerProfile.WorldPort;
            RememberUsernameBox.IsChecked = profile.RememberUsername;
            LoginUserBox.Text = profile.RememberUsername ? profile.SavedUsername : "";
            SelectServerPresetForProfile(profile);
            ApplyLaunchHelperMode(profile.LaunchHelperMode);
            ApplyGraphicsTarget(profile.GraphicsTarget);
            ApplyRuntimeMode(profile.RuntimeMode);
            ApplyRuntimeProfile(profile.RuntimeProfile);
            ApplyUmbraSettings(profile.EffectiveUmbra);
        }
        catch (Exception ex)
        {
            AppendLog($"Profile load failed: {ex.Message}");
            ApplyLaunchHelperMode(LauncherProfile.LocalDefault().LaunchHelperMode);
            ApplyGraphicsTarget(LauncherProfile.LocalDefault().GraphicsTarget);
            ApplyRuntimeMode(LauncherProfile.LocalDefault().RuntimeMode);
            ApplyRuntimeProfile(LauncherProfile.LocalDefault().RuntimeProfile);
            ApplyUmbraSettings(LauncherProfile.LocalDefault().EffectiveUmbra);
            SelectServerPresetForProfile(LauncherProfile.LocalDefault());
        }
        finally
        {
            isLoadingProfile = false;
            UpdateServerPresetUiState();
        }
    }

    private void ApplyRuntimeMode(RuntimeSelectionMode runtimeMode)
    {
        RuntimeModeBox.SelectedIndex = runtimeMode switch
        {
            RuntimeSelectionMode.CustomRuntime => 1,
            _ => 0
        };
        UpdateRuntimeUiState();
    }

    private void ApplyRuntimeProfile(WineRuntimeProfile runtimeProfile)
    {
        RuntimeNameBox.Text = runtimeProfile.Name;
        RuntimeCommandBox.Text = string.IsNullOrWhiteSpace(runtimeProfile.Command) ? "wine" : runtimeProfile.Command;
        RuntimeValueBox.Text = runtimeProfile.BottleName ?? runtimeProfile.PrefixPath ?? "";
        CustomRuntimeKindBox.SelectedIndex = runtimeProfile.Kind switch
        {
            WineRuntimeKind.WinePrefix => 0,
            WineRuntimeKind.CustomCommand => 1,
            _ => 1
        };
    }

    private void ApplyLaunchHelperMode(ClientLaunchHelperMode helperMode)
    {
        LaunchHelperModeBox.SelectedIndex = helperMode switch
        {
            ClientLaunchHelperMode.X86 => 1,
            ClientLaunchHelperMode.X64 => 2,
            _ => 0
        };
    }

    private void ApplyGraphicsTarget(ClientGraphicsTarget graphicsTarget)
    {
        GraphicsTargetBox.SelectedIndex = graphicsTarget switch
        {
            ClientGraphicsTarget.WineDefault => 1,
            ClientGraphicsTarget.OpenGLThreaded => 2,
            ClientGraphicsTarget.WineD3DVulkan => 3,
            _ => 0
        };
    }

    private void ApplyUmbraSettings(UmbraSettings settings)
    {
        UmbraSettings normalized = settings.Normalize();
        UmbraEnabledBox.IsChecked = normalized.Enabled;
    }

    private void SaveCurrentProfile()
    {
        try
        {
            LauncherProfile profile = new(
                ClientPathBox.Text ?? "",
                PatchLibraryPathBox.Text ?? "",
                LauncherServiceUrlBox.Text ?? "",
                PatchBaseUrlBox.Text ?? "",
                ReadServerProfile(),
                ReadRuntimeProfile(),
                ReadRuntimeMode(),
                ReadLaunchHelperMode(),
                ReadGraphicsTarget(),
                RememberUsernameBox.IsChecked == true ? (LoginUserBox.Text ?? "").Trim() : "",
                RememberUsernameBox.IsChecked == true,
                ReadUmbraSettings());
            ProfileStore.SaveDefault(profile);
        }
        catch (Exception ex)
        {
            AppendLog($"Profile save failed: {ex.Message}");
        }
    }

    private UmbraSettings ReadUmbraSettings()
    {
        return new UmbraSettings
        {
            Enabled = UmbraEnabledBox.IsChecked == true,
            PluginDirectory = UmbraInstallStore.PluginsRoot,
            SafeMode = false,
            LoadDelayMilliseconds = UmbraSettings.DefaultLoadDelayMilliseconds,
            UseOfficialRepository = true,
            CustomRepositoryUrls = Array.Empty<string>()
        }.Normalize();
    }

    private void ScanRuntimeCandidates(bool appendLog)
    {
        if (!platform.RequiresCompatibilityRuntime)
        {
            RuntimeCandidatesBox.Text = "Windows builds launch the client directly.";
            return;
        }

        runtimeCandidates = RuntimeDiscovery.Discover();
        RuntimeCandidatesBox.Text = runtimeCandidates.Count == 0
            ? "No approved detected runtime found."
            : string.Join(Environment.NewLine, runtimeCandidates.Select(FormatRuntimeCandidate));
        DetectedRuntimeBox.ItemsSource = runtimeCandidates.Select(FormatRuntimeCandidate).ToArray();
        if (runtimeCandidates.Count > 0 && DetectedRuntimeBox.SelectedIndex < 0)
            DetectedRuntimeBox.SelectedIndex = 0;

        if (appendLog)
            AppendLog($"Wine runtime scan found {runtimeCandidates.Count} candidate(s).");

        RefreshRuntimeSetupStatus();
    }

    private void UpdateHomeState()
    {
        if (string.IsNullOrWhiteSpace(ClientPathBox.Text))
        {
            HomeClientStatus.Text = "Client: not selected";
            HomeClientVersionStatus.Text = "Version: not checked";
            UpdateLaunchButtonState(false);
            HomeLocateClientButton.IsVisible = true;
        }
        else
        {
            HomeLocateClientButton.IsVisible = false;
            UpdateLaunchButtonState();
        }

    }

    private string ResolveLauncherEndpoint(string? configuredPath, string fallbackPath)
    {
        string endpoint = string.IsNullOrWhiteSpace(configuredPath)
            ? fallbackPath
            : configuredPath;

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? absoluteUri))
            return absoluteUri.ToString();

        string serviceUrl = LauncherServiceUrlBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(serviceUrl))
            return endpoint;

        Uri baseUri = new(serviceUrl.EndsWith('/') ? serviceUrl : $"{serviceUrl}/", UriKind.Absolute);
        return new Uri(baseUri, endpoint).ToString();
    }

    private ProcessStartInfo BuildProcessStartInfo(LaunchPlan plan)
    {
        ProcessStartInfo info;
        if (platform.UsesNativeWindowsClient || plan.RuntimeProfile.Kind == WineRuntimeKind.NativeWindows)
        {
            info = new ProcessStartInfo
            {
                FileName = plan.WindowsExecutablePath,
                Arguments = plan.Arguments,
                WorkingDirectory = plan.ClientInstall.RootPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            info = new ProcessStartInfo
            {
                FileName = plan.RuntimeProfile.Command,
                Arguments = plan.Arguments,
                WorkingDirectory = plan.ClientInstall.RootPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        foreach (KeyValuePair<string, string> pair in plan.Environment)
            info.Environment[pair.Key] = pair.Value;
        info.Environment["AETHERXIV_SERVER_XML"] = RuntimeInstallStore.ServerProfilePath;
        info.Environment["AETHERXIV_LAUNCH_LOG"] = plan.LogPath;
        if (plan.Umbra.Enabled)
        {
            info.Environment["AETHER_UMBRA_ENABLED"] = "1";
            info.Environment["AETHER_UMBRA_BOOTSTRAP"] = plan.Umbra.BootstrapPath;
            info.Environment["AETHER_UMBRA_FRAMEWORK"] = plan.Umbra.FrameworkPath;
            info.Environment["AETHER_UMBRA_PLUGIN_DIR"] = plan.Umbra.PluginDirectory;
            info.Environment["AETHER_UMBRA_LOG"] = plan.Umbra.LogPath;
            info.Environment["AETHER_UMBRA_SAFE_MODE"] = plan.Umbra.SafeMode ? "1" : "0";
            info.Environment["AETHER_UMBRA_LOAD_DELAY_MS"] = plan.Umbra.LoadDelayMilliseconds.ToString();
            info.Environment["AETHER_UMBRA_REPOSITORY_URLS"] = string.Join(";", plan.Umbra.RepositoryUrls);
            info.Environment["AETHER_UMBRA_REPOSITORIES_JSON"] = plan.Umbra.RepositoriesJson;
            info.Environment["AETHER_UMBRA_ENABLE_MANAGED_ON_WINE"] = plan.Umbra.EnableManagedOnWine ? "1" : "0";
        }

        return info;
    }

    private void RefreshRuntimeSetupStatus()
    {
        if (!platform.RequiresCompatibilityRuntime)
        {
            RuntimeCatalogStatus.Text = "Windows launches the client directly; Wine is not required.";
            ManagedRuntimeStatus.Text = "Windows native runtime.";
            return;
        }

        selectedRuntimeArtifact ??= BuiltInRuntimeCatalog.Find(platform.RuntimeIdentifier);
        if (selectedRuntimeArtifact is null)
        {
            RuntimeSetupGuidance guidance = RuntimeSetupGuidance.ForCurrentPlatform();
            RuntimeCatalogStatus.Text = guidance.Summary;
            ManagedRuntimeStatus.Text = runtimeCandidates.Count == 0
                ? "No managed package or local Wine runtime is available for this platform."
                : $"Detected {runtimeCandidates.Count} local runtime candidate(s); validate the selected runtime.";
            UpdateRuntimeUiState();
            return;
        }

        managedRuntimeInstall = RuntimeInstallStore.FindInstalled(selectedRuntimeArtifact);
        RuntimeCatalogStatus.Text = $"Managed package: {FormatRuntimeArtifact(selectedRuntimeArtifact)}";
        ManagedRuntimeStatus.Text = managedRuntimeInstall is not null
            ? $"Installed: {managedRuntimeInstall.Name} {managedRuntimeInstall.Version}; validate before launch."
            : runtimeCandidates.Count == 0
                ? "Not installed. Select Install Runtime to download and validate Wine."
                : $"Managed runtime not installed; {runtimeCandidates.Count} local candidate(s) detected.";
        UpdateRuntimeUiState();
    }

    private void RefreshInstalledUmbraFrameworkStatus()
    {
        if (selectedUmbraFrameworkArtifact is not null)
            umbraFrameworkInstall = UmbraInstallStore.FindInstalled(selectedUmbraFrameworkArtifact)
                ?? UmbraInstallStore.FindLatestInstalled()
                ?? UmbraInstallStore.FindBundled();
        else
            umbraFrameworkInstall = UmbraInstallStore.FindLatestInstalled()
                ?? UmbraInstallStore.FindBundled();

        if (umbraFrameworkInstall is null)
            AppendLog("Umbra framework: not installed.");
        else
            AppendLog($"Umbra framework: installed {umbraFrameworkInstall.Name} {umbraFrameworkInstall.Version}.");

        UpdateUmbraUiState();
    }

    private void SetRuntimeBusy(bool isBusy)
    {
        runtimeBusy = isBusy;
        InstallRuntimeButton.IsEnabled = !isBusy
            && platform.RequiresCompatibilityRuntime
            && ReadRuntimeMode() == RuntimeSelectionMode.AutomaticManaged;
        ValidateRuntimeButton.IsEnabled = !isBusy;
        VerifyDependenciesButton.IsEnabled = !isBusy;
        ResetPrefixButton.IsEnabled = !isBusy;
        RuntimeModeBox.IsEnabled = !isBusy;
        DetectedRuntimeBox.IsEnabled = !isBusy;
        CustomRuntimeKindBox.IsEnabled = !isBusy;
        RuntimeNameBox.IsEnabled = !isBusy;
        RuntimeCommandBox.IsEnabled = !isBusy;
        RuntimeValueBox.IsEnabled = !isBusy;
        BrowseRuntimeButton.IsEnabled = !isBusy;
        BrowseRuntimePrefixButton.IsEnabled = !isBusy;
        SaveRuntimeButton.IsEnabled = !isBusy;
    }

    private void SetUmbraBusy(bool isBusy)
    {
        InstallUmbraButton.IsEnabled = !isBusy;
        UmbraEnabledBox.IsEnabled = !isBusy;
    }

    private void UpdateRuntimeUiState()
    {
        bool isBusy = runtimeBusy;
        LaunchHelperModeBox.IsEnabled = !isBusy;

        if (!platform.RequiresCompatibilityRuntime)
        {
            GraphicsTargetBox.IsEnabled = false;
            return;
        }

        RuntimeSelectionMode mode = ReadRuntimeMode();
        bool automatic = mode == RuntimeSelectionMode.AutomaticManaged;
        bool custom = mode == RuntimeSelectionMode.CustomRuntime;
        bool customWinePrefix = custom && CustomRuntimeKindBox.SelectedIndex == 0;

        InstallRuntimeButton.IsEnabled = !isBusy && automatic;
        ValidateRuntimeButton.IsEnabled = !isBusy;
        VerifyDependenciesButton.IsEnabled = !isBusy;
        ResetPrefixButton.IsEnabled = !isBusy;
        LaunchHelperModeBox.IsEnabled = !isBusy;
        GraphicsTargetBox.IsEnabled = !isBusy && platform.RequiresCompatibilityRuntime;
        DetectedRuntimeBox.IsEnabled = !isBusy && automatic && runtimeCandidates.Count > 0;
        CustomRuntimeKindBox.IsEnabled = !isBusy && custom;
        RuntimeNameBox.IsEnabled = !isBusy && custom;
        RuntimeCommandBox.IsEnabled = !isBusy && custom;
        RuntimeValueBox.IsEnabled = !isBusy && customWinePrefix;
        BrowseRuntimeButton.IsEnabled = !isBusy && custom;
        BrowseRuntimePrefixButton.IsEnabled = !isBusy && customWinePrefix;
        SaveRuntimeButton.IsEnabled = !isBusy && custom;

        if (automatic)
        {
            RuntimeCatalogStatus.Text = selectedRuntimeArtifact is null
                ? RuntimeSetupGuidance.ForCurrentPlatform().Summary
                : $"Managed package: {FormatRuntimeArtifact(selectedRuntimeArtifact)}";
        }
    }

    private void UpdateUmbraUiState()
    {
        bool isBusy = umbraCancellation is not null;
        InstallUmbraButton.IsEnabled = !isBusy;
        UmbraEnabledBox.IsEnabled = !isBusy;
    }

    private static string FormatUmbraFrameworkArtifact(UmbraFrameworkArtifact artifact)
    {
        return $"{artifact.Name} {artifact.Version} ({artifact.PlatformRid}, {artifact.SizeBytes} bytes)";
    }

    private static string FormatRuntimeArtifact(RuntimeArtifact artifact)
    {
        return $"{artifact.Name} {artifact.Version} ({artifact.PlatformRid}, {artifact.SizeBytes} bytes)";
    }

    private string RuntimePrerequisiteStatusText(string action)
    {
        if (string.Equals(platform.RuntimeIdentifier, "osx-arm64", StringComparison.OrdinalIgnoreCase))
        {
            return $"{action}; checking Rosetta. If macOS asks to install it, complete the Apple prompt and the Launcher will continue.";
        }

        if (platform.OperatingSystem == LauncherOperatingSystem.Linux)
            return $"{action}; checking required Linux host libraries before Wine starts.";

        return $"{action}; validating Wine, prefix, and client helper.";
    }

    private static string FormatExecutableStatus(ClientInstallReport report)
    {
        string[] parts =
        {
            FormatStatus("boot", report.HasBootExecutable),
            FormatStatus("updater", report.HasUpdaterExecutable),
            FormatStatus("game", report.HasDirectGameExecutable),
            FormatStatus("config", report.HasConfigExecutable)
        };

        return string.Join(", ", parts);
    }

    private static string FormatRuntimeCandidate(RuntimeCandidate candidate)
    {
        string value = string.IsNullOrWhiteSpace(candidate.BottleOrPrefix)
            ? $"isolated prefix: {RuntimeInstallStore.ManagedPrefixPath}"
            : candidate.BottleOrPrefix;
        return $"{candidate.Name} | {candidate.Kind} | {candidate.Command} | {value}";
    }

    private ClientLaunchHelperMode ReadLaunchHelperMode()
    {
        return LaunchHelperModeBox.SelectedIndex switch
        {
            1 => ClientLaunchHelperMode.X86,
            2 => ClientLaunchHelperMode.X64,
            _ => ClientLaunchHelperMode.Automatic
        };
    }

    private ClientGraphicsTarget ReadGraphicsTarget()
    {
        return GraphicsTargetBox.SelectedIndex switch
        {
            1 => ClientGraphicsTarget.WineDefault,
            2 => ClientGraphicsTarget.OpenGLThreaded,
            3 => ClientGraphicsTarget.WineD3DVulkan,
            _ => ClientGraphicsTarget.OpenGLCompatibility
        };
    }

    private static string FormatLaunchHelperMode(ClientLaunchHelperMode mode)
    {
        return mode switch
        {
            ClientLaunchHelperMode.X86 => "32-bit helper (x86)",
            ClientLaunchHelperMode.X64 => "64-bit helper (x64)",
            ClientLaunchHelperMode.Arm64 => "ARM64 helper",
            _ => "Automatic"
        };
    }

    private static string FormatGraphicsTarget(ClientGraphicsTarget target)
    {
        return target switch
        {
            ClientGraphicsTarget.WineDefault => "Wine default",
            ClientGraphicsTarget.OpenGLThreaded => "OpenGL threaded",
            ClientGraphicsTarget.WineD3DVulkan => "WineD3D Vulkan",
            _ => "OpenGL compatibility"
        };
    }

    private static string FormatStatus(string label, bool isPresent)
    {
        return $"{label}={(isPresent ? "ok" : "missing")}";
    }

    private static bool IsKnownClientExecutable(string fileName)
    {
        return string.Equals(fileName, "ffxivboot.exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "ffxivgame.exe", StringComparison.OrdinalIgnoreCase);
    }

    private void LogMissingEntries(string label, IReadOnlyList<PatchEntry> entries)
    {
        foreach (PatchEntry entry in entries.Take(3))
            AppendLog($"{label}: {entry.RelativePatchPath}");

        if (entries.Count > 3)
            AppendLog($"{label}: {entries.Count - 3} more");
    }

    private void LogOptionalMetainfo(PatchLibraryReport report)
    {
        if (report.MissingMetainfoFiles.Count == 0)
            return;

        AppendLog(
            "Optional metainfo missing: "
            + $"{report.MissingMetainfoFiles.Count} torrent files. "
            + "Patch apply does not require them.");
    }

    private void LogInvalidEntries(IReadOnlyList<PatchFileReport> entries)
    {
        foreach (PatchFileReport report in entries.Take(3))
        {
            string actualSize = report.ActualSizeBytes?.ToString() ?? "missing";
            string actualCrc32 = report.ActualCrc32?.ToString("X8") ?? "not checked";
            AppendLog(
                "Invalid patch: "
                + $"{report.Entry.RelativePatchPath} "
                + $"expected {report.Entry.ExpectedSizeBytes} bytes/{report.Entry.ExpectedCrc32Text}, "
                + $"actual {actualSize} bytes/{actualCrc32}, "
                + $"path {report.PatchPath}");
        }

        if (entries.Count > 3)
            AppendLog($"Invalid patch: {entries.Count - 3} more");
    }

    private void AppendLog(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        LaunchLogBox.Text = string.IsNullOrEmpty(LaunchLogBox.Text)
            ? line
            : $"{LaunchLogBox.Text}{Environment.NewLine}{line}";
    }
}
