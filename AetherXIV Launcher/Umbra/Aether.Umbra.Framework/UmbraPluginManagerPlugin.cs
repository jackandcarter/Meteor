using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

internal sealed class UmbraPluginManagerPlugin(UmbraRuntime runtime) : IUmbraPlugin
{
    private const float TwoPaneGap = 12.0f;
    private string search = "";
    private string? selectedInstalledPluginId;
    private string? selectedStoreEntryKey;
    private string? pendingUninstallId;
    private string? resultMessage;
    private bool resultSucceeded;
    private string customRepositoryUrl = "";
    private Task<UmbraPluginActionResult>? pendingAction;

    public string Name => "Umbra Plugin Manager";

    public void Initialize(IUmbraPluginContext context)
    {
    }

    public void Update(TimeSpan delta)
    {
    }

    public void Draw(IUmbraDrawContext drawContext)
    {
        PollPendingAction();
        if (!runtime.PluginManager.IsOpen)
            return;

        drawContext.SetNextWindowSize(1180.0f, 720.0f);
        bool open = true;
        bool visible = drawContext.BeginWindow("Umbra Plugin Library###UmbraPluginManager", ref open);
        try
        {
            if (!open)
            {
                runtime.SetPluginManagerOpen(false);
                UmbraNativeUi.SetPluginManagerOpen(false);
            }

            if (!visible || !open)
                return;

            drawContext.Icon(UmbraIcon.Umbra, UmbraTextTone.Accent, 38.0f);
            drawContext.SameLine();
            drawContext.Text("Umbra", UmbraTextTone.Normal, UmbraTextStyle.Title);
            drawContext.SameLine();
            drawContext.Badge($"API {UmbraFrameworkInfo.ApiVersion}", UmbraTextTone.Accent, UmbraIcon.Shield);
            drawContext.Separator();

            bool sidebarVisible = drawContext.BeginPanel(
                "##UmbraManagerSidebar",
                200.0f,
                600.0f,
                UmbraPanelStyle.Sidebar);
            try
            {
                if (sidebarVisible)
                    DrawNavigation(drawContext);
            }
            finally
            {
                drawContext.EndChild();
            }

            drawContext.SameLine();
            bool contentVisible = drawContext.BeginPanel(
                "##UmbraManagerContent",
                0.0f,
                600.0f,
                UmbraPanelStyle.Default);
            try
            {
                if (!contentVisible)
                    return;

                DrawActiveSection(drawContext);
                if (!string.IsNullOrWhiteSpace(resultMessage))
                {
                    drawContext.Spacing(4.0f);
                    drawContext.Icon(
                        resultSucceeded ? UmbraIcon.Check : UmbraIcon.Warning,
                        resultSucceeded ? UmbraTextTone.Success : UmbraTextTone.Error,
                        18.0f);
                    drawContext.SameLine();
                    drawContext.Text(
                        resultMessage,
                        resultSucceeded ? UmbraTextTone.Success : UmbraTextTone.Error,
                        UmbraTextStyle.Caption);
                }
            }
            finally
            {
                drawContext.EndChild();
            }
        }
        finally
        {
            drawContext.EndWindow();
        }
    }

    public void Dispose()
    {
    }

    private void DrawNavigation(IUmbraDrawContext drawContext)
    {
        drawContext.Text("PLUGIN LIBRARY", UmbraTextTone.Muted, UmbraTextStyle.Caption);
        drawContext.Spacing(3.0f);
        DrawNavigationButton(drawContext, "Discover", UmbraPluginManagerTab.Supported, UmbraIcon.Discover);
        DrawNavigationButton(drawContext, "Installed", UmbraPluginManagerTab.Installed, UmbraIcon.Installed);
        DrawNavigationButton(drawContext, "Updates", UmbraPluginManagerTab.Updates, UmbraIcon.Updates);
        DrawNavigationButton(drawContext, "Repositories", UmbraPluginManagerTab.Available, UmbraIcon.Repository);
        drawContext.Spacing(10.0f);
        drawContext.Separator();
        drawContext.Spacing(5.0f);
        DrawNavigationButton(drawContext, "Settings", UmbraPluginManagerTab.Settings, UmbraIcon.Settings);
        DrawNavigationButton(drawContext, "About", UmbraPluginManagerTab.Logs, UmbraIcon.Info);
        drawContext.Spacing(22.0f);
        drawContext.Badge(
            runtime.PluginManager.SafeMode ? "Safe mode" : "Framework verified",
            runtime.PluginManager.SafeMode ? UmbraTextTone.Warning : UmbraTextTone.Success,
            UmbraIcon.Shield);
        drawContext.Text($"Umbra API {UmbraFrameworkInfo.ApiVersion}", UmbraTextTone.Muted, UmbraTextStyle.Caption);
    }

