namespace Aether.Umbra.Framework;

public static class UmbraBootstrapRunner
{
    public static async Task<int> RunFromEnvironmentAsync(string? explicitLogPath = null)
    {
        UmbraRuntimeOptions options = UmbraRuntimeOptions.FromEnvironment(explicitLogPath);
        UmbraRuntimeLog log = UmbraRuntimeLog.Open(options.LogPath);
        try
        {
            log.Info("umbra_framework_started=true");
            log.Info($"umbra_framework_version={UmbraFrameworkInfo.Version}");
            log.Info($"umbra_api_version={UmbraFrameworkInfo.ApiVersion}");
            log.Info($"umbra_plugin_dir={options.PluginDirectory}");
            log.Info($"umbra_cache_dir={options.CacheDirectory}");
            log.Info($"umbra_dev_bridge_dir={options.DevBridgeDirectory}");
            log.Info($"umbra_dev_bridge_control={options.DevBridgeControlPath}");
            log.Info($"umbra_safe_mode={options.SafeMode}");
            log.Info($"umbra_repository_urls={string.Join(";", options.RepositoryUrls)}");
            log.Info($"umbra_repository_source_count={options.RepositorySources.Count}");
            log.Info("umbra_host_mode=in_process");
            log.Info("umbra_dx9_hook_installed=false");
            log.Info("umbra_imgui_backend_ready=false");
            log.Info("umbra_title_screen_icons_rendered=false");
            log.Info("umbra_ready=false");

            UmbraRuntime runtime = await UmbraRuntimeHost.StartOrGetAsync(options, log);
            log.Info($"umbra_plugin_manifest_count={runtime.PluginManager.InstalledPlugins.Count}");
            log.Info($"umbra_plugin_enabled_count={runtime.PluginManager.InstalledPlugins.Count(plugin => plugin.Enabled)}");
            log.Info($"umbra_supported_plugin_count={runtime.PluginManager.SupportedPlugins.Count}");
            log.Info($"umbra_available_plugin_count={runtime.PluginManager.AvailablePlugins.Count}");
            log.Info($"umbra_plugin_update_count={runtime.PluginManager.Updates.Count}");
            log.Info(runtime.Options.SafeMode
                ? "umbra_plugin_load_mode=system_plugins_only_safe_mode"
                : "umbra_plugin_load_mode=system_and_enabled_third_party_plugins");
            log.Info($"umbra_plugin_execution_enabled={!runtime.Options.SafeMode}");
            log.Info($"umbra_plugin_running_count={runtime.Plugins.Statuses.Count(status => status.State == UmbraPluginRuntimeState.Running)}");
            log.Info("umbra_system_plugins=dev_bridge,trace_companion");
            log.Info("umbra_ui_shell=settings,plugin_installer,toasts");
            log.Info("umbra_framework_completed=true");
            Console.WriteLine($"{UmbraFrameworkInfo.Name} bootstrap complete: {runtime.PluginManager.InstalledPlugins.Count} plugin manifest(s)");
            return 0;
        }
        catch (Exception ex)
        {
            log.Error("umbra_framework_failed=true");
            log.Error($"umbra_framework_error={ex}");
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
