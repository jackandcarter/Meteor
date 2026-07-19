using System.Text.Json.Serialization;

namespace AetherXIV.Launcher.Core;

public sealed record LauncherConfig(
    [property: JsonPropertyName("service_version")] int ServiceVersion,
    [property: JsonPropertyName("server_name")] string ServerName,
    [property: JsonPropertyName("server_status_url")] string? ServerStatusUrl,
    [property: JsonPropertyName("news_url")] string NewsUrl,
    [property: JsonPropertyName("patch_manifest_url")] string PatchManifestUrl,
    [property: JsonPropertyName("runtime_catalog_url")] string? RuntimeCatalogUrl,
    [property: JsonPropertyName("login_url")] string? LoginUrl,
    [property: JsonPropertyName("account_create_url")] string? AccountCreateUrl,
    [property: JsonPropertyName("client_login_url")] string? ClientLoginUrl,
    [property: JsonPropertyName("patch_base_url")] string? PatchBaseUrl,
    [property: JsonPropertyName("target_boot_version")] string TargetBootVersion,
    [property: JsonPropertyName("target_game_version")] string TargetGameVersion,
    [property: JsonPropertyName("client_plugin_framework_catalog_url")] string? ClientPluginFrameworkCatalogUrl = null,
    [property: JsonPropertyName("plugin_catalog_urls")] IReadOnlyList<string>? PluginCatalogUrls = null,
    [property: JsonPropertyName("plugin_blocklist_url")] string? PluginBlocklistUrl = null);

public sealed record LauncherAuthRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

public sealed record LauncherCreateAccountRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password,
    [property: JsonPropertyName("confirm_password")] string ConfirmPassword,
    [property: JsonPropertyName("email")] string Email);

public sealed record LauncherAuthResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("session_id")] string? SessionId);

public sealed record LauncherStatus(
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("checked_at")] DateTimeOffset CheckedAt);

public sealed record LauncherNewsFeed(
    [property: JsonPropertyName("items")] IReadOnlyList<LauncherNewsItem> Items,
    [property: JsonPropertyName("reel_text_enabled")] bool ReelTextEnabled = false,
    [property: JsonPropertyName("reel_text")] IReadOnlyList<LauncherReelText>? ReelText = null);

public sealed record LauncherNewsItem(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("banner_url")] string? BannerUrl,
    [property: JsonPropertyName("link_url")] string? LinkUrl,
    [property: JsonPropertyName("published_at")] DateTimeOffset PublishedAt,
    [property: JsonPropertyName("title_color")] string TitleColor = "#F2F4FA",
    [property: JsonPropertyName("summary_color")] string SummaryColor = "#D6DCE3",
    [property: JsonPropertyName("body_color")] string BodyColor = "#AEB7C2");

public sealed record LauncherReelText(
    [property: JsonPropertyName("image_file")] string ImageFile,
    [property: JsonPropertyName("header_text")] string HeaderText,
    [property: JsonPropertyName("sub_text")] string SubText,
    [property: JsonPropertyName("header_size")] double HeaderSize,
    [property: JsonPropertyName("sub_text_size")] double SubTextSize,
    [property: JsonPropertyName("header_color")] string HeaderColor,
    [property: JsonPropertyName("sub_text_color")] string SubTextColor,
    [property: JsonPropertyName("is_enabled")] bool IsEnabled);

public sealed record LauncherPatchManifest(
    [property: JsonPropertyName("target_boot_version")] string TargetBootVersion,
    [property: JsonPropertyName("target_game_version")] string TargetGameVersion,
    [property: JsonPropertyName("patch_base_url")] string PatchBaseUrl,
    [property: JsonPropertyName("files")] IReadOnlyList<LauncherPatchFile> Files)
{
    public static LauncherPatchManifest FromKnownPatchChain(string patchBaseUrl)
    {
        string normalizedBaseUrl = string.IsNullOrWhiteSpace(patchBaseUrl)
            ? ""
            : patchBaseUrl.TrimEnd('/');

        IReadOnlyList<LauncherPatchFile> files = LegacyPatchManifest.Entries
            .Select(entry => new LauncherPatchFile(
                entry.RelativePatchPath.Replace('\\', '/'),
                entry.ExpectedSizeBytes,
                entry.ExpectedCrc32Text,
                null))
            .ToArray();

        return new LauncherPatchManifest(
            ClientVersionInfo.TargetBootVersion,
            ClientVersionInfo.TargetGameVersion,
            normalizedBaseUrl,
            files);
    }
}

public sealed record LauncherPatchFile(
    [property: JsonPropertyName("relative_path")] string RelativePath,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("crc32")] string Crc32,
    [property: JsonPropertyName("sha256")] string? Sha256);

public sealed record RuntimeCatalog(
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<RuntimeArtifact> Artifacts)
{
    public RuntimeArtifact? SelectDefault()
    {
        return Artifacts
            .Where(artifact => artifact.IsActive)
            .OrderByDescending(artifact => artifact.IsDefault)
            .ThenBy(artifact => artifact.SortOrder)
            .FirstOrDefault();
    }
}

public sealed record RuntimeArtifact(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("platform_rid")] string PlatformRid,
    [property: JsonPropertyName("runtime_kind")] string RuntimeKind,
    [property: JsonPropertyName("archive_url")] string ArchiveUrl,
    [property: JsonPropertyName("archive_format")] string ArchiveFormat,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("executable_relative_path")] string ExecutableRelativePath,
    [property: JsonPropertyName("prefix_arch")] string PrefixArch,
    [property: JsonPropertyName("environment")] IReadOnlyDictionary<string, string> Environment,
    [property: JsonPropertyName("is_default")] bool IsDefault,
    [property: JsonPropertyName("is_active")] bool IsActive,
    [property: JsonPropertyName("sort_order")] int SortOrder)
{
    public string StableId => RuntimeInstallStore.SanitizePathSegment($"{PlatformRid}-{Name}-{Version}");
}