    private void DrawNavigationButton(
        IUmbraDrawContext drawContext,
        string label,
        UmbraPluginManagerTab tab,
        UmbraIcon icon)
    {
        bool active = runtime.PluginManager.ActiveTab == tab
            || (tab == UmbraPluginManagerTab.Supported
                && runtime.PluginManager.ActiveTab == UmbraPluginManagerTab.Updates);
        if (drawContext.Button(
            $"{label}###{tab}",
            active ? UmbraButtonStyle.Primary : UmbraButtonStyle.Navigation,
            icon,
            164.0f,
            42.0f))
        {
            runtime.SetPluginManagerTab(tab);
            pendingUninstallId = null;
            resultMessage = null;
        }
    }

    private void DrawActiveSection(IUmbraDrawContext drawContext)
    {
        switch (runtime.PluginManager.ActiveTab)
        {
            case UmbraPluginManagerTab.Installed:
                DrawInstalled(drawContext);
                break;
            case UmbraPluginManagerTab.Supported:
                DrawInstaller(
                    drawContext,
                    runtime.PluginManager.SupportedPlugins.Concat(runtime.PluginManager.AvailablePlugins).ToArray(),
                    "Discover Plugins");
                break;
            case UmbraPluginManagerTab.Available:
                DrawRepositories(drawContext);
                break;
            case UmbraPluginManagerTab.Updates:
                DrawInstaller(drawContext, runtime.PluginManager.Updates, "Plugin Updates");
                break;
            case UmbraPluginManagerTab.Settings:
            case UmbraPluginManagerTab.Logs:
                DrawSettings(drawContext);
                break;
        }
    }

    private void DrawInstalled(IUmbraDrawContext drawContext)
    {
        drawContext.Text("Installed Plugins", UmbraTextTone.Normal, UmbraTextStyle.Heading);
        drawContext.Text("Manage plugin state, performance and permissions.", UmbraTextTone.Muted, UmbraTextStyle.Caption);
        drawContext.InputText("##UmbraInstalledSearch", ref search, "Search installed plugins", 256);
        drawContext.Text(
            $"{runtime.PluginManager.InstalledPlugins.Count} installed · " +
            $"{runtime.PluginManager.RuntimePlugins.Count(status => status.State == UmbraPluginRuntimeState.Running)} running",
            UmbraTextTone.Muted);

        UmbraPluginManifest[] plugins = runtime.PluginManager.InstalledPlugins
            .Where(MatchesSearch)
            .ToArray();
        if (plugins.Length > 0
            && !plugins.Any(plugin => string.Equals(plugin.Id, selectedInstalledPluginId, StringComparison.OrdinalIgnoreCase)))
        {
            selectedInstalledPluginId = plugins[0].Id;
        }

        (float listWidth, float detailWidth) = CalculateTwoPaneWidths(drawContext.AvailableContentWidth);
        const float listHeight = 444.0f;

        bool listVisible = drawContext.BeginPanel(
            "##UmbraInstalledList",
            listWidth,
            listHeight,
            UmbraPanelStyle.Default);
        try
        {
            if (listVisible && plugins.Length == 0)
            {
                drawContext.Text(
                    runtime.PluginManager.InstalledPlugins.Count == 0
                        ? "No plugin manifests are installed."
                        : "No installed plugins match the search.",
                    UmbraTextTone.Muted);
            }
            else if (listVisible)
            {
                foreach (UmbraPluginManifest manifest in plugins)
                {
                    UmbraPluginRuntimeStatus? status = runtime.PluginManager.RuntimePlugins.FirstOrDefault(
                        candidate => string.Equals(candidate.PluginId, manifest.Id, StringComparison.OrdinalIgnoreCase));
                    DrawInstalledRow(drawContext, manifest, status);
                    drawContext.Spacing(6.0f);
                }
            }
        }
        finally
        {
            drawContext.EndChild();
        }

        drawContext.SameLine();

        UmbraPluginManifest? selectedManifest = plugins.FirstOrDefault(
            plugin => string.Equals(plugin.Id, selectedInstalledPluginId, StringComparison.OrdinalIgnoreCase));
        UmbraPluginRuntimeStatus? selectedStatus = selectedManifest is null
            ? null
            : runtime.PluginManager.RuntimePlugins.FirstOrDefault(
                candidate => string.Equals(candidate.PluginId, selectedManifest.Id, StringComparison.OrdinalIgnoreCase));
        bool detailsVisible = drawContext.BeginPanel(
            "##UmbraInstalledDetails",
            detailWidth,
            listHeight,
            UmbraPanelStyle.Detail);
        try
        {
            if (detailsVisible)
                DrawInstalledDetails(drawContext, selectedManifest, selectedStatus);
        }
        finally
        {
            drawContext.EndChild();
        }
    }

