using System.Text.Json;
using AetherXIV.Data;
using AetherXIV.Launcher.Contracts;
using MySqlConnector;

namespace AetherXIV.Launcher.Host;

public sealed class MariaDbLauncherContentRepository : ILauncherContentRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbLauncherContentRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async ValueTask<LauncherConfig?> GetActiveConfigAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        LauncherConfigRow? row = await ReadActiveConfigRowAsync(connection, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return null;

        IReadOnlyList<string> pluginCatalogUrls = await ReadPluginCatalogUrlsAsync(connection, row.ConfigKey, cancellationToken)
            .ConfigureAwait(false);
        return new LauncherConfig(
            row.ServiceVersion,
            row.ServerName,
            row.ServerStatusUrl,
            row.NewsUrl,
            row.PatchManifestUrl,
            row.RuntimeCatalogUrl,
            row.LoginUrl,
            row.AccountCreateUrl,
            row.ClientLoginUrl,
            row.PatchBaseUrl,
            row.TargetBootVersion,
            row.TargetGameVersion,
            row.ClientPluginFrameworkCatalogUrl,
            pluginCatalogUrls,
            row.PluginBlocklistUrl);
    }

    public async ValueTask<LauncherStatusRecord?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT state, message
FROM launcher_status
WHERE status_key = 'default'
LIMIT 1;
""";

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new LauncherStatusRecord(
            reader.GetString("state"),
            reader.GetString("message"));
    }

    public async ValueTask<IReadOnlyList<LauncherNewsItem>> GetNewsItemsAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT news_id, title, summary, body, banner_url, link_url, published_at,
       title_color, summary_color, body_color
FROM launcher_news
WHERE is_active = 1
  AND published_at <= UTC_TIMESTAMP()
ORDER BY sort_order ASC, published_at DESC, news_id DESC
LIMIT 20;
""";

        List<LauncherNewsItem> items = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new LauncherNewsItem(
                reader.GetInt32("news_id"),
                reader.GetString("title"),
                reader.GetString("summary"),
                ReadNullableString(reader, "body"),
                ReadNullableString(reader, "banner_url"),
                ReadNullableString(reader, "link_url"),
                ReadUtcDateTimeOffset(reader, "published_at"),
                reader.GetString("title_color"),
                reader.GetString("summary_color"),
                reader.GetString("body_color")));
        }

        return items;
    }

    public async ValueTask<LauncherReelPresentation> GetReelPresentationAsync(
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        bool enabled = false;
        await using (MySqlCommand settingsCommand = connection.CreateCommand())
        {
            settingsCommand.CommandText = """
SELECT reel_text_enabled
FROM launcher_presentation
WHERE presentation_key = 'default'
LIMIT 1;
""";
            object? value = await settingsCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            enabled = value is not null && value is not DBNull && Convert.ToBoolean(value);
        }

        List<LauncherReelText> items = [];
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT image_file, header_text, sub_text, header_size, sub_text_size,
       header_color, sub_text_color, is_enabled
FROM launcher_reel_text
ORDER BY image_file ASC;
""";
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new LauncherReelText(
                reader.GetString("image_file"),
                reader.GetString("header_text"),
                reader.GetString("sub_text"),
                reader.GetDouble("header_size"),
                reader.GetDouble("sub_text_size"),
                reader.GetString("header_color"),
                reader.GetString("sub_text_color"),
                reader.GetBoolean("is_enabled")));
        }

        return new LauncherReelPresentation(enabled, items);
    }

    public async ValueTask<IReadOnlyList<LauncherPatchFile>> GetPatchFilesAsync(
        string targetBootVersion,
        string targetGameVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetBootVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetGameVersion);

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT relative_path, size_bytes, crc32, sha256
FROM launcher_patch_files
WHERE is_active = 1
  AND target_boot_version = @target_boot_version
  AND target_game_version = @target_game_version
ORDER BY sort_order ASC, relative_path ASC;
""";
        command.Parameters.AddWithValue("@target_boot_version", targetBootVersion);
        command.Parameters.AddWithValue("@target_game_version", targetGameVersion);

        List<LauncherPatchFile> files = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            files.Add(new LauncherPatchFile(
                reader.GetString("relative_path"),
                reader.GetInt64("size_bytes"),
                reader.GetString("crc32"),
                ReadNullableString(reader, "sha256")));
        }

        return files;
    }

    public async ValueTask<IReadOnlyList<RuntimeArtifact>> GetRuntimeArtifactsAsync(
        string platformRid,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT name, version, platform_rid, runtime_kind, archive_url, archive_format, size_bytes, sha256,
       executable_relative_path, prefix_arch, environment_json, is_default, is_active, sort_order
FROM launcher_runtime_artifacts
WHERE is_active = 1
  AND (@platform_rid = '' OR platform_rid = @platform_rid)
ORDER BY is_default DESC, sort_order ASC, name ASC, version ASC;
""";
        command.Parameters.AddWithValue("@platform_rid", platformRid ?? "");

        List<RuntimeArtifact> artifacts = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            artifacts.Add(new RuntimeArtifact(
                reader.GetString("name"),
                reader.GetString("version"),
                reader.GetString("platform_rid"),
                reader.GetString("runtime_kind"),
                reader.GetString("archive_url"),
                reader.GetString("archive_format"),
                reader.GetInt64("size_bytes"),
                reader.GetString("sha256"),
                reader.GetString("executable_relative_path"),
                reader.GetString("prefix_arch"),
                ReadStringDictionary(reader, "environment_json"),
                reader.GetBoolean("is_default"),
                reader.GetBoolean("is_active"),
                reader.GetInt32("sort_order")));
        }

        return artifacts;
    }

    public async ValueTask<IReadOnlyList<UmbraFrameworkArtifact>> GetUmbraFrameworkArtifactsAsync(
        string platformRid,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT name, version, api_version, platform_rid, archive_url, archive_format, size_bytes, sha256,
       bootstrap_relative_path, framework_relative_path, supported_game_sha256_json,
       is_default, is_active, sort_order
FROM launcher_umbra_framework_artifacts
WHERE is_active = 1
  AND (@platform_rid = '' OR platform_rid = @platform_rid)
ORDER BY is_default DESC, sort_order ASC, name ASC, version ASC;
""";
        command.Parameters.AddWithValue("@platform_rid", platformRid ?? "");

        List<UmbraFrameworkArtifact> artifacts = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            artifacts.Add(new UmbraFrameworkArtifact(
                reader.GetString("name"),
                reader.GetString("version"),
                reader.GetString("api_version"),
                reader.GetString("platform_rid"),
                reader.GetString("archive_url"),
                reader.GetString("archive_format"),
                reader.GetInt64("size_bytes"),
                reader.GetString("sha256"),
                reader.GetString("bootstrap_relative_path"),
                reader.GetString("framework_relative_path"),
                ReadStringList(reader, "supported_game_sha256_json"),
                reader.GetBoolean("is_default"),
                reader.GetBoolean("is_active"),
                reader.GetInt32("sort_order")));
        }

        return artifacts;
    }

    public async ValueTask<UmbraPluginCatalog?> GetUmbraPluginCatalogAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        PluginRepositoryRow? repository = await ReadDefaultPluginRepositoryAsync(connection, cancellationToken)
            .ConfigureAwait(false);
        if (repository is null)
            return null;

        IReadOnlyList<UmbraPluginCatalogEntry> plugins = await ReadPluginsAsync(
            connection,
            repository.RepositoryId,
            cancellationToken).ConfigureAwait(false);
        return new UmbraPluginCatalog(repository.RepositoryName, plugins);
    }

    public async ValueTask<IReadOnlyList<UmbraPluginBlock>> GetUmbraPluginBlocksAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT plugin_key, repository_url, version, reason
