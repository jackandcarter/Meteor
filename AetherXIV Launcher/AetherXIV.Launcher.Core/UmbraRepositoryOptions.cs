namespace AetherXIV.Launcher.Core;

public static class UmbraRepositoryOptions
{
    public static IReadOnlyList<UmbraRepositorySource> BuildEffectiveRepositorySources(
        UmbraSettings settings,
        IEnumerable<string>? supportedRepositoryUrls,
        string? supportedRepositoryBaseUrl = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        List<UmbraRepositorySource> sources = new();
        if (settings.UseOfficialRepository)
        {
            sources.AddRange(UmbraRepositorySource.FromUrls(
                NormalizeSupportedRepositoryUrls(supportedRepositoryUrls, supportedRepositoryBaseUrl),
                UmbraRepositorySource.Supported));
        }

        sources.AddRange(UmbraRepositorySource.FromUrls(
            settings.CustomRepositoryUrls,
            UmbraRepositorySource.Custom));
        return UmbraRepositorySource.Normalize(sources);
    }

    public static IReadOnlyList<string> BuildEffectiveRepositoryUrls(
        UmbraSettings settings,
        IEnumerable<string>? supportedRepositoryUrls = null,
        string? supportedRepositoryBaseUrl = null)
    {
        return BuildEffectiveRepositorySources(settings, supportedRepositoryUrls, supportedRepositoryBaseUrl)
            .Select(source => source.Url)
            .ToArray();
    }

    public static IReadOnlyList<string> NormalizeSupportedRepositoryUrls(
        IEnumerable<string>? urls,
        string? baseUrl = null)
    {
        if (urls is null)
            return Array.Empty<string>();

        Uri? baseUri = CreateOptionalBaseUri(baseUrl);
        List<string> normalized = new();
        foreach (string? source in urls)
        {
            string value = (source ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            string resolved = ResolveSupportedRepositoryUrl(value, baseUri);
            normalized.AddRange(NormalizeCustomRepositoryUrls(new[] { resolved }));
        }

        return normalized.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static IReadOnlyList<string> NormalizeCustomRepositoryUrls(IEnumerable<string>? urls)
    {
        if (urls is null)
            return Array.Empty<string>();

        List<string> normalized = new();
        foreach (string? source in urls)
        {
            string value = (source ?? "").Trim();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
                throw new ArgumentException($"Umbra repository URL is invalid: {value}");

            if (!IsAllowedRepositoryUri(uri))
                throw new ArgumentException($"Umbra repository URL must use HTTPS, except localhost development URLs: {value}");

            normalized.Add(uri.ToString());
        }

        return normalized.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static bool IsAllowedRepositoryUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            return false;

        return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> ParseRepositoryList(string value)
    {
        return NormalizeCustomRepositoryUrls(
            (value ?? "")
                .Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    public static string FormatRepositoryList(IEnumerable<string> urls)
    {
        return string.Join(Environment.NewLine, NormalizeCustomRepositoryUrls(urls));
    }

    private static string ResolveSupportedRepositoryUrl(string value, Uri? baseUri)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? absoluteUri))
            return absoluteUri.ToString();

        if (baseUri is null)
            throw new ArgumentException($"Umbra repository URL is relative but no launcher service URL was provided: {value}");

        return new Uri(baseUri, value).ToString();
    }

    private static Uri? CreateOptionalBaseUri(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;

        string normalized = baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/";
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri))
            throw new ArgumentException($"Launcher service URL is invalid: {baseUrl}");

        if (!IsAllowedRepositoryUri(uri))
            throw new ArgumentException($"Launcher service URL must use HTTPS, except localhost development URLs: {baseUrl}");

        return uri;
    }
}
