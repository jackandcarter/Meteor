using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aether.Umbra.Framework;

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

    public static IReadOnlyList<UmbraRepositorySource> FromUrls(IEnumerable<string>? urls, string source)
    {
        if (urls is null)
            return Array.Empty<UmbraRepositorySource>();

        return Normalize(urls.Select(url => new UmbraRepositorySource(url, source)));
    }

    public static IReadOnlyList<UmbraRepositorySource> FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<UmbraRepositorySource>();

        IReadOnlyList<UmbraRepositorySource>? sources =
            JsonSerializer.Deserialize<IReadOnlyList<UmbraRepositorySource>>(json, JsonOptions);
        return Normalize(sources);
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
            if (string.IsNullOrWhiteSpace(url) || !IsAllowedUri(url))
                continue;

            if (!seen.Add(url))
                continue;

            string sourceKind = string.Equals(source.Source, Supported, StringComparison.OrdinalIgnoreCase)
                ? Supported
                : Custom;
            string? name = string.IsNullOrWhiteSpace(source.Name) ? null : source.Name.Trim();
            normalized.Add(new UmbraRepositorySource(url, sourceKind, name));
        }

        return normalized;
    }

    public static string ToJson(IEnumerable<UmbraRepositorySource>? sources)
    {
        return JsonSerializer.Serialize(Normalize(sources), JsonOptions);
    }

    private static bool IsAllowedUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
            return false;

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
    }
}
