using System.Text.Json.Serialization;
using AetherXIV.Core;

namespace AetherXIV.Launcher.Contracts;

public enum AetherXivServerGeneration
{
    LegacyAetherXiv,
    AetherXiv2
}

public sealed record AetherXivLauncherServerProfile(
    string Name,
    AetherXivServerGeneration Generation,
    ServerEndpoint LauncherEndpoint,
    ServerEndpoint LobbyEndpoint,
    ServerEndpoint WorldEndpoint,
    ServerEndpoint MapEndpoint,
    string ClientLoginUrl,
    string? Notes = null);

public sealed record ClientInstallDescriptor(
    string RootPath,
    string Version,
    bool HasBootExecutable,
    bool HasGameExecutable);

public sealed record ClientDataExtractionRequest(
    string ClientRootPath,
    string OutputRootPath,
    IReadOnlyList<string> DataKinds);

public sealed record ClientDataExtractionResult(
    bool Success,
    IReadOnlyList<string> WrittenFiles,
    string? Error);

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
    [property: JsonPropertyName("files")] IReadOnlyList<LauncherPatchFile> Files);

public sealed record LauncherPatchFile(
    [property: JsonPropertyName("relative_path")] string RelativePath,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("crc32")] string Crc32,
    [property: JsonPropertyName("sha256")] string? Sha256);

public sealed record RuntimeCatalog(
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<RuntimeArtifact> Artifacts);

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
    [property: JsonPropertyName("sort_order")] int SortOrder);

public sealed record UmbraFrameworkCatalog(
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<UmbraFrameworkArtifact> Artifacts);

public sealed record UmbraFrameworkArtifact(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("api_version")] string ApiVersion,
    [property: JsonPropertyName("platform_rid")] string PlatformRid,
    [property: JsonPropertyName("archive_url")] string ArchiveUrl,
    [property: JsonPropertyName("archive_format")] string ArchiveFormat,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("bootstrap_relative_path")] string BootstrapRelativePath,
    [property: JsonPropertyName("framework_relative_path")] string FrameworkRelativePath,
    [property: JsonPropertyName("supported_game_sha256")] IReadOnlyList<string> SupportedGameSha256,
    [property: JsonPropertyName("is_default")] bool IsDefault,
    [property: JsonPropertyName("is_active")] bool IsActive,
    [property: JsonPropertyName("sort_order")] int SortOrder);

public sealed record UmbraPluginCatalog(
    [property: JsonPropertyName("repository_name")] string RepositoryName,
    [property: JsonPropertyName("plugins")] IReadOnlyList<UmbraPluginCatalogEntry> Plugins);

public sealed record UmbraPluginCatalogEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("api_version")] string ApiVersion,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("download_url")] string DownloadUrl,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("minimum_framework_version")] string MinimumFrameworkVersion,
    [property: JsonPropertyName("is_active")] bool IsActive);

public sealed record UmbraPluginBlocklist(
    [property: JsonPropertyName("blocks")] IReadOnlyList<UmbraPluginBlock> Blocks);

public sealed record UmbraPluginBlock(
    [property: JsonPropertyName("plugin_key")] string PluginKey,
    [property: JsonPropertyName("repository_url")] string RepositoryUrl,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("reason")] string Reason);

public static class AetherXivLauncherDefaults
{
    public const string TargetBootVersion = "2010.09.18.0000";
    public const string TargetGameVersion = "2012.09.19.0001";

    public static AetherXivLauncherServerProfile LocalAetherXiv2 { get; } = new(
        "Local AetherXIV 2.0",
        AetherXivServerGeneration.AetherXiv2,
        new ServerEndpoint("127.0.0.1", 8080),
        new ServerEndpoint("127.0.0.1", 54994),
        new ServerEndpoint("127.0.0.1", 54992),
        new ServerEndpoint("127.0.0.1", 1989),
        "http://127.0.0.1:8080/login/index.php",
        "Local single-PC Wine/macOS development profile.");

    public static LauncherConfig LocalConfig { get; } = new(
        1,
        "AetherXIV 2 Local",
        "status",
        "news",
        "patch-manifest",
        "runtime-catalog",
        "login",
        "create-account",
        "../login/index.php",
        "",
        TargetBootVersion,
        TargetGameVersion,
        "umbra/framework-catalog",
        ["umbra/plugin-catalog"],
        "umbra/plugin-blocklist");
}