    private void DrawInstalledRow(
        IUmbraDrawContext drawContext,
        UmbraPluginManifest manifest,
        UmbraPluginRuntimeStatus? status)
    {
        bool selected = string.Equals(selectedInstalledPluginId, manifest.Id, StringComparison.OrdinalIgnoreCase);
        bool cardVisible = drawContext.BeginPanel(
            $"##installed-card-{manifest.Id}",
            0.0f,
            138.0f,
            selected ? UmbraPanelStyle.Selected : UmbraPanelStyle.Card);
        try
        {
            if (!cardVisible)
                return;

            drawContext.Artwork(manifest.Id, UmbraIcon.Plug, 78.0f);
            drawContext.SameLine();
            bool bodyVisible = drawContext.BeginPanel(
                $"##installed-body-{manifest.Id}",
                0.0f,
                0.0f,
                UmbraPanelStyle.Default);
            try
            {
                if (!bodyVisible)
                    return;

                drawContext.Text(manifest.Name, UmbraTextTone.Normal, UmbraTextStyle.Heading);
                drawContext.SameLine();
                drawContext.Badge($"v{manifest.Version}", UmbraTextTone.Accent);

                string stateText = status?.State.ToString() ?? (manifest.Enabled ? "Not loaded" : "Disabled");
                UmbraTextTone stateTone = status?.State switch
                {
                    UmbraPluginRuntimeState.Running => UmbraTextTone.Success,
                    UmbraPluginRuntimeState.Faulted => UmbraTextTone.Error,
                    _ => UmbraTextTone.Muted
                };
                drawContext.Badge(
                    stateText,
                    stateTone,
                    status?.State == UmbraPluginRuntimeState.Running ? UmbraIcon.Check : UmbraIcon.Power);
                drawContext.SameLine();
                drawContext.Badge($"API {manifest.ApiVersion}", UmbraTextTone.Accent, UmbraIcon.Shield);

                if (drawContext.Button(
                    $"{(selected ? "Selected" : "Details")}###{manifest.Id}",
                    selected ? UmbraButtonStyle.Primary : UmbraButtonStyle.Ghost,
                    selected ? UmbraIcon.Check : UmbraIcon.Info))
                {
                    selectedInstalledPluginId = manifest.Id;
                }
            }
            finally
            {
                drawContext.EndChild();
            }
        }
        finally
        {
            drawContext.EndChild();
        }
    }

