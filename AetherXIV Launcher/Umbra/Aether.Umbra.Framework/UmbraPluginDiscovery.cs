namespace Aether.Umbra.Framework;

public static class UmbraPluginDiscovery
{
    private static readonly string[] ManifestFileNames =
    [
        "umbra-plugin.json",
        "plugin.json"
    ];

    public static IReadOnlyList<UmbraPluginManifest> Discover(string pluginDirectory, UmbraRuntimeLog log)
    {
        if (string.IsNullOrWhiteSpace(pluginDirectory))
            return Array.Empty<UmbraPluginManifest>();

        Directory.CreateDirectory(pluginDirectory);

        List<string> manifestPaths = new();
        foreach (string fileName in ManifestFileNames)
        {
            string directManifest = Path.Combine(pluginDirectory, fileName);
            if (File.Exists(directManifest))
                manifestPaths.Add(directManifest);
        }

        foreach (string childDirectory in Directory.EnumerateDirectories(pluginDirectory).Order(StringComparer.OrdinalIgnoreCase))
        {
            foreach (string fileName in ManifestFileNames)
            {
                string candidate = Path.Combine(childDirectory, fileName);
                if (File.Exists(candidate))
                    manifestPaths.Add(candidate);
            }
        }

        List<UmbraPluginManifest> manifests = new();
        foreach (string manifestPath in manifestPaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                UmbraPluginManifest manifest = UmbraPluginManifest.Load(manifestPath);
                manifests.Add(manifest);
                log.Info($"umbra_plugin_manifest={manifest.Id}|{manifest.Name}|{manifest.Version}|{manifestPath}");
            }
            catch (Exception ex)
            {
                log.Warning($"umbra_plugin_manifest_invalid path={manifestPath} error={ex.Message}");
            }
        }

        return manifests
            .OrderBy(manifest => manifest.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(manifest => manifest.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
