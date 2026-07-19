using System.Text.Json;
using System.Text.Json.Serialization;

namespace AetherXIV.Launcher.Core;

public sealed record UmbraSettings
{
    public const int DefaultLoadDelayMilliseconds = 0;
    public const int MaximumLoadDelayMilliseconds = 30000;

    public bool Enabled { get; init; }

    public bool SafeMode { get; init; }

    public int LoadDelayMilliseconds { get; init; } = DefaultLoadDelayMilliseconds;

    public string PluginDirectory { get; init; } = "";

    public bool UseOfficialRepository { get; init; } = true;

    public IReadOnlyList<string> CustomRepositoryUrls { get; init; } = Array.Empty<string>();

    public static UmbraSettings Default => new();

    public UmbraSettings Normalize()
    {
        return this with
        {
            LoadDelayMilliseconds = Math.Clamp(LoadDelayMilliseconds, 0, MaximumLoadDelayMilliseconds),
            PluginDirectory = string.IsNullOrWhiteSpace(PluginDirectory)
                ? UmbraInstallStore.PluginsRoot
                : Path.GetFullPath(PluginDirectory),
            CustomRepositoryUrls = UmbraRepositoryOptions.NormalizeCustomRepositoryUrls(CustomRepositoryUrls)
        };
    }
}

public sealed record UmbraLaunchOptions(
    bool Enabled,
    bool SafeMode,
    int LoadDelayMilliseconds,
    string BootstrapPath,
    string FrameworkPath,
    string PluginDirectory,
    string LogPath,
    IReadOnlyList<string> RepositoryUrls,
    bool EnableManagedOnWine = false)
{
    public static UmbraLaunchOptions Disabled => new(
        false,
        false,
        0,
        "",
        "",
        "",
        "",
        Array.Empty<string>());

    public IReadOnlyList<UmbraRepositorySource> RepositorySources { get; init; } =
        UmbraRepositorySource.FromUrls(RepositoryUrls, UmbraRepositorySource.Custom);

    public string RepositoriesJson => UmbraRepositorySource.ToJson(RepositorySources);

    public bool HasRequiredPaths =>
        !string.IsNullOrWhiteSpace(BootstrapPath)
        && !string.IsNullOrWhiteSpace(FrameworkPath)
        && !string.IsNullOrWhiteSpace(PluginDirectory)
        && !string.IsNullOrWhiteSpace(LogPath);

    public UmbraLaunchOptions Normalize()
    {
        IReadOnlyList<UmbraRepositorySource> repositorySources = UmbraRepositorySource.Normalize(
            RepositorySources.Count == 0
                ? UmbraRepositorySource.FromUrls(RepositoryUrls, UmbraRepositorySource.Custom)
                : RepositorySources);

        return this with
        {
            LoadDelayMilliseconds = Math.Clamp(LoadDelayMilliseconds, 0, UmbraSettings.MaximumLoadDelayMilliseconds),
            RepositoryUrls = repositorySources.Select(source => source.Url).ToArray(),
            RepositorySources = repositorySources
        };
    }
}

public sealed record UmbraRepositorySource(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("name")] string? Name = null)
{
    public const string Supported = "supported";
    public const string Custom = "custom";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public static IReadOnlyList<UmbraRepositorySource> FromUrls(
        IEnumerable<string>? urls,
        string source)
    {
        if (urls is null)
            return Array.Empty<UmbraRepositorySource>();

        return urls
            .Select(url => new UmbraRepositorySource(url, source))
            .ToArray();
    }

    public static IReadOnlyList<UmbraRepositorySource> Normalize(IEnumerable<UmbraRepositorySource>? sources)
    {
        if (sources is null)
            return Array.Empty<UmbraRepositorySource>();

        List<UmbraRepositorySource> normalized = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (UmbraRepositorySource source in sources)
        {
            string url = (source.Url ?? "").Trim();
            if (string.IsNullOrWhiteSpace(url))
                continue;

            IReadOnlyList<string> normalizedUrl = UmbraRepositoryOptions.NormalizeCustomRepositoryUrls(new[] { url });
            if (normalizedUrl.Count == 0 || !seen.Add(normalizedUrl[0]))
                continue;

            string sourceName = string.Equals(source.Source, Supported, StringComparison.OrdinalIgnoreCase)
                ? Supported
                : Custom;
            string? name = string.IsNullOrWhiteSpace(source.Name) ? null : source.Name.Trim();
            normalized.Add(new UmbraRepositorySource(normalizedUrl[0], sourceName, name));
        }

        return normalized;
    }

    public static string ToJson(IEnumerable<UmbraRepositorySource>? sources)
    {
        return JsonSerializer.Serialize(Normalize(sources), JsonOptions);
    }

    public static IReadOnlyList<UmbraRepositorySource> FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<UmbraRepositorySource>();

        IReadOnlyList<UmbraRepositorySource>? sources = JsonSerializer.Deserialize<IReadOnlyList<UmbraRepositorySource>>(json, JsonOptions);
        return Normalize(sources);
    }
}