    private void DrawInstalledDetails(
        IUmbraDrawContext drawContext,
        UmbraPluginManifest? manifest,
        UmbraPluginRuntimeStatus? status)
    {
        if (manifest is null)
        {
            drawContext.Icon(UmbraIcon.Info, UmbraTextTone.Muted, 30.0f);
            drawContext.Text("Select an installed plugin to inspect its details.", UmbraTextTone.Muted);
            return;
        }

        drawContext.Artwork(manifest.Id, UmbraIcon.Plug, 84.0f);
        drawContext.SameLine();
        drawContext.Text(manifest.Name, UmbraTextTone.Normal, UmbraTextStyle.Heading);
        drawContext.Badge($"v{manifest.Version}", UmbraTextTone.Accent);
        drawContext.SameLine();
        drawContext.Badge($"API {manifest.ApiVersion}", UmbraTextTone.Accent, UmbraIcon.Shield);
        drawContext.Spacing(5.0f);
        drawContext.Separator();

        string stateText = status?.State.ToString() ?? (manifest.Enabled ? "Not loaded" : "Disabled");
        UmbraTextTone stateTone = status?.State switch
        {
            UmbraPluginRuntimeState.Running => UmbraTextTone.Success,
            UmbraPluginRuntimeState.Faulted => UmbraTextTone.Error,
            _ => UmbraTextTone.Muted
        };
        drawContext.Badge(stateText, stateTone, status?.State == UmbraPluginRuntimeState.Running ? UmbraIcon.Check : UmbraIcon.Power);
        drawContext.Text($"Entry: {manifest.Entry}", UmbraTextTone.Muted, UmbraTextStyle.Caption);
        drawContext.Text($"Minimum framework: {manifest.MinimumFrameworkVersion}", UmbraTextTone.Muted, UmbraTextStyle.Caption);
        if (!string.IsNullOrWhiteSpace(manifest.InstalledFromSource))
            drawContext.Text($"Source: {manifest.InstalledFromSource}", UmbraTextTone.Muted, UmbraTextStyle.Caption);
        drawContext.Spacing(4.0f);
        drawContext.Text("Permissions", UmbraTextTone.Normal, UmbraTextStyle.Heading);
        drawContext.Text(
            manifest.Capabilities.Count == 0
                ? "Capabilities: none declared"
                : $"Capabilities: {string.Join(", ", manifest.Capabilities)}",
            UmbraTextTone.Muted,
            UmbraTextStyle.Caption);

        if (status is not null)
        {
            drawContext.Spacing(4.0f);
            drawContext.Text("Performance", UmbraTextTone.Normal, UmbraTextStyle.Heading);
            drawContext.Text(
                $"Update {status.LastUpdateDuration.TotalMilliseconds:F3} ms · " +
                $"Draw {status.LastDrawDuration.TotalMilliseconds:F3} ms · " +
                $"Peak {status.PeakDrawDuration.TotalMilliseconds:F3} ms · " +
                $"Over budget {status.SlowDrawCount}",
                status.SlowDrawCount > 0 ? UmbraTextTone.Warning : UmbraTextTone.Muted,
                UmbraTextStyle.Caption);
            if (!string.IsNullOrWhiteSpace(status.LastError))
                drawContext.Text($"Last error: {status.LastError}", UmbraTextTone.Error);
        }

        drawContext.Spacing(8.0f);
        drawContext.Separator();
        if (!runtime.Options.SafeMode)
        {
            string toggleLabel = manifest.Enabled ? "Disable" : "Enable";
            if (drawContext.Button(
                $"{toggleLabel}###detail-toggle-{manifest.Id}",
                manifest.Enabled ? UmbraButtonStyle.Default : UmbraButtonStyle.Primary,
                UmbraIcon.Power,
                112.0f,
                36.0f))
            {
                Report(runtime.SetPluginEnabled(manifest.Id, !manifest.Enabled));
                pendingUninstallId = null;
            }

            if (manifest.Enabled)
            {
                drawContext.SameLine();
                if (drawContext.Button(
                    $"Reload###detail-reload-{manifest.Id}",
                    UmbraButtonStyle.Ghost,
                    UmbraIcon.Refresh,
                    112.0f,
                    36.0f))
                {
                    Report(runtime.ReloadPlugin(manifest.Id));
                }
            }
        }

        drawContext.Spacing(5.0f);
        if (pendingUninstallId == manifest.Id)
        {
            if (drawContext.Button(
                $"Confirm uninstall###detail-confirm-{manifest.Id}",
                UmbraButtonStyle.Danger,
                UmbraIcon.Trash,
                166.0f,
                36.0f))
            {
                Report(runtime.UninstallPlugin(manifest.Id));
                pendingUninstallId = null;
                selectedInstalledPluginId = null;
                return;
            }
            drawContext.SameLine();
            if (drawContext.Button(
                $"Cancel###detail-cancel-{manifest.Id}",
                UmbraButtonStyle.Ghost,
                UmbraIcon.None,
                88.0f,
                36.0f))
            {
                pendingUninstallId = null;
            }
        }
        else if (drawContext.Button(
            $"Uninstall###detail-uninstall-{manifest.Id}",
            UmbraButtonStyle.Ghost,
            UmbraIcon.Trash,
            124.0f,
            36.0f))
        {
            pendingUninstallId = manifest.Id;
            resultMessage = "Confirm uninstall to archive this plugin in Cache/PluginTrash.";
            resultSucceeded = false;
        }
    }

    private void DrawInstaller(
        IUmbraDrawContext drawContext,
        IReadOnlyList<UmbraStoreEntry> entries,
        string heading)
    {
        if (drawContext.Button(
            "Discover###installer-supported",
            runtime.PluginManager.ActiveTab == UmbraPluginManagerTab.Supported
                ? UmbraButtonStyle.Primary
                : UmbraButtonStyle.Navigation,
            UmbraIcon.Discover))
            runtime.SetPluginManagerTab(UmbraPluginManagerTab.Supported);
        drawContext.SameLine();
        if (drawContext.Button(
            $"Updates ({runtime.PluginManager.Updates.Count})###installer-updates",
            runtime.PluginManager.ActiveTab == UmbraPluginManagerTab.Updates
                ? UmbraButtonStyle.Primary
                : UmbraButtonStyle.Navigation,
            UmbraIcon.Updates))
            runtime.SetPluginManagerTab(UmbraPluginManagerTab.Updates);
        drawContext.Spacing(4.0f);
        DrawStoreEntries(drawContext, entries, heading);
    }

