using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AetherXIV.Operator;
using System.Text;

namespace AetherXIV.UI.App;

public sealed partial class MainWindow : Window
{
    private readonly Dictionary<AetherXivManagedService, ServiceRowControls> serviceRows = new();
    private readonly Dictionary<AetherXivManagedService, TextBox> logBoxes = new();
    private readonly AetherXivLiveLogBuffer liveLogBuffer = new();
    private readonly DispatcherTimer liveLogFlushTimer;
    private readonly AetherXivDatabasePreflightService databasePreflight = new();
    private readonly AetherXivDatabaseInstaller databaseInstaller = new();
    private readonly AetherXivDependencyPreflightService dependencyPreflight = new();
    private readonly LauncherContentAdminService launcherContentAdmin = new();
    private IReadOnlyList<LauncherNewsAdminItem> launcherNewsItems = [];
    private IReadOnlyDictionary<string, LauncherReelTextAdminItem> reelTextItems =
        new Dictionary<string, LauncherReelTextAdminItem>(StringComparer.OrdinalIgnoreCase);
    private int editingNewsId;
    private bool suppressReelEvents;
    private bool launcherContentBusy;
    private AetherXivOperatorConfig config;
    private AetherXivOperatorConfig supervisorConfig;
    private AetherXivServiceSupervisor supervisor;
    private bool operationInProgress;
    private bool mirrorPreflightToServiceLogs;

    public MainWindow()
    {
        InitializeComponent();
        liveLogFlushTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        liveLogFlushTimer.Tick += (_, _) => FlushLiveServiceLogs();
        liveLogFlushTimer.Start();
        string defaultRoot = AetherXivOperatorPaths.FindWorkspaceRoot(AppContext.BaseDirectory);
        config = PreferPackagedLaunchRoot(
            AetherXivOperatorConfigStore.LoadOrCreate(workspaceRoot: defaultRoot),
            defaultRoot);
        supervisorConfig = config;
        supervisor = CreateSupervisor(config);
        ApplyConfigToFields(config);
        BuildServiceRows();
        BuildLogTabs();
        ConfigureColorSwatches();
        ResetNewsEditor();
        DetectReelImages();
        RefreshHeader();
        Closing += (_, _) =>
        {
            liveLogFlushTimer.Stop();
            if (supervisor.HasRunningServices)
                supervisor.StopStackAsync().GetAwaiter().GetResult();

            supervisor.Dispose();
        };
    }

    private AetherXivServiceSupervisor CreateSupervisor(AetherXivOperatorConfig nextConfig)
    {
        AetherXivServiceSupervisor next = new(nextConfig);
        next.LogReceived += Supervisor_LogReceived;
        next.StateChanged += Supervisor_StateChanged;
        return next;
    }

    private static AetherXivOperatorConfig PreferPackagedLaunchRoot(
        AetherXivOperatorConfig loaded,
        string launchRoot)
    {
        if (!AetherXivOperatorPaths.IsPackagedRoot(launchRoot))
            return loaded.Normalize();

        AetherXivOperatorConfig normalized = loaded.Normalize();
        string packagedRoot = Path.GetFullPath(launchRoot);
        if (String.Equals(normalized.WorkspaceRoot, packagedRoot, StringComparison.Ordinal))
            return normalized;

        return normalized with
        {
            WorkspaceRoot = packagedRoot,
            DataRoot = packagedRoot,
            ScriptsRoot = AetherXivOperatorPaths.ResolveScriptsRoot(packagedRoot)
        };
    }

    private bool TryRebuildSupervisor()
    {
        if (supervisor.HasRunningServices)
        {
            HeaderStatusText.Text = "Stop stack before reloading config";
            return false;
        }

        supervisor.Dispose();
        supervisor = CreateSupervisor(config);
        supervisorConfig = config;
        serviceRows.Clear();
        logBoxes.Clear();
        ServicesPanel.Children.Clear();
        ServiceLogTabs.Items.Clear();
        BuildServiceRows();
        BuildLogTabs();
        RefreshHeader();
        return true;
    }

    private async void StartStack_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (operationInProgress)
            return;

        if (!TrySaveConfig(reloadSupervisor: true, requireReloadForCurrentConfig: true))
            return;