FROM launcher_umbra_plugin_blocks
WHERE is_active = 1
ORDER BY plugin_key ASC, version ASC;
""";

        List<UmbraPluginBlock> blocks = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            blocks.Add(new UmbraPluginBlock(
                reader.GetString("plugin_key"),
                reader.GetString("repository_url"),
                ReadNullableString(reader, "version"),
                reader.GetString("reason")));
        }

        return blocks;
    }

    private static async Task<LauncherConfigRow?> ReadActiveConfigRowAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT config_key, service_version, server_name, server_status_url, news_url, patch_manifest_url,
       runtime_catalog_url, login_url, account_create_url, client_login_url, patch_base_url,
       target_boot_version, target_game_version, client_plugin_framework_catalog_url, plugin_blocklist_url
FROM launcher_config
WHERE is_active = 1
ORDER BY config_key ASC
LIMIT 1;
""";

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new LauncherConfigRow(
            reader.GetString("config_key"),
            reader.GetInt32("service_version"),
            reader.GetString("server_name"),
            ReadNullableString(reader, "server_status_url"),
            reader.GetString("news_url"),
            reader.GetString("patch_manifest_url"),
            ReadNullableString(reader, "runtime_catalog_url"),
            ReadNullableString(reader, "login_url"),
            ReadNullableString(reader, "account_create_url"),
            ReadNullableString(reader, "client_login_url"),
            ReadNullableString(reader, "patch_base_url"),
            reader.GetString("target_boot_version"),
            reader.GetString("target_game_version"),
            ReadNullableString(reader, "client_plugin_framework_catalog_url"),
            ReadNullableString(reader, "plugin_blocklist_url"));
    }

    private static async Task<IReadOnlyList<string>> ReadPluginCatalogUrlsAsync(
        MySqlConnection connection,
        string configKey,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT catalog_url
FROM launcher_config_plugin_catalogs
WHERE config_key = @config_key
ORDER BY sort_order ASC, catalog_url ASC;
""";
        command.Parameters.AddWithValue("@config_key", configKey);

        List<string> urls = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            urls.Add(reader.GetString("catalog_url"));

        return urls;
    }

    private static async Task<PluginRepositoryRow?> ReadDefaultPluginRepositoryAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT repository_id, repository_name
FROM launcher_umbra_plugin_repositories
WHERE is_active = 1
ORDER BY sort_order ASC, repository_id ASC
LIMIT 1;
""";

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new PluginRepositoryRow(
            reader.GetInt32("repository_id"),
            reader.GetString("repository_name"));
    }

    private static async Task<IReadOnlyList<UmbraPluginCatalogEntry>> ReadPluginsAsync(
        MySqlConnection connection,
        int repositoryId,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT plugin_key, name, version, api_version, author, description, download_url, size_bytes, sha256,
       minimum_framework_version, is_active
FROM launcher_umbra_plugins
WHERE repository_id = @repository_id
  AND is_active = 1
ORDER BY sort_order ASC, name ASC, plugin_key ASC;
""";
        command.Parameters.AddWithValue("@repository_id", repositoryId);

        List<UmbraPluginCatalogEntry> plugins = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            plugins.Add(new UmbraPluginCatalogEntry(
                reader.GetString("plugin_key"),
                reader.GetString("name"),
                reader.GetString("version"),
                reader.GetString("api_version"),
                reader.GetString("author"),
                reader.GetString("description"),
                reader.GetString("download_url"),
                reader.GetInt64("size_bytes"),
                reader.GetString("sha256"),
                reader.GetString("minimum_framework_version"),
                reader.GetBoolean("is_active")));
        }

        return plugins;
    }

    private static IReadOnlyDictionary<string, string> ReadStringDictionary(MySqlDataReader reader, string name)
    {
        string? json = ReadNullableString(reader, name);
        if (String.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? new Dictionary<string, string>();
    }

    private static IReadOnlyList<string> ReadStringList(MySqlDataReader reader, string name)
    {
        string? json = ReadNullableString(reader, name);
        if (String.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
    }

    private static string? ReadNullableString(MySqlDataReader reader, string name)
    {
        int ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTimeOffset ReadUtcDateTimeOffset(MySqlDataReader reader, string name)
    {
        DateTime value = reader.GetDateTime(name);
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private sealed record LauncherConfigRow(
        string ConfigKey,
        int ServiceVersion,
        string ServerName,
        string? ServerStatusUrl,
        string NewsUrl,
        string PatchManifestUrl,
        string? RuntimeCatalogUrl,
        string? LoginUrl,
        string? AccountCreateUrl,
        string? ClientLoginUrl,
        string? PatchBaseUrl,
        string TargetBootVersion,
        string TargetGameVersion,
        string? ClientPluginFrameworkCatalogUrl,
        string? PluginBlocklistUrl);

    private sealed record PluginRepositoryRow(
        int RepositoryId,
        string RepositoryName);
}