    private void DrawStoreEntries(
        IUmbraDrawContext drawContext,
        IReadOnlyList<UmbraStoreEntry> entries,
        string heading)
    {
        drawContext.Text(heading, UmbraTextTone.Normal, UmbraTextStyle.Heading);
        drawContext.Text(
            runtime.PluginManager.ActiveTab == UmbraPluginManagerTab.Available
                ? "Browse plugins from your configured custom repositories."
                : "Explore plugins compatible with Umbra API 2.0.",
            UmbraTextTone.Muted,
            UmbraTextStyle.Caption);
        drawContext.InputText($"##UmbraStoreSearch-{runtime.PluginManager.ActiveTab}", ref search, "Search plugins", 256);
        drawContext.Badge($"{entries.Count} plugins", UmbraTextTone.Accent, UmbraIcon.Grid);

        UmbraStoreEntry[] filtered = entries.Where(MatchesSearch).ToArray();
        if (filtered.Length > 0
            && !filtered.Any(entry => string.Equals(StoreEntryKey(entry), selectedStoreEntryKey, StringComparison.OrdinalIgnoreCase)))
        {
            selectedStoreEntryKey = StoreEntryKey(filtered[0]);
        }

        (float listWidth, float detailWidth) = CalculateTwoPaneWidths(drawContext.AvailableContentWidth);
        const float listHeight = 414.0f;

        bool listVisible = drawContext.BeginPanel(
            $"##UmbraStoreList-{runtime.PluginManager.ActiveTab}",
            listWidth,
            listHeight,
            UmbraPanelStyle.Default);
        try
        {
            if (listVisible && filtered.Length == 0)
            {
                drawContext.Text(
                    entries.Count == 0 ? "No repository entries are available." : "No plugins match the search.",
                    UmbraTextTone.Muted);
            }
            else if (listVisible)
            {
                foreach (UmbraStoreEntry entry in filtered)
                {
                    DrawStoreCard(drawContext, entry);
                    drawContext.Spacing(6.0f);
                }
            }
        }
        finally
        {
            drawContext.EndChild();
        }

        drawContext.SameLine();

        UmbraStoreEntry? selectedEntry = filtered.FirstOrDefault(
            entry => string.Equals(StoreEntryKey(entry), selectedStoreEntryKey, StringComparison.OrdinalIgnoreCase));
        bool detailsVisible = drawContext.BeginPanel(
            $"##UmbraStoreDetails-{runtime.PluginManager.ActiveTab}",
            detailWidth,
            listHeight,
            UmbraPanelStyle.Detail);
        try
        {
            if (detailsVisible)
                DrawStoreDetails(drawContext, selectedEntry);
        }
        finally
        {
            drawContext.EndChild();
        }
    }

    private void DrawStoreCard(IUmbraDrawContext drawContext, UmbraStoreEntry entry)
    {
        bool selected = string.Equals(StoreEntryKey(entry), selectedStoreEntryKey, StringComparison.OrdinalIgnoreCase);
        bool cardVisible = drawContext.BeginPanel(
            $"##store-card-{entry.Source}-{entry.Id}",
            0.0f,
            164.0f,
            selected ? UmbraPanelStyle.Selected : UmbraPanelStyle.Card);
        try
        {
            if (!cardVisible)
                return;

            drawContext.Artwork(entry.Id, UmbraIcon.Plug, 86.0f);
            drawContext.SameLine();
            bool bodyVisible = drawContext.BeginPanel(
                $"##store-body-{entry.Source}-{entry.Id}",
                0.0f,
                0.0f,
                UmbraPanelStyle.Default);
            try
            {
                if (!bodyVisible)
                    return;

                drawContext.Text(entry.Name, UmbraTextTone.Normal, UmbraTextStyle.Heading);
                drawContext.SameLine();
                drawContext.Badge($"v{entry.Version}", UmbraTextTone.Accent);
                drawContext.Text(
                    $"by {(string.IsNullOrWhiteSpace(entry.Author) ? "Unknown" : entry.Author)}",
                    UmbraTextTone.Muted,
                    UmbraTextStyle.Caption);
                string description = !string.IsNullOrWhiteSpace(entry.Punchline)
                    ? entry.Punchline
                    : entry.Description;
                if (!string.IsNullOrWhiteSpace(description))
                    drawContext.Text(description, UmbraTextTone.Normal, UmbraTextStyle.Body);

                bool supported = string.Equals(entry.Source, UmbraRepositorySource.Supported, StringComparison.OrdinalIgnoreCase);
                drawContext.Badge(
                    supported ? "AetherXIV supported" : "Custom · unreviewed",
                    supported ? UmbraTextTone.Success : UmbraTextTone.Warning,
                    supported ? UmbraIcon.Shield : UmbraIcon.Warning);
                drawContext.SameLine();
                drawContext.Badge($"API {entry.ApiVersion}", UmbraTextTone.Accent, UmbraIcon.Check);
                drawContext.SameLine();
                if (drawContext.Button(
                    $"{(selected ? "Selected" : "Details")}###details-{entry.Source}-{entry.Id}",
                    selected ? UmbraButtonStyle.Primary : UmbraButtonStyle.Ghost,
                    selected ? UmbraIcon.Check : UmbraIcon.Info,
                    122.0f,
                    34.0f))
                {
                    selectedStoreEntryKey = StoreEntryKey(entry);
                }
            }
            finally
            {
                drawContext.EndChild();
            }
        }
        finally
        {
            drawContext.EndChild();
        }
    }

