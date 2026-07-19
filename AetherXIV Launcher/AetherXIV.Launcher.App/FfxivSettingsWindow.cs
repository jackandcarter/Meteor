using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using AetherXIV.Launcher.Core;

namespace AetherXIV.Launcher.App;

public sealed class FfxivSettingsWindow : Window
{
    private readonly ClientInstall clientInstall;
    private readonly FfxivConfigStorageTarget storageTarget;
    private readonly ComboBox languageBox = new();
    private readonly ComboBox displayModeBox = new();
    private readonly ComboBox resolutionBox = new();
    private readonly ComboBox shadowMapQualityBox = new();
    private readonly ComboBox textureQualityBox = new();
    private readonly ComboBox backgroundQualityBox = new();
    private readonly ComboBox frameRateLimitBox = new();
    private readonly CheckBox repairSystemConfigBox = new();
    private readonly TextBlock systemConfigStatus = new();
    private readonly TextBlock saveStatus = new();

    public FfxivSettingsSaveResult? LastSaveResult { get; private set; }

    public FfxivSettingsWindow(ClientInstall clientInstall, FfxivConfigStorageTarget storageTarget)
    {
        this.clientInstall = clientInstall;
        this.storageTarget = storageTarget;

        Title = "FFXIV Settings";
        Width = 660;
        Height = 560;
        MinWidth = 560;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Avalonia.Media.Brush.Parse("#0D1117");
        Foreground = Avalonia.Media.Brush.Parse("#E7EAEE");

        Content = BuildContent();
        LoadSettings();
        RefreshSystemConfigStatus();
    }

    private Control BuildContent()
    {
        Grid root = new()
        {
            Margin = new Thickness(20),
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"),
            RowSpacing = 14
        };

        StackPanel header = new() { Spacing = 4 };
        header.Children.Add(new TextBlock
        {
            Text = "FFXIV Settings",
            FontSize = 22,
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });
        header.Children.Add(new TextBlock
        {
            Text = "AetherXIV native config",
            Foreground = Avalonia.Media.Brush.Parse("#AEB7C2")
        });
        root.Children.Add(header);

        Grid pathGrid = new()
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("150,*"),
            RowSpacing = 8,
            ColumnSpacing = 12
        };
        pathGrid.Children.Add(new TextBlock { Text = "Config folder", VerticalAlignment = VerticalAlignment.Center });
        TextBox configPathBox = new()
        {
            Text = storageTarget.HostConfigDirectoryPath,
            IsReadOnly = true
        };
        Grid.SetColumn(configPathBox, 1);
        pathGrid.Children.Add(configPathBox);

        TextBlock winePathLabel = new() { Text = "Windows path", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(winePathLabel, 1);
        pathGrid.Children.Add(winePathLabel);
        TextBox winePathBox = new()
        {
            Text = storageTarget.WindowsDocumentsPath,
            IsReadOnly = true
        };
        Grid.SetRow(winePathBox, 1);
        Grid.SetColumn(winePathBox, 1);
        pathGrid.Children.Add(winePathBox);
        Grid.SetRow(pathGrid, 1);
        root.Children.Add(pathGrid);

        TabControl tabs = new();
        tabs.Items.Add(new TabItem
        {
            Header = "General",
            Content = BuildGeneralTab()
        });
        tabs.Items.Add(new TabItem
        {
            Header = "Graphics",
            Content = BuildGraphicsTab()
        });
        Grid.SetRow(tabs, 2);
        root.Children.Add(tabs);

        saveStatus.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        saveStatus.Foreground = Avalonia.Media.Brush.Parse("#AEB7C2");
        Grid.SetRow(saveStatus, 3);
        root.Children.Add(saveStatus);

        StackPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        Button cancelButton = new() { Content = "Cancel", MinWidth = 92 };
        cancelButton.Click += (_, _) => Close(false);
        Button saveButton = new() { Content = "Save", MinWidth = 92 };
        saveButton.Classes.Add("primary");
        saveButton.Click += SaveButton_Click;
        actions.Children.Add(cancelButton);
        actions.Children.Add(saveButton);
        Grid.SetRow(actions, 4);
        root.Children.Add(actions);

        return root;
    }

