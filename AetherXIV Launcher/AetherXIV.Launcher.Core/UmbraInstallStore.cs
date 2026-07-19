using System.Text.Json;

namespace AetherXIV.Launcher.Core;

public static class UmbraInstallStore
{
    private const string ManifestFileName = "umbra-framework.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string Root => Path.Combine(RuntimeInstallStore.ApplicationDataRoot, "Umbra");

    public static string FrameworksRoot => Path.Combine(Root, "Frameworks");

    public static string FrameworkCacheRoot => Path.Combine(Root, "FrameworkCache");

    public static string PluginsRoot => Path.Combine(Root, "Plugins");

    public static string LogsRoot => Path.Combine(Root, "Logs");

    public static string FrameworkInstallRootFor(UmbraFrameworkArtifact artifact, string? frameworksRoot = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return Path.Combine(frameworksRoot ?? FrameworksRoot, artifact.StableId);
    }

    public static string ManifestPathFor(string installRoot)
    {
        return Path.Combine(Path.GetFullPath(installRoot), ManifestFileName);
    }

    public static void Save(UmbraFrameworkInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        Directory.CreateDirectory(install.InstallPath);
        File.WriteAllText(ManifestPathFor(install.InstallPath), JsonSerializer.Serialize(install, JsonOptions));
    }

    public static UmbraFrameworkInstall Load(string installRoot)
    {
        string json = File.ReadAllText(ManifestPathFor(installRoot));
        UmbraFrameworkInstall? install = JsonSerializer.Deserialize<UmbraFrameworkInstall>(json, JsonOptions);
        return install ?? throw new InvalidOperationException("Umbra framework install manifest could not be read.");
    }

    public static UmbraFrameworkInstall? FindInstalled(UmbraFrameworkArtifact artifact, string? frameworksRoot = null)
    {
        string installRoot = FrameworkInstallRootFor(artifact, frameworksRoot);
        string manifestPath = ManifestPathFor(installRoot);
        if (!File.Exists(manifestPath))
            return null;

        UmbraFrameworkInstall install = Load(installRoot);
        return IsUsable(install) ? install : null;
    }

    public static UmbraFrameworkInstall? FindLatestInstalled(string? frameworksRoot = null)
    {
        string root = frameworksRoot ?? FrameworksRoot;
        if (!Directory.Exists(root))
            return null;

        return Directory.EnumerateDirectories(root)
            .Select(TryLoad)
            .Where(install => install is not null && IsUsable(install))
            .OrderByDescending(install => install!.InstalledAt)
            .FirstOrDefault();
    }

    public static UmbraFrameworkInstall? FindBundled(string? baseDirectory = null)
    {
        string root = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "Umbra", "Framework");
        string bootstrapPath = Path.Combine(root, "Aether.Umbra.Bootstrap.x86.dll");
        string frameworkPath = Path.Combine(root, "Managed", "Aether.Umbra.Framework.dll");

        if (!File.Exists(frameworkPath))
            frameworkPath = Path.Combine(root, "Managed", "Aether.Umbra.Framework.exe");

        if (!File.Exists(bootstrapPath) || !File.Exists(frameworkPath))
            return null;

        return new UmbraFrameworkInstall(
            "Aether Umbra",
            ReadBundledVersion(root),
            UmbraCompatibility.CurrentApiVersion,
            "win-x86",
            root,
            bootstrapPath,
            frameworkPath,
            new[] { UmbraCompatibility.Known123bGameSha256 },
            File.GetLastWriteTimeUtc(bootstrapPath));
    }

    public static string CreateLogPath(string prefix = "umbra")
    {
        Directory.CreateDirectory(LogsRoot);
        string timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(LogsRoot, $"{prefix}-{timestamp}.log");
    }

    public static void ResetFrameworks()
    {
        if (Directory.Exists(FrameworksRoot))
            Directory.Delete(FrameworksRoot, true);
    }

    private static UmbraFrameworkInstall? TryLoad(string installRoot)
    {
        try
        {
            return Load(installRoot);
        }
        catch
        {
            return null;
        }
    }

    private static string ReadBundledVersion(string root)
    {
        string versionPath = Path.Combine(root, "version.txt");
        if (!File.Exists(versionPath))
            return "0.1.0";

        string value = File.ReadAllText(versionPath).Trim();
        return string.IsNullOrWhiteSpace(value) ? "0.1.0" : value;
    }

    private static bool IsUsable(UmbraFrameworkInstall? install)
    {
        return install is not null
            && install.UsesAetherEntrypoints
            && File.Exists(install.BootstrapPath)
            && File.Exists(install.FrameworkPath);
    }
}