    private void DrawStoreDetails(IUmbraDrawContext drawContext, UmbraStoreEntry? entry)
    {
        if (entry is null)
        {
            drawContext.Icon(UmbraIcon.Info, UmbraTextTone.Muted, 30.0f);
            drawContext.Text("Select a plugin to inspect repository details.", UmbraTextTone.Muted);
            return;
        }

        drawContext.Artwork(entry.Id, UmbraIcon.Plug, 84.0f);
        drawContext.SameLine();
        drawContext.Text(entry.Name, UmbraTextTone.Normal, UmbraTextStyle.Heading);
        drawContext.Text(
            $"by {(string.IsNullOrWhiteSpace(entry.Author) ? "Unknown" : entry.Author)}",
            UmbraTextTone.Muted,
            UmbraTextStyle.Caption);
        bool supported = string.Equals(entry.Source, UmbraRepositorySource.Supported, StringComparison.OrdinalIgnoreCase);
        drawContext.Badge(
            supported ? "AetherXIV supported" : "Custom · unreviewed",
            supported ? UmbraTextTone.Success : UmbraTextTone.Warning,
            supported ? UmbraIcon.Shield : UmbraIcon.Warning);
        drawContext.SameLine();
        drawContext.Badge($"API {entry.ApiVersion}", UmbraTextTone.Accent, UmbraIcon.Check);
        drawContext.Spacing(5.0f);
        drawContext.Separator();

        string description = !string.IsNullOrWhiteSpace(entry.Description)
            ? entry.Description
            : entry.Punchline;
        if (!string.IsNullOrWhiteSpace(description))
            drawContext.Text(description, UmbraTextTone.Normal, UmbraTextStyle.Body);

        drawContext.Spacing(4.0f);
        drawContext.Text($"Version: {entry.Version}", UmbraTextTone.Muted);
        drawContext.Text($"Source: {entry.Source}", UmbraTextTone.Muted);
        drawContext.Text($"Minimum framework: {entry.MinimumFrameworkVersion}", UmbraTextTone.Muted);
        if (!string.IsNullOrWhiteSpace(entry.LastUpdate))
            drawContext.Text($"Updated: {entry.LastUpdate}", UmbraTextTone.Muted);
        if (entry.SizeBytes > 0)
            drawContext.Text($"Package size: {FormatBytes(entry.SizeBytes)}", UmbraTextTone.Muted);

        drawContext.Spacing(6.0f);
        UmbraPluginManifest? installed = runtime.PluginManager.InstalledPlugins.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
        bool sameVersion = installed is not null
            && string.Equals(installed.Version, entry.Version, StringComparison.OrdinalIgnoreCase);
        if (sameVersion)
        {
            drawContext.Badge("Installed", UmbraTextTone.Success, UmbraIcon.Check);
        }
        else if (pendingAction is not null)
        {
            drawContext.Badge("Working…", UmbraTextTone.Accent, UmbraIcon.Refresh);
        }
        else if (drawContext.Button(
            $"{(installed is null ? "Install" : "Update")}###install-{entry.Source}-{entry.Id}",
            UmbraButtonStyle.Primary,
            UmbraIcon.Download,
            132.0f,
            36.0f))
        {
            StartAction(runtime.InstallPluginAsync(entry));
        }
        drawContext.Text(
            supported
                ? "The package will be downloaded, verified, staged and activated with rollback protection."
                : "Custom repository packages are checksum-verified but are not reviewed by AetherXIV.",
            supported ? UmbraTextTone.Muted : UmbraTextTone.Warning,
            UmbraTextStyle.Caption);
        if (!string.IsNullOrWhiteSpace(entry.Changelog))
        {
            drawContext.Spacing(6.0f);
            drawContext.Text("Changelog", UmbraTextTone.Normal, UmbraTextStyle.Heading);
            drawContext.Text(entry.Changelog, UmbraTextTone.Muted, UmbraTextStyle.Caption);
        }
    }

