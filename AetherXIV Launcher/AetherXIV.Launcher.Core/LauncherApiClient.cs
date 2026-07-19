using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AetherXIV.Launcher.Core;

public sealed class LauncherApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient;
    private readonly Uri baseUri;

    public LauncherApiClient(HttpClient httpClient, string serviceBaseUrl)
    {
        this.httpClient = httpClient;
        baseUri = CreateBaseUri(serviceBaseUrl);
    }

    public Task<LauncherConfig?> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        return GetAsync<LauncherConfig>("config", cancellationToken);
    }

    public Task<LauncherStatus?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return GetAsync<LauncherStatus>("status", cancellationToken);
    }

    public Task<LauncherNewsFeed?> GetNewsAsync(CancellationToken cancellationToken = default)
    {
        return GetAsync<LauncherNewsFeed>("news", cancellationToken);
    }

    public Task<LauncherPatchManifest?> GetPatchManifestAsync(CancellationToken cancellationToken = default)
    {
        return GetAsync<LauncherPatchManifest>("patch-manifest", cancellationToken);
    }

    public Task<RuntimeCatalog?> GetRuntimeCatalogAsync(string platformRid, CancellationToken cancellationToken = default)
    {
        return GetRuntimeCatalogAsync(platformRid, null, cancellationToken);
    }

    public Task<RuntimeCatalog?> GetRuntimeCatalogAsync(
        string platformRid,
        string? runtimeCatalogPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(platformRid))
            throw new ArgumentException("Platform runtime identifier is required.", nameof(platformRid));

        string encoded = Uri.EscapeDataString(platformRid);
        string endpoint = string.IsNullOrWhiteSpace(runtimeCatalogPath)
            ? "runtime-catalog"
            : runtimeCatalogPath.TrimStart('/');
        char separator = endpoint.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return GetAsync<RuntimeCatalog>($"{endpoint}{separator}platform={encoded}", cancellationToken);
    }

    public Task<UmbraFrameworkCatalog?> GetUmbraFrameworkCatalogAsync(
        string platformRid,
        CancellationToken cancellationToken = default)
    {
        return GetUmbraFrameworkCatalogAsync(platformRid, null, cancellationToken);
    }

    public Task<UmbraFrameworkCatalog?> GetUmbraFrameworkCatalogAsync(
        string platformRid,
        string? catalogPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(platformRid))
            throw new ArgumentException("Platform runtime identifier is required.", nameof(platformRid));

        string encoded = Uri.EscapeDataString(platformRid);
        string endpoint = string.IsNullOrWhiteSpace(catalogPath)
            ? "umbra/framework-catalog"
            : catalogPath.TrimStart('/');
        char separator = endpoint.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return GetAsync<UmbraFrameworkCatalog>($"{endpoint}{separator}platform={encoded}", cancellationToken);
    }

    public Task<UmbraPluginCatalog?> GetUmbraPluginCatalogAsync(
        string catalogPath,
        CancellationToken cancellationToken = default)
    {
        string endpoint = string.IsNullOrWhiteSpace(catalogPath)
            ? "umbra/plugin-catalog"
            : catalogPath.TrimStart('/');
        return GetAsync<UmbraPluginCatalog>(endpoint, cancellationToken);
    }

    public Task<LauncherAuthResponse?> LoginAsync(
        LauncherAuthRequest request,
        string? loginPath,
        CancellationToken cancellationToken = default)
    {
        return PostAsync<LauncherAuthRequest, LauncherAuthResponse>(
            string.IsNullOrWhiteSpace(loginPath) ? "login" : loginPath,
            request,
            cancellationToken);
    }

    public Task<LauncherAuthResponse?> CreateAccountAsync(
        LauncherCreateAccountRequest request,
        string? accountCreatePath,
        CancellationToken cancellationToken = default)
    {
        return PostAsync<LauncherCreateAccountRequest, LauncherAuthResponse>(
            string.IsNullOrWhiteSpace(accountCreatePath) ? "create-account" : accountCreatePath,
            request,
            cancellationToken);
    }

    private async Task<T?> GetAsync<T>(string relativePath, CancellationToken cancellationToken)
    {
        Uri uri = new(baseUri, relativePath);
        using HttpResponseMessage response = await httpClient.GetAsync(uri, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return default;

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(
        string relativePath,
        TRequest payload,
        CancellationToken cancellationToken)
    {
        Uri uri = new(baseUri, relativePath.TrimStart('/'));
        string json = JsonSerializer.Serialize(payload, JsonOptions);
        using StringContent content = new(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await httpClient.PostAsync(uri, content, cancellationToken);
        TResponse? result = await response.Content.ReadFromJsonAsync<TResponse>(
            JsonOptions,
            cancellationToken);
        return result;
    }

    private static Uri CreateBaseUri(string serviceBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(serviceBaseUrl))
            throw new ArgumentException("Launcher service URL is required.", nameof(serviceBaseUrl));

        string normalized = serviceBaseUrl.EndsWith('/') ? serviceBaseUrl : $"{serviceBaseUrl}/";
        return new Uri(normalized, UriKind.Absolute);
    }
}