    private Control BuildGeneralTab()
    {
        Grid settingsGrid = new()
        {
            Margin = new Thickness(0, 12, 0, 0),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("150,*"),
            RowSpacing = 10,
            ColumnSpacing = 12
        };
        settingsGrid.Children.Add(new TextBlock { Text = "Language", VerticalAlignment = VerticalAlignment.Center });
        languageBox.ItemsSource = LanguageOptions.All;
        languageBox.MinHeight = 32;
        Grid.SetColumn(languageBox, 1);
        settingsGrid.Children.Add(languageBox);

        TextBlock systemConfigLabel = new() { Text = "System config", VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(systemConfigLabel, 1);
        settingsGrid.Children.Add(systemConfigLabel);
        StackPanel systemConfigPanel = new() { Spacing = 6 };
        repairSystemConfigBox.Content = "Create or repair config.sys";
        repairSystemConfigBox.IsChecked = true;
        systemConfigPanel.Children.Add(repairSystemConfigBox);
        systemConfigPanel.Children.Add(systemConfigStatus);
        Grid.SetRow(systemConfigPanel, 1);
        Grid.SetColumn(systemConfigPanel, 1);
        settingsGrid.Children.Add(systemConfigPanel);

        return settingsGrid;
    }

    private Control BuildGraphicsTab()
    {
        Grid grid = new()
        {
            Margin = new Thickness(0, 12, 0, 0),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("180,*"),
            RowSpacing = 10,
            ColumnSpacing = 12
        };

        AddComboRow(grid, 0, "Screen mode", displayModeBox, DisplayModeOptions.All);
        AddComboRow(grid, 1, "Resolution", resolutionBox, ResolutionOptions.Default);
        AddComboRow(grid, 2, "Shadow map quality", shadowMapQualityBox, ShadowMapQualityOptions.All);
        AddComboRow(grid, 3, "Texture quality", textureQualityBox, IndexedQualityOptions.ThreeStep);
        AddComboRow(grid, 4, "Background quality", backgroundQualityBox, IndexedQualityOptions.FourStep);
        AddComboRow(grid, 5, "Frame rate cap", frameRateLimitBox, FrameRateLimitOptions.All);

        return grid;
    }

    private static void AddComboRow<T>(Grid grid, int row, string label, ComboBox comboBox, IReadOnlyList<T> options)
    {
        TextBlock textBlock = new()
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(textBlock, row);
        grid.Children.Add(textBlock);

        comboBox.ItemsSource = options;
        comboBox.MinHeight = 32;
        Grid.SetRow(comboBox, row);
        Grid.SetColumn(comboBox, 1);
        grid.Children.Add(comboBox);
    }

    private void LoadSettings()
    {
        FfxivClientSettings settings = FfxivClientSettingsStore.LoadOrDefault(storageTarget.HostConfigDirectoryPath);
        languageBox.SelectedItem = LanguageOptions.All.FirstOrDefault(option => option.Value == settings.Language)
            ?? LanguageOptions.All[1];

        displayModeBox.SelectedItem = DisplayModeOptions.All.FirstOrDefault(option => option.Value == settings.SystemConfig.DisplayMode)
            ?? DisplayModeOptions.All[0];

        List<ResolutionOptions> resolutionOptions = ResolutionOptions.CreateFor(settings.SystemConfig);
        resolutionBox.ItemsSource = resolutionOptions;
        resolutionBox.SelectedItem = resolutionOptions.FirstOrDefault(option =>
                option.Width == settings.SystemConfig.ResolutionWidth
                && option.Height == settings.SystemConfig.ResolutionHeight)
            ?? resolutionOptions.First(option => option.Width == FfxivSystemConfig.Default.ResolutionWidth
                && option.Height == FfxivSystemConfig.Default.ResolutionHeight);

        shadowMapQualityBox.SelectedItem = ShadowMapQualityOptions.All.FirstOrDefault(option => option.Value == settings.SystemConfig.ShadowMapQuality)
            ?? ShadowMapQualityOptions.All[^1];
        textureQualityBox.SelectedItem = IndexedQualityOptions.ThreeStep.FirstOrDefault(option => option.Value == settings.SystemConfig.TextureQualityIndex)
            ?? IndexedQualityOptions.ThreeStep[1];
        backgroundQualityBox.SelectedItem = IndexedQualityOptions.FourStep.FirstOrDefault(option => option.Value == settings.SystemConfig.BackgroundQualityIndex)
            ?? IndexedQualityOptions.FourStep[2];
        frameRateLimitBox.SelectedItem = FrameRateLimitOptions.All.FirstOrDefault(option => option.Value == settings.SystemConfig.FrameRateLimit)
            ?? FrameRateLimitOptions.All[0];
    }

    private void RefreshSystemConfigStatus()
    {
        string systemPath = Path.Combine(storageTarget.HostConfigDirectoryPath, "config.sys");
        bool usable = FfxivClientSettingsStore.IsUsableSystemConfig(systemPath);
        systemConfigStatus.Text = usable
            ? "config.sys is present and valid."
            : "config.sys needs to be created or repaired.";
        repairSystemConfigBox.IsChecked = !usable;
    }

    private void SaveButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            FfxivClientLanguage language = languageBox.SelectedItem is LanguageOptions selected
                ? selected.Value
                : FfxivClientLanguage.English;
            FfxivSystemConfig systemConfig = CreateSystemConfigFromControls();

            LastSaveResult = FfxivClientSettingsStore.Save(
                clientInstall,
                storageTarget.HostConfigDirectoryPath,
                new FfxivClientSettings(language, systemConfig),
                repairSystemConfig: repairSystemConfigBox.IsChecked == true);

            saveStatus.Text = LastSaveResult.BackupPaths.Count == 0
                ? "Settings saved."
                : $"Settings saved. Backups created: {LastSaveResult.BackupPaths.Count}.";
            RefreshSystemConfigStatus();
            Close(true);
        }
        catch (Exception ex)
        {
            saveStatus.Text = $"Settings save failed: {ex.Message}";
        }
    }

    private FfxivSystemConfig CreateSystemConfigFromControls()
    {
        FfxivDisplayMode displayMode = displayModeBox.SelectedItem is DisplayModeOptions displayModeOption
            ? displayModeOption.Value
            : FfxivSystemConfig.Default.DisplayMode;
        ResolutionOptions resolution = resolutionBox.SelectedItem as ResolutionOptions
            ?? ResolutionOptions.Default.First(option => option.Width == FfxivSystemConfig.Default.ResolutionWidth
                && option.Height == FfxivSystemConfig.Default.ResolutionHeight);
        FfxivShadowMapQuality shadowMapQuality = shadowMapQualityBox.SelectedItem is ShadowMapQualityOptions shadowOption
            ? shadowOption.Value
            : FfxivSystemConfig.Default.ShadowMapQuality;
        int textureQuality = textureQualityBox.SelectedItem is IndexedQualityOptions textureOption
            ? textureOption.Value
            : FfxivSystemConfig.Default.TextureQualityIndex;
        int backgroundQuality = backgroundQualityBox.SelectedItem is IndexedQualityOptions backgroundOption
            ? backgroundOption.Value
            : FfxivSystemConfig.Default.BackgroundQualityIndex;
        FfxivFrameRateLimit frameRateLimit = frameRateLimitBox.SelectedItem is FrameRateLimitOptions frameOption
            ? frameOption.Value
            : FfxivSystemConfig.Default.FrameRateLimit;

        return new FfxivSystemConfig(
            displayMode,
            resolution.Width,
            resolution.Height,
            shadowMapQuality,
            textureQuality,
            backgroundQuality,
            frameRateLimit);
    }

    private sealed record LanguageOptions(FfxivClientLanguage Value, string Label)
    {
        public static readonly IReadOnlyList<LanguageOptions> All = new[]
        {
            new LanguageOptions(FfxivClientLanguage.Japanese, "Japanese"),
            new LanguageOptions(FfxivClientLanguage.English, "English"),
            new LanguageOptions(FfxivClientLanguage.German, "German"),
            new LanguageOptions(FfxivClientLanguage.French, "French")
        };

        public override string ToString() => Label;
    }

    private sealed record DisplayModeOptions(FfxivDisplayMode Value, string Label)
    {
        public static readonly IReadOnlyList<DisplayModeOptions> All = new[]
        {
            new DisplayModeOptions(FfxivDisplayMode.Windowed, "Windowed"),
            new DisplayModeOptions(FfxivDisplayMode.Fullscreen, "Fullscreen")
        };

        public override string ToString() => Label;
    }

    private sealed record ResolutionOptions(int Width, int Height)
    {
        public static readonly IReadOnlyList<ResolutionOptions> Default = new[]
        {
            new ResolutionOptions(800, 600),
            new ResolutionOptions(1024, 768),
            new ResolutionOptions(1280, 720),
            new ResolutionOptions(1280, 800),
            new ResolutionOptions(1366, 768),
            new ResolutionOptions(1440, 900),
            new ResolutionOptions(1600, 900),
            new ResolutionOptions(1680, 1050),
            new ResolutionOptions(1920, 1080),
            new ResolutionOptions(1920, 1200),
            new ResolutionOptions(2560, 1440),
            new ResolutionOptions(2560, 1600),
            new ResolutionOptions(3440, 1440),
            new ResolutionOptions(3840, 2160)
        };

        public static List<ResolutionOptions> CreateFor(FfxivSystemConfig config)
        {
            List<ResolutionOptions> options = Default.ToList();
            if (!options.Any(option => option.Width == config.ResolutionWidth && option.Height == config.ResolutionHeight))
                options.Insert(0, new ResolutionOptions(config.ResolutionWidth, config.ResolutionHeight));

            return options;
        }

        public override string ToString() => $"{Width} x {Height}";
    }

    private sealed record ShadowMapQualityOptions(FfxivShadowMapQuality Value, string Label)
    {
        public static readonly IReadOnlyList<ShadowMapQualityOptions> All = new[]
        {
            new ShadowMapQualityOptions(FfxivShadowMapQuality.Lowest, "Lowest"),
            new ShadowMapQualityOptions(FfxivShadowMapQuality.Low, "Low"),
            new ShadowMapQualityOptions(FfxivShadowMapQuality.Standard, "Standard"),
            new ShadowMapQualityOptions(FfxivShadowMapQuality.High, "High"),
            new ShadowMapQualityOptions(FfxivShadowMapQuality.Highest, "Highest")
        };

        public override string ToString() => Label;
    }

    private sealed record IndexedQualityOptions(int Value, string Label)
    {
        public static readonly IReadOnlyList<IndexedQualityOptions> ThreeStep = new[]
        {
            new IndexedQualityOptions(0, "High"),
            new IndexedQualityOptions(1, "Standard"),
            new IndexedQualityOptions(2, "Low")
        };

        public static readonly IReadOnlyList<IndexedQualityOptions> FourStep = new[]
        {
            new IndexedQualityOptions(0, "Highest"),
            new IndexedQualityOptions(1, "High"),
            new IndexedQualityOptions(2, "Standard"),
            new IndexedQualityOptions(3, "Low")
        };

        public override string ToString() => Label;
    }

    private sealed record FrameRateLimitOptions(FfxivFrameRateLimit Value, string Label)
    {
        public static readonly IReadOnlyList<FrameRateLimitOptions> All = new[]
        {
            new FrameRateLimitOptions(FfxivFrameRateLimit.Fps60, "60 FPS"),
            new FrameRateLimitOptions(FfxivFrameRateLimit.Fps30, "30 FPS"),
            new FrameRateLimitOptions(FfxivFrameRateLimit.Fps20, "20 FPS"),
            new FrameRateLimitOptions(FfxivFrameRateLimit.Fps15, "15 FPS"),
            new FrameRateLimitOptions(FfxivFrameRateLimit.Fps10, "10 FPS")
        };

        public override string ToString() => Label;
    }
}