    private void DrawRepositories(IUmbraDrawContext drawContext)
    {
        drawContext.Text("Plugin Repositories", UmbraTextTone.Normal, UmbraTextStyle.Heading);
        drawContext.Text(
            "Add HTTPS plugin index URLs. Valid entries appear in Discover after the repository is checked.",
            UmbraTextTone.Muted,
            UmbraTextStyle.Caption);
        drawContext.InputText(
            "##UmbraCustomRepositoryUrl",
            ref customRepositoryUrl,
            "https://example.github.io/repository.json",
            2048);

        if (pendingAction is null)
        {
            if (drawContext.Button(
                "Add repository###repository-add",
                UmbraButtonStyle.Primary,
                UmbraIcon.Repository,
                170.0f,
                36.0f))
            {
                string url = customRepositoryUrl.Trim();
                StartAction(runtime.AddCustomRepositoryAsync(url));
                customRepositoryUrl = "";
            }
            drawContext.SameLine();
            if (drawContext.Button(
                "Refresh all###repository-refresh",
                UmbraButtonStyle.Ghost,
                UmbraIcon.Refresh,
                140.0f,
                36.0f))
            {
                StartAction(runtime.RefreshRepositoriesAsync());
            }
        }
        else
        {
            drawContext.Badge("Repository operation in progress…", UmbraTextTone.Accent, UmbraIcon.Refresh);
        }

        drawContext.Spacing(8.0f);
        drawContext.Separator();
        drawContext.Text(
            $"Configured sources ({runtime.PluginManager.RepositorySources.Count})",
            UmbraTextTone.Accent,
            UmbraTextStyle.Heading);
        if (runtime.PluginManager.RepositorySources.Count == 0)
        {
            drawContext.Text("No repositories are configured.", UmbraTextTone.Muted);
            return;
        }

        foreach (UmbraRepositorySource source in runtime.PluginManager.RepositorySources)
        {
            bool supported = string.Equals(source.Source, UmbraRepositorySource.Supported, StringComparison.OrdinalIgnoreCase);
            bool visible = drawContext.BeginPanel(
                $"##repository-{source.Url}",
                0.0f,
                136.0f,
                UmbraPanelStyle.Card);
            try
            {
                if (!visible)
                    continue;

                drawContext.Badge(
                    supported ? "Managed · supported" : "Custom · unreviewed",
                    supported ? UmbraTextTone.Success : UmbraTextTone.Warning,
                    supported ? UmbraIcon.Shield : UmbraIcon.Warning);
                drawContext.Text(source.Name ?? source.Url, UmbraTextTone.Normal, UmbraTextStyle.Caption);
                if (source.Name is not null)
                    drawContext.Text(source.Url, UmbraTextTone.Muted, UmbraTextStyle.Caption);
                int entryCount = runtime.PluginManager.SupportedPlugins
                    .Concat(runtime.PluginManager.AvailablePlugins)
                    .Count(entry => string.Equals(
                        entry.RepositoryUrl,
                        source.Url,
                        StringComparison.OrdinalIgnoreCase));
                drawContext.Text($"{entryCount} compatible plugin entries", UmbraTextTone.Muted, UmbraTextStyle.Caption);
                if (!supported && pendingAction is null)
                {
                    if (drawContext.Button(
                        $"Remove###repository-remove-{source.Url}",
                        UmbraButtonStyle.Ghost,
                        UmbraIcon.Trash,
                        104.0f,
                        30.0f))
                    {
                        StartAction(runtime.RemoveCustomRepositoryAsync(source.Url));
                    }
                }
            }
            finally
            {
                drawContext.EndChild();
            }
            drawContext.Spacing(5.0f);
        }
    }

