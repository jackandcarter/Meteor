using System.Reflection;
using System.Diagnostics;
using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

public enum UmbraPluginRuntimeState
{
    Discovered,
    Loading,
    Running,
    Faulted,
    Unloaded
}

public sealed record UmbraPluginRuntimeStatus(
    string PluginId,
    string Name,
    string Version,
    UmbraPluginRuntimeState State,
    int ErrorCount,
    DateTimeOffset? LoadedAt,
    string? LastError,
    TimeSpan LastUpdateDuration,
    TimeSpan LastDrawDuration,
    TimeSpan PeakDrawDuration,
    long SlowDrawCount);

public sealed class UmbraThirdPartyPluginHost : IDisposable
{
    public const int MaximumConsecutiveCallbackErrors = 3;
    public static readonly TimeSpan DrawCallbackBudget = TimeSpan.FromMilliseconds(4);

    private readonly UmbraRuntime runtime;
    private readonly object gate = new();
    private readonly Dictionary<string, LoadedPlugin> loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, UmbraPluginRuntimeStatus> inactive = new(StringComparer.OrdinalIgnoreCase);
    private bool disposed;

    internal UmbraThirdPartyPluginHost(UmbraRuntime runtime)
    {
        this.runtime = runtime;
    }

    public IReadOnlyList<UmbraPluginRuntimeStatus> Statuses
    {
        get
        {
            lock (gate)
            {
                return loaded.Values.Select(plugin => plugin.Status)
                    .Concat(inactive.Values)
                    .OrderBy(status => status.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(status => status.PluginId, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }
    }

    public void LoadEnabled(IEnumerable<UmbraPluginManifest> manifests)
    {
        if (runtime.Options.SafeMode)
        {
            runtime.Log.Warning("umbra_third_party_plugins_skipped=safe_mode");
            return;
        }

        foreach (UmbraPluginManifest manifest in manifests.Where(candidate => candidate.Enabled))
        {
            try
            {
                Load(manifest);
            }
            catch (Exception ex)
            {
                lock (gate)
                {
                    inactive[manifest.Id] = CreateStatus(manifest, UmbraPluginRuntimeState.Faulted) with
                    {
                        ErrorCount = 1,
                        LastError = ex.Message
                    };
                }
                runtime.Log.Error($"umbra_plugin_rejected id={manifest.Id}", ex);
            }
        }
    }

    public UmbraPluginRuntimeStatus Load(UmbraPluginManifest manifest)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        manifest.Validate();
        UmbraPluginCompatibility.Validate(manifest);

        lock (gate)
        {
            if (loaded.TryGetValue(manifest.Id, out LoadedPlugin? existing))
                return existing.Status;

            inactive.Remove(manifest.Id);
        }

        string assemblyPath = ResolveEntryAssembly(manifest);
        UmbraPluginLoadContext loadContext = new(assemblyPath);
        LoadedPlugin candidate = new(manifest, loadContext)
        {
            Status = CreateStatus(manifest, UmbraPluginRuntimeState.Loading)
        };

        try
        {
            Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            Type pluginType = ResolvePluginType(assembly, manifest);
            candidate.Instance = Activator.CreateInstance(pluginType) as IUmbraPlugin
                ?? throw new InvalidDataException($"Umbra plugin entry type does not implement {nameof(IUmbraPlugin)}: {pluginType.FullName}");

            Directory.CreateDirectory(Path.Combine(
                runtime.Options.CacheDirectory,
                "PluginConfig",
                Sanitize(manifest.Id)));

            candidate.Instance.Initialize(new UmbraPluginContext(runtime, manifest.Id, manifest.Capabilities));
            candidate.Status = CreateStatus(manifest, UmbraPluginRuntimeState.Running) with
            {
                LoadedAt = DateTimeOffset.UtcNow
            };

            lock (gate)
                loaded.Add(manifest.Id, candidate);

            runtime.Log.Info($"umbra_plugin_loaded id={manifest.Id} name={manifest.Name} version={manifest.Version} entry={assemblyPath}");
            return candidate.Status;
        }
        catch (Exception ex)
        {
            candidate.Status = CreateStatus(manifest, UmbraPluginRuntimeState.Faulted) with
            {
                ErrorCount = 1,
                LastError = ex.Message
            };
            DisposePlugin(candidate, invokePluginDispose: true);
            lock (gate)
                inactive[manifest.Id] = candidate.Status;
            runtime.Log.Error($"umbra_plugin_load_failed id={manifest.Id}", ex);
            return candidate.Status;
        }
    }

    public bool Unload(string pluginId)
    {
        LoadedPlugin? plugin;
        lock (gate)
        {
            if (!loaded.Remove(pluginId, out plugin))
                return false;
        }

        plugin.Status = plugin.Status with { State = UmbraPluginRuntimeState.Unloaded };
        DisposePlugin(plugin, invokePluginDispose: true);
        lock (gate)
            inactive[pluginId] = plugin.Status;
        runtime.Log.Info($"umbra_plugin_unloaded id={pluginId}");
        return true;
    }

    public void Update(TimeSpan delta)
    {
        LoadedPlugin[] snapshot;
        lock (gate)
            snapshot = loaded.Values.ToArray();

        foreach (LoadedPlugin plugin in snapshot)
            Invoke(plugin, "update", instance => instance.Update(delta));
    }

    public void Draw(IUmbraDrawContext drawContext)
    {
        LoadedPlugin[] snapshot;
        lock (gate)
            snapshot = loaded.Values.ToArray();

        foreach (LoadedPlugin plugin in snapshot)
        {
            try
            {
                Invoke(plugin, "draw", instance => instance.Draw(drawContext));
            }
            finally
            {
                if (drawContext is IUmbraDrawContextRecovery recovery)
                    recovery.RecoverAfterPluginCallback();
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        LoadedPlugin[] snapshot;
        lock (gate)
        {
            snapshot = loaded.Values.ToArray();
            loaded.Clear();
            inactive.Clear();
        }

        for (int index = snapshot.Length - 1; index >= 0; index--)
            DisposePlugin(snapshot[index], invokePluginDispose: true);
    }

    private void Invoke(LoadedPlugin plugin, string callback, Action<IUmbraPlugin> action)
    {
        IUmbraPlugin? instance = plugin.Instance;
        if (instance is null || plugin.Status.State != UmbraPluginRuntimeState.Running)
            return;

        long startedAt = Stopwatch.GetTimestamp();
        bool quarantine = false;
        try
        {
            action(instance);
            plugin.ConsecutiveCallbackErrors = 0;
        }
        catch (Exception ex)
        {
            plugin.ConsecutiveCallbackErrors++;
            plugin.Status = plugin.Status with
            {
                ErrorCount = plugin.Status.ErrorCount + 1,
                LastError = ex.Message
            };
            runtime.Log.Error($"umbra_plugin_callback_failed id={plugin.Manifest.Id} callback={callback}", ex);

            if (plugin.ConsecutiveCallbackErrors >= MaximumConsecutiveCallbackErrors)
                quarantine = true;
        }
        finally
        {
            TimeSpan duration = Stopwatch.GetElapsedTime(startedAt);
            RecordCallbackTiming(plugin, callback, duration);
        }

        if (quarantine)
            Quarantine(plugin, callback);
    }

    private void RecordCallbackTiming(LoadedPlugin plugin, string callback, TimeSpan duration)
    {
        if (callback == "update")
        {
            plugin.Status = plugin.Status with { LastUpdateDuration = duration };
            return;
        }

        if (callback != "draw")
            return;

        bool slow = duration > DrawCallbackBudget;
        long slowDrawCount = plugin.Status.SlowDrawCount + (slow ? 1 : 0);
        plugin.Status = plugin.Status with
        {
            LastDrawDuration = duration,
            PeakDrawDuration = duration > plugin.Status.PeakDrawDuration
                ? duration
                : plugin.Status.PeakDrawDuration,
            SlowDrawCount = slowDrawCount
        };

        if (slow && (slowDrawCount == 1 || slowDrawCount % 120 == 0))
        {
            runtime.Log.Warning(
                $"umbra_plugin_draw_over_budget id={plugin.Manifest.Id} " +
                $"elapsed_ms={duration.TotalMilliseconds:F3} budget_ms={DrawCallbackBudget.TotalMilliseconds:F3} " +
                $"count={slowDrawCount}");
        }
    }

    private void Quarantine(LoadedPlugin plugin, string callback)
    {
        lock (gate)
        {
            loaded.Remove(plugin.Manifest.Id);
            inactive[plugin.Manifest.Id] = plugin.Status;
        }

        plugin.Status = plugin.Status with
        {
            State = UmbraPluginRuntimeState.Faulted,
            LastError = $"Disabled after {MaximumConsecutiveCallbackErrors} consecutive {callback} failures. {plugin.Status.LastError}"
        };
        lock (gate)
            inactive[plugin.Manifest.Id] = plugin.Status;
        DisposePlugin(plugin, invokePluginDispose: true);
        runtime.Log.Warning($"umbra_plugin_quarantined id={plugin.Manifest.Id} callback={callback}");
    }

    private static string ResolveEntryAssembly(UmbraPluginManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.ManifestPath))
            throw new InvalidDataException($"Umbra plugin manifest has no source path: {manifest.Id}");

        string pluginRoot = Path.GetFullPath(Path.GetDirectoryName(manifest.ManifestPath)
            ?? throw new InvalidDataException($"Umbra plugin manifest path has no directory: {manifest.Id}"));
        string assemblyPath = Path.GetFullPath(Path.Combine(pluginRoot, manifest.Entry));
        if (!assemblyPath.StartsWith(pluginRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Umbra plugin entry escapes its install directory: {manifest.Id}");
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Umbra plugin entry assembly was not found: {manifest.Id}", assemblyPath);

        return assemblyPath;
    }

    private static Type ResolvePluginType(Assembly assembly, UmbraPluginManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.EntryType))
        {
            return assembly.GetType(manifest.EntryType, throwOnError: true, ignoreCase: false)
                ?? throw new InvalidDataException($"Umbra plugin entry type was not found: {manifest.EntryType}");
        }

        Type[] candidates;
        try
        {
            candidates = assembly.GetTypes()
                .Where(type => type is { IsAbstract: false, IsInterface: false }
                    && type.IsPublic
                    && typeof(IUmbraPlugin).IsAssignableFrom(type))
                .ToArray();
        }
        catch (ReflectionTypeLoadException ex)
        {
            string details = string.Join("; ", ex.LoaderExceptions
                .Where(loaderException => loaderException is not null)
                .Select(loaderException => loaderException!.Message));
            throw new InvalidDataException($"Umbra plugin types could not be inspected: {details}", ex);
        }

        return candidates.Length switch
        {
            1 => candidates[0],
            0 => throw new InvalidDataException($"Umbra plugin assembly contains no public {nameof(IUmbraPlugin)} implementation: {manifest.Id}"),
            _ => throw new InvalidDataException($"Umbra plugin assembly contains multiple entry types; set entry_type in the manifest: {manifest.Id}")
        };
    }

    private static UmbraPluginRuntimeStatus CreateStatus(
        UmbraPluginManifest manifest,
        UmbraPluginRuntimeState state)
    {
        return new UmbraPluginRuntimeStatus(
            manifest.Id,
            manifest.Name,
            manifest.Version,
            state,
            0,
            null,
            null,
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.Zero,
            0);
    }

    private void DisposePlugin(LoadedPlugin plugin, bool invokePluginDispose)
    {
        if (invokePluginDispose && plugin.Instance is not null)
        {
            try
            {
                plugin.Instance.Dispose();
            }
            catch
            {
                // Teardown must continue so the collectible context can be released.
            }
        }

        plugin.Instance = null;
        runtime.Commands.Release(plugin.Manifest.Id);
        plugin.LoadContext.Unload();
    }

    private static string Sanitize(string value)
    {
        char[] chars = value
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_')
            .ToArray();
        string sanitized = new(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "plugin" : sanitized;
    }

    private sealed class LoadedPlugin(UmbraPluginManifest manifest, UmbraPluginLoadContext loadContext)
    {
        public UmbraPluginManifest Manifest { get; } = manifest;

        public UmbraPluginLoadContext LoadContext { get; } = loadContext;

        public IUmbraPlugin? Instance { get; set; }

        public UmbraPluginRuntimeStatus Status { get; set; } = CreateStatus(manifest, UmbraPluginRuntimeState.Discovered);

        public int ConsecutiveCallbackErrors { get; set; }
    }
}
