using AetherXIV.Data;
using MySqlConnector;

namespace AetherXIV.Operator;

public sealed record LauncherNewsAdminItem(
    int Id,
    string Title,
    string Summary,
    string Body,
    string BannerUrl,
    string LinkUrl,
    DateTimeOffset PublishedAt,
    bool IsActive,
    int SortOrder,
    string TitleColor,
    string SummaryColor,
    string BodyColor);

public sealed record LauncherReelTextAdminItem(
    string ImageFile,
    string HeaderText,
    string SubText,
    double HeaderSize,
    double SubTextSize,
    string HeaderColor,
    string SubTextColor,
    bool IsEnabled);

public sealed record LauncherPresentationAdminState(
    bool ReelTextEnabled,
    IReadOnlyList<LauncherReelTextAdminItem> ReelTextItems);

public sealed class LauncherContentAdminService
{
    public async Task<IReadOnlyList<LauncherNewsAdminItem>> GetNewsAsync(
        AetherXivDatabaseConfig database,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await OpenAsync(database, cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT news_id, title, summary, body, banner_url, link_url, published_at,
       is_active, sort_order, title_color, summary_color, body_color
FROM launcher_news
ORDER BY sort_order ASC, published_at DESC, news_id DESC;
""";

        List<LauncherNewsAdminItem> items = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            DateTime publishedAt = DateTime.SpecifyKind(reader.GetDateTime("published_at"), DateTimeKind.Utc);
            items.Add(new LauncherNewsAdminItem(
                reader.GetInt32("news_id"),
                reader.GetString("title"),
                reader.GetString("summary"),
                ReadString(reader, "body"),
                ReadString(reader, "banner_url"),
                ReadString(reader, "link_url"),
                new DateTimeOffset(publishedAt),
                reader.GetBoolean("is_active"),
                reader.GetInt32("sort_order"),
                reader.GetString("title_color"),
                reader.GetString("summary_color"),
                reader.GetString("body_color")));
        }

        return items;
    }

    public async Task<int> SaveNewsAsync(
        AetherXivDatabaseConfig database,
        LauncherNewsAdminItem item,
        CancellationToken cancellationToken = default)
    {
        LauncherNewsAdminItem normalized = ValidateNews(item);
        await using MySqlConnection connection = await OpenAsync(database, cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = normalized.Id > 0
            ? """
UPDATE launcher_news
SET title = @title, summary = @summary, body = @body, banner_url = @banner_url,
    link_url = @link_url, published_at = @published_at, is_active = @is_active,
    sort_order = @sort_order, title_color = @title_color,
    summary_color = @summary_color, body_color = @body_color
WHERE news_id = @news_id;
"""
            : """
INSERT INTO launcher_news
  (title, summary, body, banner_url, link_url, published_at, is_active, sort_order,
   title_color, summary_color, body_color)
VALUES
  (@title, @summary, @body, @banner_url, @link_url, @published_at, @is_active, @sort_order,
   @title_color, @summary_color, @body_color);
""";
        AddNewsParameters(command, normalized);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return normalized.Id > 0 ? normalized.Id : checked((int)command.LastInsertedId);
    }

    public async Task DeleteNewsAsync(
        AetherXivDatabaseConfig database,
        int newsId,
        CancellationToken cancellationToken = default)
    {
        if (newsId <= 0)
            throw new ArgumentOutOfRangeException(nameof(newsId));

        await using MySqlConnection connection = await OpenAsync(database, cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM launcher_news WHERE news_id = @news_id;";
        command.Parameters.AddWithValue("@news_id", newsId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<LauncherPresentationAdminState> GetPresentationAsync(
        AetherXivDatabaseConfig database,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await OpenAsync(database, cancellationToken).ConfigureAwait(false);
        bool enabled;
        await using (MySqlCommand settings = connection.CreateCommand())
        {
            settings.CommandText = "SELECT reel_text_enabled FROM launcher_presentation WHERE presentation_key = 'default' LIMIT 1;";
            object? value = await settings.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            enabled = value is not null && value is not DBNull && Convert.ToBoolean(value);
        }

        List<LauncherReelTextAdminItem> items = [];
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
            items.Add(new LauncherReelTextAdminItem(
                reader.GetString("image_file"),
                reader.GetString("header_text"),
                reader.GetString("sub_text"),
                reader.GetDouble("header_size"),
                reader.GetDouble("sub_text_size"),
                reader.GetString("header_color"),
                reader.GetString("sub_text_color"),
                reader.GetBoolean("is_enabled")));
        }

        return new LauncherPresentationAdminState(enabled, items);
    }

    public async Task SetReelTextEnabledAsync(
        AetherXivDatabaseConfig database,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await OpenAsync(database, cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO launcher_presentation (presentation_key, reel_text_enabled)
VALUES ('default', @enabled)
ON DUPLICATE KEY UPDATE reel_text_enabled = VALUES(reel_text_enabled);
""";
        command.Parameters.AddWithValue("@enabled", enabled);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveReelTextAsync(
        AetherXivDatabaseConfig database,
        LauncherReelTextAdminItem item,
        CancellationToken cancellationToken = default)
    {
        LauncherReelTextAdminItem normalized = ValidateReelText(item);
        await using MySqlConnection connection = await OpenAsync(database, cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO launcher_reel_text
  (image_file, header_text, sub_text, header_size, sub_text_size,
   header_color, sub_text_color, is_enabled)
VALUES
  (@image_file, @header_text, @sub_text, @header_size, @sub_text_size,
   @header_color, @sub_text_color, @is_enabled)
ON DUPLICATE KEY UPDATE
  header_text = VALUES(header_text), sub_text = VALUES(sub_text),
  header_size = VALUES(header_size), sub_text_size = VALUES(sub_text_size),
  header_color = VALUES(header_color), sub_text_color = VALUES(sub_text_color),
  is_enabled = VALUES(is_enabled);
""";
        command.Parameters.AddWithValue("@image_file", normalized.ImageFile);
        command.Parameters.AddWithValue("@header_text", normalized.HeaderText);
        command.Parameters.AddWithValue("@sub_text", normalized.SubText);
        command.Parameters.AddWithValue("@header_size", normalized.HeaderSize);
        command.Parameters.AddWithValue("@sub_text_size", normalized.SubTextSize);
        command.Parameters.AddWithValue("@header_color", normalized.HeaderColor);
        command.Parameters.AddWithValue("@sub_text_color", normalized.SubTextColor);
        command.Parameters.AddWithValue("@is_enabled", normalized.IsEnabled);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteReelTextAsync(
        AetherXivDatabaseConfig database,
        string imageFile,
        CancellationToken cancellationToken = default)
    {
        string normalized = Path.GetFileName(imageFile.Trim());
        if (String.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("A reel image file is required.", nameof(imageFile));

        await using MySqlConnection connection = await OpenAsync(database, cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM launcher_reel_text WHERE image_file = @image_file;";
        command.Parameters.AddWithValue("@image_file", normalized);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public static string NormalizeColor(string? value, string fallback)
    {
        string color = (value ?? "").Trim().ToUpperInvariant();
        if (color.Length is 7 or 9 && color[0] == '#' && color[1..].All(Uri.IsHexDigit))
            return color;

        return fallback;
    }

    private static LauncherNewsAdminItem ValidateNews(LauncherNewsAdminItem item)
    {
        string title = item.Title.Trim();
        string summary = item.Summary.Trim();
        if (String.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Enter a title before saving the post.");
        if (String.IsNullOrWhiteSpace(summary))
            throw new InvalidOperationException("Enter a summary before saving the post.");
        if (title.Length > 160)
            throw new InvalidOperationException("The title must be 160 characters or fewer.");
        if (summary.Length > 500)
            throw new InvalidOperationException("The summary must be 500 characters or fewer.");

        return item with
        {
            Title = title,
            Summary = summary,
            Body = item.Body.Trim(),
            BannerUrl = item.BannerUrl.Trim(),
            LinkUrl = item.LinkUrl.Trim(),
            TitleColor = NormalizeColor(item.TitleColor, "#F2F4FA"),
            SummaryColor = NormalizeColor(item.SummaryColor, "#D6DCE3"),
            BodyColor = NormalizeColor(item.BodyColor, "#AEB7C2")
        };
    }

    private static LauncherReelTextAdminItem ValidateReelText(LauncherReelTextAdminItem item)
    {
        string imageFile = Path.GetFileName(item.ImageFile.Trim());
        if (String.IsNullOrWhiteSpace(imageFile))
            throw new InvalidOperationException("Choose a reel image before saving.");
        if (item.HeaderText.Length > 160 || item.SubText.Length > 300)
            throw new InvalidOperationException("Reel header or subtext is too long.");

        return item with
        {
            ImageFile = imageFile,
            HeaderText = item.HeaderText.Trim(),
            SubText = item.SubText.Trim(),
            HeaderSize = Math.Clamp(item.HeaderSize, 12, 72),
            SubTextSize = Math.Clamp(item.SubTextSize, 10, 48),
            HeaderColor = NormalizeColor(item.HeaderColor, "#FFFFFFFF"),
            SubTextColor = NormalizeColor(item.SubTextColor, "#FFD7E0EE")
        };
    }

    private static async Task<MySqlConnection> OpenAsync(
        AetherXivDatabaseConfig database,
        CancellationToken cancellationToken)
    {
        MariaDbOptions options = new(database.Host, database.Port, database.Name, database.User, database.Password);
        MySqlConnection connection = new(options.ToConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static void AddNewsParameters(MySqlCommand command, LauncherNewsAdminItem item)
    {
        command.Parameters.AddWithValue("@news_id", item.Id);
        command.Parameters.AddWithValue("@title", item.Title);
        command.Parameters.AddWithValue("@summary", item.Summary);
        command.Parameters.AddWithValue("@body", String.IsNullOrWhiteSpace(item.Body) ? DBNull.Value : item.Body);
        command.Parameters.AddWithValue("@banner_url", String.IsNullOrWhiteSpace(item.BannerUrl) ? DBNull.Value : item.BannerUrl);
        command.Parameters.AddWithValue("@link_url", String.IsNullOrWhiteSpace(item.LinkUrl) ? DBNull.Value : item.LinkUrl);
        command.Parameters.AddWithValue("@published_at", item.PublishedAt.UtcDateTime);
        command.Parameters.AddWithValue("@is_active", item.IsActive);
        command.Parameters.AddWithValue("@sort_order", item.SortOrder);
        command.Parameters.AddWithValue("@title_color", item.TitleColor);
        command.Parameters.AddWithValue("@summary_color", item.SummaryColor);
        command.Parameters.AddWithValue("@body_color", item.BodyColor);
    }

    private static string ReadString(MySqlDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? "" : reader.GetString(name);
}