    private void DrawSettings(IUmbraDrawContext drawContext)
    {
        bool about = runtime.PluginManager.ActiveTab == UmbraPluginManagerTab.Logs;
        if (!about && drawContext is UmbraDrawContext)
        {
            UmbraNativeUi.DrawSettingsContent();
            return;
        }

        drawContext.Text(
            about ? "About Umbra" : "Plugin Settings",
            UmbraTextTone.Normal,
            UmbraTextStyle.Heading);
        drawContext.Text(
            about ? "Framework identity and runtime diagnostics." : "Configure plugin development and inspect framework state.",
            UmbraTextTone.Muted,
            UmbraTextStyle.Caption);

        bool statusVisible = drawContext.BeginPanel("##UmbraRuntimeCard", 0.0f, 118.0f, UmbraPanelStyle.Card);
        try
        {
            if (statusVisible)
            {
                drawContext.Icon(UmbraIcon.Shield, runtime.PluginManager.SafeMode ? UmbraTextTone.Warning : UmbraTextTone.Success, 32.0f);
                drawContext.SameLine();
                drawContext.Text("Plugin Runtime", UmbraTextTone.Normal, UmbraTextStyle.Heading);
                drawContext.Badge(
                    runtime.PluginManager.SafeMode ? "Safe mode enabled" : "Verified runtime",
                    runtime.PluginManager.SafeMode ? UmbraTextTone.Warning : UmbraTextTone.Success,
                    UmbraIcon.Shield);
                drawContext.SameLine();
                drawContext.Badge(
                    runtime.PluginManager.PluginExecutionEnabled ? "Execution enabled" : "Execution blocked",
                    runtime.PluginManager.PluginExecutionEnabled ? UmbraTextTone.Success : UmbraTextTone.Warning,
                    UmbraIcon.Power);
            }
        }
        finally
        {
            drawContext.EndChild();
        }

        drawContext.Spacing(6.0f);

        bool debugLogging = runtime.PluginManager.DebugLoggingEnabled;
        bool devUi = runtime.PluginManager.DevUiEnabled;
        bool changed = drawContext.Toggle("Debug logging", ref debugLogging);
        changed |= drawContext.Toggle("Developer UI", ref devUi);
        if (changed)
            runtime.SetPluginManagerPreferences(debugLogging, devUi);

        drawContext.Separator();
        drawContext.Text($"Repositories ({runtime.PluginManager.RepositorySources.Count})", UmbraTextTone.Accent);
        if (runtime.PluginManager.RepositorySources.Count == 0)
        {
            drawContext.Text("No supported or custom repositories are configured.", UmbraTextTone.Muted);
        }
        else
        {
            foreach (UmbraRepositorySource source in runtime.PluginManager.RepositorySources)
                drawContext.Text($"{source.Source}: {source.Name ?? source.Url}", UmbraTextTone.Muted);
        }

        drawContext.Separator();
        drawContext.Text("Diagnostics", UmbraTextTone.Accent);
        drawContext.Text($"Framework log: {runtime.Options.LogPath}", UmbraTextTone.Muted);
        drawContext.Text($"Plugin directory: {runtime.Options.PluginDirectory}", UmbraTextTone.Muted);
        drawContext.Text($"Render frames: {runtime.RenderBridge.FrameCount}", UmbraTextTone.Muted);
        drawContext.Text($"Device generation: {runtime.RenderBridge.DeviceGeneration}", UmbraTextTone.Muted);
    }

    private bool MatchesSearch(UmbraPluginManifest manifest)
    {
        return MatchesSearch(manifest.Name, manifest.Id, manifest.Version);
    }

    private bool MatchesSearch(UmbraStoreEntry entry)
    {
        return MatchesSearch(entry.Name, entry.Id, entry.Author, entry.Description, entry.Punchline);
    }

    private static string StoreEntryKey(UmbraStoreEntry entry) => $"{entry.Source}:{entry.Id}";

    private static (float ListWidth, float DetailWidth) CalculateTwoPaneWidths(float availableWidth)
    {
        float usableWidth = Math.Max(2.0f, availableWidth - TwoPaneGap);
        float detailWidth = Math.Clamp(availableWidth * 0.36f, 280.0f, 360.0f);
        if (detailWidth >= usableWidth)
            detailWidth = Math.Max(1.0f, usableWidth * 0.42f);

        return (Math.Max(1.0f, usableWidth - detailWidth), detailWidth);
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB"];
        double value = bytes;
        int suffix = 0;
        while (value >= 1024.0 && suffix < suffixes.Length - 1)
        {
            value /= 1024.0;
            suffix++;
        }

        return $"{value:0.#} {suffixes[suffix]}";
    }

    private bool MatchesSearch(params string?[] values)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;
        return values.Any(value => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
    }

    private void Report(UmbraPluginActionResult result)
    {
        resultMessage = result.Message;
        resultSucceeded = result.Succeeded;
    }

    private void StartAction(Task<UmbraPluginActionResult> action)
    {
        if (pendingAction is not null)
            return;
        pendingAction = action;
        resultMessage = null;
    }

    private void PollPendingAction()
    {
        Task<UmbraPluginActionResult>? action = pendingAction;
        if (action is null || !action.IsCompleted)
            return;

        pendingAction = null;
        if (action.IsCompletedSuccessfully)
        {
            Report(action.Result);
            return;
        }

        string message = action.Exception?.GetBaseException().Message ?? "The plugin operation was cancelled.";
        Report(UmbraPluginActionResult.Failure(message));
    }
}