        SetBusy(true, "Starting stack");
        try
        {
            if (!await EnsureStartupPreflightAsync())
                return;

            await supervisor.StartStackAsync();
            RefreshHeader();
        }
        catch (Exception ex)
        {
            HeaderStatusText.Text = $"Start failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void StopStack_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (operationInProgress)
            return;

        SetBusy(true, "Stopping stack");
        try
        {
            await supervisor.StopStackAsync();
            RefreshHeader();
        }
        catch (Exception ex)
        {
            HeaderStatusText.Text = $"Stop failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SaveConfig_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (operationInProgress)
            return;

        TrySaveConfig(reloadSupervisor: true, requireReloadForCurrentConfig: false);
    }

    private async void RefreshLauncherContent_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await RefreshLauncherContentAsync();

    private async Task RefreshLauncherContentAsync(int? selectNewsId = null, string? selectReelImage = null)
    {
        if (launcherContentBusy)
            return;

        if (!TrySaveConfig(reloadSupervisor: false, requireReloadForCurrentConfig: false))
            return;

        launcherContentBusy = true;
        LauncherContentStatusText.Text = "Checking the launcher content database…";
        try
        {
            AetherXivDatabasePreflightResult preflight = await databasePreflight.RunAsync(
                config,
                config.AutoRepairDatabase).ConfigureAwait(true);
            if (!preflight.CanStartServices)
            {
                LauncherContentStatusText.Text = preflight.NeedsAdminCredentials
                    ? "Database setup needs MariaDB admin credentials. Start the stack once to complete setup, then refresh."
                    : "Launcher content schema is not ready. Enable Auto setup/repair in Config or start the stack, then refresh.";
                return;
            }

            launcherNewsItems = await launcherContentAdmin.GetNewsAsync(config.Database).ConfigureAwait(true);
            LauncherPresentationAdminState presentation = await launcherContentAdmin
                .GetPresentationAsync(config.Database)
                .ConfigureAwait(true);
            reelTextItems = presentation.ReelTextItems.ToDictionary(
                item => item.ImageFile,
                StringComparer.OrdinalIgnoreCase);

            RenderNewsPostList();
            suppressReelEvents = true;
            ReelTextEnabledBox.IsChecked = presentation.ReelTextEnabled;
            ReelTextEditorPanel.IsEnabled = presentation.ReelTextEnabled;
            suppressReelEvents = false;
            DetectReelImages(selectReelImage);

            if (selectNewsId is int id)
            {
                LauncherNewsAdminItem? selected = launcherNewsItems.FirstOrDefault(item => item.Id == id);
                if (selected is not null)
                    LoadNewsEditor(selected);
            }

            LauncherContentStatusText.Text = $"Connected to {config.Database.Name}. {launcherNewsItems.Count} news post(s) and {reelTextItems.Count} reel caption(s) loaded.";
        }
        catch (Exception ex)
        {
            LauncherContentStatusText.Text = $"Could not load launcher content: {ex.Message}";
        }
        finally
        {
            launcherContentBusy = false;
        }
    }

    private void RenderNewsPostList()
    {
        NewsPostsPanel.Children.Clear();
        NewsPostCountText.Text = $"{launcherNewsItems.Count} post{(launcherNewsItems.Count == 1 ? "" : "s")}";
        if (launcherNewsItems.Count == 0)
        {
            NewsPostsPanel.Children.Add(new TextBlock
            {
                Text = "No posts yet. Select New and create the first launcher update.",
                Foreground = Brush.Parse("#AAB5C0"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 10)
            });
            return;
        }

        foreach (LauncherNewsAdminItem item in launcherNewsItems)
        {
            Button button = new()
            {
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 10),
                Tag = item.Id,
                Content = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = item.Title,
                            FontWeight = FontWeight.SemiBold,
                            TextWrapping = TextWrapping.Wrap
                        },
                        new TextBlock
                        {
                            Text = item.Summary,
                            Foreground = Brush.Parse("#AAB5C0"),
                            TextWrapping = TextWrapping.Wrap,
                            MaxLines = 2,
                            TextTrimming = TextTrimming.CharacterEllipsis
                        },
                        new TextBlock
                        {
                            Text = $"{(item.IsActive ? "Published" : "Draft")}  •  {item.PublishedAt.UtcDateTime:MMM d, yyyy HH:mm} UTC  •  Sort {item.SortOrder}",
                            Foreground = Brush.Parse(item.IsActive ? "#86D7A1" : "#D6B77A"),
                            FontSize = 11
                        }
                    }
                }
            };
            button.Click += (_, _) => LoadNewsEditor(item);
            NewsPostsPanel.Children.Add(button);
        }
    }

    private void NewNewsPost_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ResetNewsEditor();

    private void ResetNewsEditor()
    {
        editingNewsId = 0;
        NewsEditorHeadingText.Text = "Create News Post";
        NewsTitleBox.Text = "";
        NewsSummaryBox.Text = "";
        NewsBodyBox.Text = "";
        NewsBannerUrlBox.Text = "";
        NewsLinkUrlBox.Text = "";
        DateTimeOffset now = DateTimeOffset.UtcNow;
        NewsPublishDatePicker.SelectedDate = now;
        NewsPublishTimePicker.SelectedTime = now.TimeOfDay;
        NewsSortOrderBox.Value = 0;
        NewsPublishedBox.IsChecked = true;
        SetSwatchColor(NewsTitleColorButton, "#F2F4FA");
        SetSwatchColor(NewsSummaryColorButton, "#D6DCE3");
        SetSwatchColor(NewsBodyColorButton, "#AEB7C2");
        DeleteNewsPostButton.IsEnabled = false;
        NewsFormHintText.Text = "Ready for a new post.";
    }

    private void LoadNewsEditor(LauncherNewsAdminItem item)
    {
        editingNewsId = item.Id;
        NewsEditorHeadingText.Text = $"Edit News Post #{item.Id}";
        NewsTitleBox.Text = item.Title;
        NewsSummaryBox.Text = item.Summary;
        NewsBodyBox.Text = item.Body;
        NewsBannerUrlBox.Text = item.BannerUrl;
        NewsLinkUrlBox.Text = item.LinkUrl;
        NewsPublishDatePicker.SelectedDate = item.PublishedAt;
        NewsPublishTimePicker.SelectedTime = item.PublishedAt.TimeOfDay;
        NewsSortOrderBox.Value = item.SortOrder;
        NewsPublishedBox.IsChecked = item.IsActive;
        SetSwatchColor(NewsTitleColorButton, item.TitleColor);
        SetSwatchColor(NewsSummaryColorButton, item.SummaryColor);
        SetSwatchColor(NewsBodyColorButton, item.BodyColor);
        DeleteNewsPostButton.IsEnabled = true;
        NewsFormHintText.Text = item.IsActive ? "Editing a published post." : "Editing a draft post.";
    }

    private async void SaveNewsPost_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (launcherContentBusy)
            return;

        launcherContentBusy = true;
        try
        {
            DateTimeOffset date = NewsPublishDatePicker.SelectedDate ?? DateTimeOffset.UtcNow;
            TimeSpan time = NewsPublishTimePicker.SelectedTime ?? TimeSpan.Zero;
            DateTime utcDateTime = new(date.Year, date.Month, date.Day, time.Hours, time.Minutes, time.Seconds, DateTimeKind.Utc);
            LauncherNewsAdminItem item = new(
                editingNewsId,
                NewsTitleBox.Text ?? "",
                NewsSummaryBox.Text ?? "",
                NewsBodyBox.Text ?? "",
                NewsBannerUrlBox.Text ?? "",
                NewsLinkUrlBox.Text ?? "",
                new DateTimeOffset(utcDateTime),
                NewsPublishedBox.IsChecked == true,
                Decimal.ToInt32(NewsSortOrderBox.Value ?? 0),
                GetSwatchColor(NewsTitleColorButton),
                GetSwatchColor(NewsSummaryColorButton),
                GetSwatchColor(NewsBodyColorButton));
            int savedId = await launcherContentAdmin.SaveNewsAsync(config.Database, item).ConfigureAwait(true);
            NewsFormHintText.Text = editingNewsId > 0 ? "Post updated." : "Post created.";
            launcherContentBusy = false;
            await RefreshLauncherContentAsync(savedId);
        }
        catch (Exception ex)
        {
            NewsFormHintText.Text = $"Save failed: {ex.Message}";
        }
        finally
        {
            launcherContentBusy = false;
        }
    }

    private async void DeleteNewsPost_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (editingNewsId <= 0 || launcherContentBusy)
            return;
        if (!await ConfirmAsync("Delete news post?", "This permanently removes the selected launcher news post."))
            return;

        launcherContentBusy = true;
        try
        {
            await launcherContentAdmin.DeleteNewsAsync(config.Database, editingNewsId).ConfigureAwait(true);
            ResetNewsEditor();
            launcherContentBusy = false;
            await RefreshLauncherContentAsync();
        }
        catch (Exception ex)
        {
            NewsFormHintText.Text = $"Delete failed: {ex.Message}";
        }
        finally
        {
            launcherContentBusy = false;
        }
    }

    private void DetectReelImages(string? selectImage = null)
    {
        string? previous = selectImage ?? ReelImageBox.SelectedItem as string;
        string reelsDirectory = Path.Combine(config.WorkspaceRoot, "AetherXIV Launcher", "Image", "Reels");
        string[] extensions = [".jpg", ".jpeg", ".png", ".webp"];
        string[] images = Directory.Exists(reelsDirectory)
            ? Directory.EnumerateFiles(reelsDirectory)
                .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .Select(Path.GetFileName)
                .Where(name => !String.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : [];

        ReelImageBox.ItemsSource = images;
        ReelFolderHintText.Text = images.Length == 0
            ? $"No supported images found in {reelsDirectory}"
            : $"Detected {images.Length} image{(images.Length == 1 ? "" : "s")} in the Reels folder.";
        if (images.Length == 0)
        {
            ClearReelEditor();
            return;
        }

        ReelImageBox.SelectedItem = images.FirstOrDefault(name => String.Equals(name, previous, StringComparison.OrdinalIgnoreCase))
            ?? images[0];
    }

    private async void ReelTextEnabled_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (suppressReelEvents || launcherContentBusy)
            return;

        bool enabled = ReelTextEnabledBox.IsChecked == true;
        ReelTextEditorPanel.IsEnabled = enabled;
        try
        {
            await launcherContentAdmin.SetReelTextEnabledAsync(config.Database, enabled).ConfigureAwait(true);
            LauncherContentStatusText.Text = enabled
                ? "Reel Image Text enabled. Choose an image and save its text."
                : "Reel Image Text disabled globally. Saved image captions are preserved.";
        }
        catch (Exception ex)
        {
            LauncherContentStatusText.Text = $"Could not save Reel Image Text setting: {ex.Message}";
        }
    }

    private void ReelImage_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (suppressReelEvents)
            return;

        string? imageFile = ReelImageBox.SelectedItem as string;
        if (String.IsNullOrWhiteSpace(imageFile))
        {
            ClearReelEditor();
            return;
        }

        if (reelTextItems.TryGetValue(imageFile, out LauncherReelTextAdminItem? item))
        {
            ReelHeaderBox.Text = item.HeaderText;
            ReelSubTextBox.Text = item.SubText;
            ReelHeaderSizeBox.Value = (decimal)item.HeaderSize;
            ReelSubTextSizeBox.Value = (decimal)item.SubTextSize;
            SetSwatchColor(ReelHeaderColorButton, item.HeaderColor);
            SetSwatchColor(ReelSubTextColorButton, item.SubTextColor);
            ReelImageEnabledBox.IsChecked = item.IsEnabled;
            ReelFormHintText.Text = item.IsEnabled ? "Saved text is enabled for this image." : "Saved text is disabled for this image.";
        }
        else
        {
            ClearReelEditor();
            ReelFormHintText.Text = "No text has been saved for this image yet.";
        }
    }

    private void ClearReelEditor()
    {
        ReelHeaderBox.Text = "";
        ReelSubTextBox.Text = "";
        ReelHeaderSizeBox.Value = 32;
        ReelSubTextSizeBox.Value = 18;
        SetSwatchColor(ReelHeaderColorButton, "#FFFFFFFF");
        SetSwatchColor(ReelSubTextColorButton, "#FFD7E0EE");
        ReelImageEnabledBox.IsChecked = true;
        ReelFormHintText.Text = "Choose an image to begin.";
    }

    private async void SaveReelText_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string imageFile = ReelImageBox.SelectedItem as string ?? "";
        try
        {
            LauncherReelTextAdminItem item = new(
                imageFile,
                ReelHeaderBox.Text ?? "",
                ReelSubTextBox.Text ?? "",
                Decimal.ToDouble(ReelHeaderSizeBox.Value ?? 32),
                Decimal.ToDouble(ReelSubTextSizeBox.Value ?? 18),
                GetSwatchColor(ReelHeaderColorButton),
                GetSwatchColor(ReelSubTextColorButton),
                ReelImageEnabledBox.IsChecked == true);
            await launcherContentAdmin.SaveReelTextAsync(config.Database, item).ConfigureAwait(true);
            ReelFormHintText.Text = "Image text saved.";
            await RefreshLauncherContentAsync(selectReelImage: imageFile);
        }
        catch (Exception ex)
        {
            ReelFormHintText.Text = $"Save failed: {ex.Message}";
        }
    }

    private async void DeleteReelText_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        string imageFile = ReelImageBox.SelectedItem as string ?? "";
        if (String.IsNullOrWhiteSpace(imageFile))
            return;
        if (!await ConfirmAsync("Remove image text?", $"This removes the saved header and subtext for {imageFile}. The reel image stays in place."))
            return;

        try
        {
            await launcherContentAdmin.DeleteReelTextAsync(config.Database, imageFile).ConfigureAwait(true);
            ReelFormHintText.Text = "Saved image text removed.";
            await RefreshLauncherContentAsync(selectReelImage: imageFile);
        }
        catch (Exception ex)
        {
            ReelFormHintText.Text = $"Remove failed: {ex.Message}";
        }
    }

    private void ConfigureColorSwatches()
    {
        foreach (Button button in new[]
        {
            NewsTitleColorButton, NewsSummaryColorButton, NewsBodyColorButton,
            ReelHeaderColorButton, ReelSubTextColorButton
        })
        {
            ConfigureColorSwatch(button);
        }
    }

    private void ConfigureColorSwatch(Button button)
    {
        string[] palette =
        [
            "#FFFFFFFF", "#F2F4FA", "#D6DCE3", "#AEB7C2", "#8FC9FF",
            "#86D7A1", "#FFD37A", "#FF9B9B", "#D2A8FF", "#FFB4DC"
        ];
        List<MenuItem> items = [];
        foreach (string color in palette)
        {
            MenuItem item = new()
            {
                Header = color,
                Foreground = Brush.Parse(color)
            };
            item.Click += (_, _) => SetSwatchColor(button, color);
            items.Add(item);
        }

        MenuItem custom = new() { Header = "Custom hex color…" };
        custom.Click += async (_, _) =>
        {
            string? color = await RequestHexColorAsync(GetSwatchColor(button));
            if (color is not null)
                SetSwatchColor(button, color);
        };
        items.Add(custom);
        button.ContextMenu = new ContextMenu { ItemsSource = items };
    }

    private void ColorSwatch_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button)
            button.ContextMenu?.Open(button);
    }

    private static string GetSwatchColor(Button button) => button.Tag as string ?? button.Content?.ToString() ?? "#FFFFFFFF";

    private static void SetSwatchColor(Button button, string color)
    {
        string normalized = LauncherContentAdminService.NormalizeColor(color, "#FFFFFFFF");
        button.Tag = normalized;
        button.Content = normalized;
        button.BorderBrush = Brush.Parse(normalized);
    }

    private async Task<string?> RequestHexColorAsync(string current)
    {
        TextBox input = new() { Text = current, MinWidth = 230 };
        TextBlock error = new() { Foreground = Brush.Parse("#FF9B9B") };
        Button cancel = new() { Content = "Cancel" };
        Button apply = new() { Content = "Apply Color" };
        apply.Classes.Add("primary");
        Window dialog = new()
        {
            Title = "Custom Color",
            Width = 390,
            Height = 230,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush.Parse("#101418")
        };
        cancel.Click += (_, _) => dialog.Close(null);
        apply.Click += (_, _) =>
        {
            string candidate = input.Text?.Trim() ?? "";
            string normalized = LauncherContentAdminService.NormalizeColor(candidate, "");
            if (String.IsNullOrEmpty(normalized))
            {
                error.Text = "Use #RRGGBB or #AARRGGBB.";
                return;
            }
            dialog.Close(normalized);
        };
        dialog.Content = new Border
        {
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = "Enter a hexadecimal color. Eight digits include opacity first.", TextWrapping = TextWrapping.Wrap },
                    input,
                    error,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancel, apply }
                    }
                }
            }
        };
        return await dialog.ShowDialog<string?>(this).ConfigureAwait(true);
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        Button cancel = new() { Content = "Cancel" };
        Button confirm = new() { Content = "Delete" };
        confirm.Classes.Add("danger");
        Window dialog = new()
        {
            Title = title,
            Width = 430,
            Height = 210,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush.Parse("#101418")
        };
        cancel.Click += (_, _) => dialog.Close(false);
        confirm.Click += (_, _) => dialog.Close(true);
        dialog.Content = new Border
        {
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancel, confirm }
                    }
                }
            }
        };
        return await dialog.ShowDialog<bool>(this).ConfigureAwait(true);
    }

    private void VerifyDependencies_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (operationInProgress)
            return;

        if (!TrySaveConfig(reloadSupervisor: false, requireReloadForCurrentConfig: false))
            return;

        SetBusy(true, "Checking dependencies");
        try
        {
            RunDependencyPreflight(clearStatus: true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void UsePublicServiceBinds_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (operationInProgress)
            return;

        try
        {
            WorldBindBox.Text = WithWildcardBindHost(WorldBindBox.Text, "World");
            LobbyBindBox.Text = WithWildcardBindHost(LobbyBindBox.Text, "Lobby");
            bool stackRunning = supervisor.HasRunningServices;
            if (!TrySaveConfig(reloadSupervisor: true, requireReloadForCurrentConfig: false))
                return;

            HeaderStatusText.Text = stackRunning
                ? "Public World/Lobby binds saved; stop and restart the stack to apply them"
                : "Public World/Lobby binds saved; set public DNS names in Advertise, then verify dependencies";
        }
        catch (Exception ex)
        {
            HeaderStatusText.Text = $"Public bind setup failed: {ex.Message}";
        }
    }

    private static string WithWildcardBindHost(string? endpoint, string serviceName)
    {
        string value = endpoint?.Trim() ?? "";
        int separator = value.LastIndexOf(':');
        if (separator <= 0 || separator == value.Length - 1
            || !UInt16.TryParse(value[(separator + 1)..], out ushort port))
        {
            throw new InvalidOperationException($"{serviceName} endpoint must use host:port syntax.");
        }

        return $"0.0.0.0:{port}";
    }

    private bool TrySaveConfig(bool reloadSupervisor, bool requireReloadForCurrentConfig)
    {
        try
        {
            config = ReadConfigFromFields().Normalize();
            AetherXivOperatorConfigStore.Save(config);
            ApplyConfigToFields(config);
            bool needsSupervisorReload = config != supervisorConfig;
            if (reloadSupervisor && needsSupervisorReload)
            {
                bool reloaded = TryRebuildSupervisor();
                if (!reloaded)
                {
                    HeaderStatusText.Text = "Config saved; stop stack before applying service changes";
                    return !requireReloadForCurrentConfig;
                }
            }

            HeaderStatusText.Text = needsSupervisorReload && !reloadSupervisor
                ? "Config saved for preflight; service changes apply after stack reload"
                : $"Config saved: {AetherXivOperatorConfigStore.DefaultPath}";
            return true;
        }
        catch (Exception ex)
        {
            HeaderStatusText.Text = $"Config save failed: {ex.Message}";
            return false;
        }
    }

    private void BuildServiceRows()
    {
        foreach (AetherXivServiceProcess process in supervisor.Processes)
        {
            Grid row = new()
            {
                ColumnDefinitions = new ColumnDefinitions("120,90,90,*,96,96"),
                ColumnSpacing = 8,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock name = new()
            {
                Text = process.Definition.DisplayName,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock state = new()
            {
                Text = process.State.ToString(),
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock pid = new()
            {
                Text = "-",
                Foreground = Brush.Parse("#AAB5C0"),
                VerticalAlignment = VerticalAlignment.Center
            };
            TextBlock endpoint = new()
            {
                Text = process.Definition.BindEndpoint,
                Foreground = Brush.Parse("#C5CED8"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Button start = new()
            {
                Content = "Start",
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Button stop = new()
            {
                Content = "Stop",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = false
            };

            start.Click += async (_, _) => await StartServiceAsync(process.Definition.Kind);
            stop.Click += async (_, _) => await StopServiceAsync(process.Definition.Kind);

            row.Children.Add(name);
            Grid.SetColumn(state, 1);
            row.Children.Add(state);
            Grid.SetColumn(pid, 2);
            row.Children.Add(pid);
            Grid.SetColumn(endpoint, 3);
            row.Children.Add(endpoint);
            Grid.SetColumn(start, 4);
            row.Children.Add(start);
            Grid.SetColumn(stop, 5);
            row.Children.Add(stop);

            Border wrapper = new()
            {
                Background = Brush.Parse("#1D242B"),
                BorderBrush = Brush.Parse("#34404A"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Child = row
            };

            ServicesPanel.Children.Add(wrapper);
            serviceRows[process.Definition.Kind] = new ServiceRowControls(state, pid, start, stop);
            UpdateServiceRow(process);
        }
    }

    private void BuildLogTabs()
    {
        foreach (AetherXivServiceProcess process in supervisor.Processes)
        {
            TextBox logBox = new()
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Menlo,Consolas,monospace"),
                Text = ""
            };

            ServiceLogTabs.Items.Add(new TabItem
            {
                Header = process.Definition.DisplayName,
                Content = logBox
            });
            logBoxes[process.Definition.Kind] = logBox;
        }
    }

    private async Task StartServiceAsync(AetherXivManagedService service)
    {
        if (operationInProgress)
            return;

        if (!TrySaveConfig(reloadSupervisor: true, requireReloadForCurrentConfig: true))
            return;

        SetBusy(true, $"Starting {service}");
        try
        {
            if (!await EnsureStartupPreflightAsync())
                return;

            await supervisor.StartAsync(service);
            RefreshHeader();
        }
        catch (Exception ex)
        {
            HeaderStatusText.Text = $"{service} start failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task StopServiceAsync(AetherXivManagedService service)
    {
        if (operationInProgress)
            return;

        SetBusy(true, $"Stopping {service}");
        try
        {
            await supervisor.StopAsync(service);
            RefreshHeader();
        }
        catch (Exception ex)
        {
            HeaderStatusText.Text = $"{service} stop failed: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Supervisor_LogReceived(object? sender, AetherXivServiceLogEventArgs e)
    {
        string prefix = e.IsError ? "ERR" : "OUT";
        liveLogBuffer.Enqueue(e.Service, FormatServiceLogEntry(prefix, e.Line));
    }

    private void AppendServiceLog(
        AetherXivManagedService service,
        string prefix,
        string line,
        bool isError)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => AppendServiceLog(service, prefix, line, isError));
            return;
        }

        if (!logBoxes.TryGetValue(service, out TextBox? logBox))
            return;

        string entry = FormatServiceLogEntry(prefix, line);
        logBox.Text = TrimLog(String.Concat(logBox.Text, entry, Environment.NewLine));
        logBox.CaretIndex = logBox.Text?.Length ?? 0;
    }

    private void FlushLiveServiceLogs()
    {
        AetherXivLiveLogBatch batch = liveLogBuffer.Drain(maxEntries: 2_000);
        if (batch.IsEmpty)
            return;

        Dictionary<AetherXivManagedService, StringBuilder> updates = new();
        foreach ((AetherXivManagedService service, int dropped) in batch.DroppedByService)
        {
            GetServiceLogUpdate(updates, service).AppendLine(
                $"[{DateTime.Now:HH:mm:ss}] UI {dropped:N0} live log line(s) omitted from the on-screen preview; disk traces remain complete.");
        }

        foreach (AetherXivLiveLogEntry entry in batch.Entries)
            GetServiceLogUpdate(updates, entry.Service).AppendLine(entry.Text);

        foreach ((AetherXivManagedService service, StringBuilder update) in updates)
        {
            if (!logBoxes.TryGetValue(service, out TextBox? logBox))
                continue;

            logBox.Text = TrimLog(String.Concat(logBox.Text, update.ToString()));
            logBox.CaretIndex = logBox.Text?.Length ?? 0;
        }
    }

    private static StringBuilder GetServiceLogUpdate(
        IDictionary<AetherXivManagedService, StringBuilder> updates,
        AetherXivManagedService service)
    {
        if (!updates.TryGetValue(service, out StringBuilder? update))
        {
            update = new StringBuilder();
            updates.Add(service, update);
        }

        return update;
    }

    private static string FormatServiceLogEntry(string prefix, string line)
    {
        string normalized = line
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        return $"[{DateTime.Now:HH:mm:ss}] {prefix} {normalized}";
    }

    private void Supervisor_StateChanged(object? sender, AetherXivServiceStateChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AetherXivServiceProcess process = supervisor.Find(e.Service);
            UpdateServiceRow(process);
            RefreshHeader();
        });
    }

    private void UpdateServiceRow(AetherXivServiceProcess process)
    {
        if (!serviceRows.TryGetValue(process.Definition.Kind, out ServiceRowControls? row))
            return;

        row.State.Text = process.State.ToString();
        row.ProcessId.Text = process.ProcessId?.ToString() ?? "-";
        row.StartButton.IsEnabled = !operationInProgress && !process.IsRunning;
        row.StopButton.IsEnabled = !operationInProgress && process.IsRunning;
    }

    private void SetBusy(bool busy, string? message = null)
    {
        operationInProgress = busy;
        StartStackButton.IsEnabled = !busy;
        StopStackButton.IsEnabled = !busy;
        VerifyDependenciesButton.IsEnabled = !busy;
        if (!String.IsNullOrWhiteSpace(message))
            HeaderStatusText.Text = message;

        foreach (AetherXivServiceProcess process in supervisor.Processes)
            UpdateServiceRow(process);
    }

    private void RefreshHeader()
    {
        int running = supervisor.Processes.Count(process => process.IsRunning);
        HeaderStatusText.Text = running == 0
            ? "Services stopped"
            : $"{running}/{supervisor.Processes.Count} service(s) running";
    }

    private void ApplyConfigToFields(AetherXivOperatorConfig source)
    {
        AetherXivOperatorConfig normalized = source.Normalize();
        WorkspaceRootBox.Text = normalized.WorkspaceRoot;
        DotnetPathBox.Text = normalized.DotnetPath;
        DataRootBox.Text = normalized.DataRoot;
        ScriptsRootBox.Text = normalized.ScriptsRoot;
        DiagnosticsDirectoryBox.Text = normalized.DiagnosticsDirectory;
        TraceEnabledBox.IsChecked = normalized.TraceEnabled;
        DevLoggingEnabledBox.IsChecked = normalized.DevLogging.Enabled;
        AutoRepairDatabaseBox.IsChecked = normalized.AutoRepairDatabase;
        DevLogLevelBox.SelectedIndex = normalized.DevLogging.Level switch
        {
            AetherXivDevLogLevel.Off => 0,
            AetherXivDevLogLevel.Verbose => 2,
            _ => 1
        };
        NetworkTraceBox.IsChecked = normalized.DevLogging.NetworkTrace;
        ServerTraceBox.IsChecked = normalized.DevLogging.ServerTrace;

        DatabaseHostBox.Text = normalized.Database.Host;
        DatabasePortBox.Text = normalized.Database.Port.ToString();
        DatabaseNameBox.Text = normalized.Database.Name;
        DatabaseUserBox.Text = normalized.Database.User;
        DatabasePasswordBox.Text = normalized.Database.Password;

        MapBindBox.Text = normalized.Map.Bind;
        MapAdvertiseBox.Text = normalized.Map.Advertise;
        WorldBindBox.Text = normalized.World.Bind;
        WorldAdvertiseBox.Text = normalized.World.Advertise;
        LobbyBindBox.Text = normalized.Lobby.Bind;
        LobbyAdvertiseBox.Text = normalized.Lobby.Advertise;
        LauncherBindBox.Text = normalized.LauncherServices.Bind;
        LauncherAdvertiseBox.Text = normalized.LauncherServices.Advertise;
        WorldMapRouteBox.Text = normalized.WorldMapRoute;
        WorldMapRouteZoneBox.Text = normalized.WorldMapRouteZone.ToString();
    }

    private AetherXivOperatorConfig ReadConfigFromFields()
    {
        return new AetherXivOperatorConfig(
            WorkspaceRootBox.Text ?? "",
            DotnetPathBox.Text ?? "",
            DataRootBox.Text ?? "",
            DiagnosticsDirectoryBox.Text ?? "",
            ScriptsRootBox.Text ?? "",
            TraceEnabledBox.IsChecked == true,
            new AetherXivDevLoggingConfig(
                DevLoggingEnabledBox.IsChecked == true,
                ReadDevLogLevel(),
                NetworkTraceBox.IsChecked == true,
                ServerTraceBox.IsChecked == true),
            new AetherXivDatabaseConfig(
                DatabaseHostBox.Text ?? "",
                ParseUShort(DatabasePortBox.Text, "database port"),
                DatabaseNameBox.Text ?? "",
                DatabaseUserBox.Text ?? "",
                DatabasePasswordBox.Text ?? ""),
            new AetherXivEndpointConfig(LauncherBindBox.Text ?? "", LauncherAdvertiseBox.Text ?? ""),
            new AetherXivEndpointConfig(MapBindBox.Text ?? "", MapAdvertiseBox.Text ?? ""),
            new AetherXivEndpointConfig(WorldBindBox.Text ?? "", WorldAdvertiseBox.Text ?? ""),
            new AetherXivEndpointConfig(LobbyBindBox.Text ?? "", LobbyAdvertiseBox.Text ?? ""),
            WorldMapRouteBox.Text ?? "",
            ParseUInt(WorldMapRouteZoneBox.Text, "map route zone"),
            true,
            AutoRepairDatabaseBox.IsChecked == true);
    }

    private async Task<bool> EnsureStartupPreflightAsync()
    {
        mirrorPreflightToServiceLogs = true;
        try
        {
            AetherXivDependencyCheckResult dependencyResult = RunDependencyPreflight(clearStatus: true);
            if (!dependencyResult.CanStartServices)
            {
                HeaderStatusText.Text = "Dependency preflight blocked startup";
                return false;
            }

            return await EnsureDatabaseReadyAsync(clearStatus: false).ConfigureAwait(true);
        }
        finally
        {
            mirrorPreflightToServiceLogs = false;
        }
    }

    private async Task<bool> EnsureDatabaseReadyAsync(bool clearStatus)
    {
        AetherXivDatabasePreflightResult result = await RunDatabasePreflightAsync(
            config.AutoRepairDatabase,
            adminCredentials: null,
            clearStatus).ConfigureAwait(true);

        if (result.NeedsAdminCredentials)
        {
            AetherXivMariaDbAdminCredentials? adminCredentials = await RequestMariaDbAdminCredentialsAsync().ConfigureAwait(true);
            if (adminCredentials is null)
            {
                AppendPreflightStatus("[Blocked] database.bootstrap: MariaDB setup was cancelled.");
                HeaderStatusText.Text = "Database setup cancelled";
                return false;
            }

            AetherXivDatabaseInstallResult setup = await databaseInstaller.SetupAsync(config, adminCredentials).ConfigureAwait(true);
            AppendDatabaseInstallerResult("database.setup", setup);
            if (!setup.Succeeded)
            {
                HeaderStatusText.Text = "Database setup failed";
                return false;
            }
            result = await RunDatabasePreflightAsync(repair: true, adminCredentials: null, clearStatus: false).ConfigureAwait(true);
        }

        if (result.RequiresInPlaceMigration)
        {
            AppendPreflightStatus("[Updating] database.migrations: Backing up and applying pending migrations with the configured database account.");
            AetherXivDatabaseInstallResult update = await databaseInstaller.ApplyPendingMigrationsAsync(config).ConfigureAwait(true);
            AppendDatabaseInstallerResult("database.update", update);
            if (!update.Succeeded)
            {
                AppendPreflightStatus(
                    "[NeedsRepair] database.update: The existing database could not be migrated and verified in place. "
                    + "Its backup was retained; a fresh canonical database install is required before startup can continue.");
                result = new AetherXivDatabasePreflightResult(
                [
                    new AetherXivDatabasePreflightStep(
                        "database.update",
                        AetherXivDatabasePreflightStatus.NeedsRepair,
                        "In-place migration failed; offer a backed-up canonical rebuild.")
                ]);
            }
            else
            {
                result = await RunDatabasePreflightAsync(repair: true, adminCredentials: null, clearStatus: false).ConfigureAwait(true);
            }
        }

        if (result.RequiresCanonicalRepair)
        {
            if (!await ConfirmCanonicalDatabaseRepairAsync().ConfigureAwait(true))
            {
                AppendPreflightStatus("[Blocked] database.repair: Canonical database repair was cancelled.");
                HeaderStatusText.Text = "Database repair cancelled";
                return false;
            }

            AetherXivMariaDbAdminCredentials? adminCredentials = await RequestMariaDbAdminCredentialsAsync("Repair Database").ConfigureAwait(true);
            if (adminCredentials is null)
            {
                AppendPreflightStatus("[Blocked] database.repair: MariaDB credentials were not provided.");
                HeaderStatusText.Text = "Database repair cancelled";
                return false;
            }

            AetherXivDatabaseInstallResult repair = await databaseInstaller.RebuildCanonicalAsync(config, adminCredentials).ConfigureAwait(true);
            AppendDatabaseInstallerResult("database.repair", repair);
            if (!repair.Succeeded)
            {
                HeaderStatusText.Text = "Database repair failed; recovery backup retained";
                return false;
            }
            result = await RunDatabasePreflightAsync(repair: true, adminCredentials: null, clearStatus: false).ConfigureAwait(true);
        }

        if (result.CanStartServices)
            return true;

        HeaderStatusText.Text = "Database preflight blocked startup";
        return false;
    }

    private async Task<AetherXivDatabasePreflightResult> RunDatabasePreflightAsync(
        bool repair,
        AetherXivMariaDbAdminCredentials? adminCredentials,
        bool clearStatus)
    {
        if (clearStatus)
            ResetPreflightStatus("Database preflight");
        else
            AppendPreflightStatus("Database preflight");

        Progress<AetherXivDatabasePreflightStep> progress = new(step =>
        {
            AppendPreflightStatus(FormatPreflightStep(step));
        });

        HeaderStatusText.Text = repair ? "Checking and repairing database" : "Checking database";
        AetherXivDatabasePreflightResult result = await databasePreflight.RunAsync(
            config,
            repair,
            adminCredentials,
            progress);
        HeaderStatusText.Text = result.CanStartServices
            ? "Database ready"
            : "Database requires attention";
        return result;
    }

    private static string FormatPreflightStep(AetherXivDatabasePreflightStep step) =>
        $"[{step.Status}] {step.Name}: {step.Message}";

    private AetherXivDependencyCheckResult RunDependencyPreflight(bool clearStatus)
    {
        if (clearStatus)
            ResetPreflightStatus("Dependency preflight");
        else
            AppendPreflightStatus("Dependency preflight");

        AetherXivDependencyCheckResult result = dependencyPreflight.Run(config);
        foreach (AetherXivDependencyCheckStep step in result.Steps)
            AppendPreflightStatus(FormatDependencyStep(step));

        HeaderStatusText.Text = result.CanStartServices
            ? "Dependencies ready"
            : "Dependencies require attention";
        return result;
    }

    private void ResetPreflightStatus(string header)
    {
        PreflightStatusBox.Text = $"{header}{Environment.NewLine}";
        PreflightStatusBox.CaretIndex = PreflightStatusBox.Text.Length;
        MirrorPreflightStatusToServiceLogs(header);
    }

    private void AppendPreflightStatus(string line)
    {
        PreflightStatusBox.Text += line + Environment.NewLine;
        PreflightStatusBox.CaretIndex = PreflightStatusBox.Text.Length;
        MirrorPreflightStatusToServiceLogs(line);
    }

    private void AppendDatabaseInstallerResult(string name, AetherXivDatabaseInstallResult result)
    {
        AppendPreflightStatus($"[{(result.Succeeded ? "Repaired" : "Blocked")}] {name}: installer exit={result.ExitCode} package={result.PackageDirectory}");
        foreach (string line in result.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            AppendPreflightStatus($"[Installer] {line}");
    }

    private void MirrorPreflightStatusToServiceLogs(string line)
    {
        if (!mirrorPreflightToServiceLogs)
            return;

        foreach (AetherXivServiceProcess process in supervisor.Processes)
            AppendServiceLog(process.Definition.Kind, "PRE", line, isError: false);
    }

    private static string FormatDependencyStep(AetherXivDependencyCheckStep step) =>
        $"[{step.Status}] {step.Name}: {step.Message}";

    private async Task<AetherXivMariaDbAdminCredentials?> RequestMariaDbAdminCredentialsAsync(string actionLabel = "Setup Database")
    {
        TextBox userBox = new()
        {
            Text = "root",
            MinWidth = 260
        };
        TextBox passwordBox = new()
        {
            PasswordChar = '*',
            MinWidth = 260
        };
        TextBlock errorText = new()
        {
            Text = "",
            Foreground = Brush.Parse("#FF9B9B")
        };
        Button cancelButton = new()
        {
            Content = "Cancel",
            MinWidth = 88
        };
        Button setupButton = new()
        {
            Content = actionLabel,
            MinWidth = 128
        };
        setupButton.Classes.Add("primary");

        Window dialog = new()
        {
            Title = "MariaDB Setup",
            Width = 460,
            Height = 300,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush.Parse("#101418"),
            Foreground = Brush.Parse("#E8EDF2")
        };

        setupButton.Click += (_, _) =>
        {
            string user = userBox.Text?.Trim() ?? "";
            if (String.IsNullOrWhiteSpace(user))
            {
                errorText.Text = "Admin user is required.";
                return;
            }

            dialog.Close(new AetherXivMariaDbAdminCredentials(user, passwordBox.Text ?? ""));
        };
        cancelButton.Click += (_, _) => dialog.Close(null);

        TextBlock passwordLabel = new()
        {
            Text = "Password",
            VerticalAlignment = VerticalAlignment.Center
        };
        dialog.Content = new Border
        {
            Padding = new Thickness(18),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "MariaDB admin credentials are needed once to create or repair the configured AetherXIV database and app user.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("120,*"),
                        RowDefinitions = new RowDefinitions("Auto,Auto"),
                        RowSpacing = 10,
                        ColumnSpacing = 10,
                        Children =
                        {
                            new TextBlock { Text = "Admin user", VerticalAlignment = VerticalAlignment.Center },
                            WithGridPosition(userBox, row: 0, column: 1),
                            WithGridPosition(passwordLabel, row: 1, column: 0),
                            WithGridPosition(passwordBox, row: 1, column: 1)
                        }
                    },
                    errorText,
                    new TextBlock
                    {
                        Text = "These admin credentials are not saved. The servers keep using the database account configured in this tab.",
                        Foreground = Brush.Parse("#AAB5C0"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children =
                        {
                            cancelButton,
                            setupButton
                        }
                    }
                }
            }
        };

        return await dialog.ShowDialog<AetherXivMariaDbAdminCredentials?>(this).ConfigureAwait(true);
    }

    private async Task<bool> ConfirmCanonicalDatabaseRepairAsync()
    {
        Button cancelButton = new() { Content = "Cancel", MinWidth = 88 };
        Button migrateButton = new() { Content = "Back Up and Install Clean", MinWidth = 190 };
        migrateButton.Classes.Add("primary");
        Window dialog = new()
        {
            Title = "Database Repair",
            Width = 560,
            Height = 420,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brush.Parse("#101418"),
            Foreground = Brush.Parse("#E8EDF2")
        };
        cancelButton.Click += (_, _) => dialog.Close(false);
        migrateButton.Click += (_, _) => dialog.Close(true);
        dialog.Content = new Border
        {
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    new TextBlock
                    {
                        Text = "This database needs to be initialized or repaired for AetherXIV 2.",
                        FontSize = 18,
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "AetherXIV will make a complete verified backup and install the canonical database. If compatible account and character tables are present, it will also try to restore accounts, characters, and character-owned tables. If those rows do not fit the AetherXIV 2 schema, setup keeps the clean database and retains the recovery files.",
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = "Stop all AetherXIV services before continuing. The untouched backup is kept outside the release folder and is restored automatically only if the clean database itself cannot be installed.",
                        Foreground = Brush.Parse("#F3C969"),
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancelButton, migrateButton }
                    }
                }
            }
        };
        return await dialog.ShowDialog<bool>(this).ConfigureAwait(true);
    }

    private static T WithGridPosition<T>(T control, int row, int column)
        where T : Control
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        return control;
    }

    private AetherXivDevLogLevel ReadDevLogLevel()
    {
        return DevLogLevelBox.SelectedIndex switch
        {
            0 => AetherXivDevLogLevel.Off,
            2 => AetherXivDevLogLevel.Verbose,
            _ => AetherXivDevLogLevel.Basic
        };
    }

    private static ushort ParseUShort(string? value, string name)
    {
        if (!UInt16.TryParse(value, out ushort parsed))
            throw new FormatException($"{name} must be a valid port.");

        return parsed;
    }

    private static uint ParseUInt(string? value, string name)
    {
        if (!UInt32.TryParse(value, out uint parsed))
            throw new FormatException($"{name} must be a valid number.");

        return parsed;
    }

    private static string TrimLog(string value)
    {
        const int maxLength = 200_000;
        if (value.Length <= maxLength)
            return value;

        return value[^maxLength..];
    }

    private sealed record ServiceRowControls(
        TextBlock State,
        TextBlock ProcessId,
        Button StartButton,
        Button StopButton);
}
