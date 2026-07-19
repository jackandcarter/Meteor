using System.Text.Json;

namespace Aether.Umbra.Framework;

public sealed record UmbraStoreEntry(
    string Id,
    string Name,
    string Version,
    string ApiVersion,
    string DownloadUrl,
    long SizeBytes,
    string Sha256,
    string MinimumFrameworkVersion,
    string RepositoryUrl,
    string Source,
    string Author,
    string Description,
    string Punchline,
    string? RepoUrl,
    string? IconUrl,
    IReadOnlyList<string> ImageUrls,
    string? Changelog,
    string? LastUpdate,
    bool IsHidden,
    bool TestingOnly,
    string? Entry)
{
    public bool IsInstallable =>
        !IsHidden
        && !TestingOnly
        && !string.IsNullOrWhiteSpace(Id)
        && !string.IsNullOrWhiteSpace(Name)
        && !string.IsNullOrWhiteSpace(Version)
        && !string.IsNullOrWhiteSpace(ApiVersion)
        && !string.IsNullOrWhiteSpace(DownloadUrl)
        && SizeBytes > 0
        && !string.IsNullOrWhiteSpace(Sha256)
        && !string.IsNullOrWhiteSpace(MinimumFrameworkVersion);

    public void ValidateInstallable()
    {
        if (!IsInstallable)
            throw new InvalidDataException($"Umbra store entry is not installable: {Id}");
        if (Sha256.Length != 64 || Sha256.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException($"Umbra store entry has an invalid SHA256: {Id}");
    }

    public UmbraPluginManifest ToManifest(bool enabled = false)
    {
        return new UmbraPluginManifest(
            Id,
            Name,
            Version,
            ApiVersion,
            string.IsNullOrWhiteSpace(Entry) ? $"{Id}.dll" : Entry,
            MinimumFrameworkVersion,
            enabled);
    }

    public static IReadOnlyList<UmbraStoreEntry> ParseRepository(
        string json,
        UmbraRepositorySource repository)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Umbra plugin repository must be a JSON array.");

        List<UmbraStoreEntry> entries = new();
        foreach (JsonElement element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
                continue;

            entries.Add(FromJsonObject(element, repository));
        }

        return entries
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(entry => entry.Version, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static UmbraStoreEntry FromJsonObject(JsonElement element, UmbraRepositorySource repository)
    {
        string id = ReadString(element, "id", "plugin_id", "InternalName", "internal_name");
        string name = ReadString(element, "name", "Name");
        string version = ReadString(element, "version", "AssemblyVersion", "assembly_version");
        string apiVersion = ReadString(element, "api_version", "DalamudApiLevel", "dalamud_api_level");
        string downloadUrl = ReadString(element, "download_url", "DownloadLinkInstall", "download_link_install");
        long sizeBytes = ReadLong(element, "size_bytes", "SizeBytes", "size");
        string sha256 = ReadString(element, "sha256", "Sha256", "hash");
        string minimumFrameworkVersion = ReadString(
            element,
            "minimum_framework_version",
            "MinimumFrameworkVersion",
            "minimumFrameworkVersion");

        return new UmbraStoreEntry(
            id,
            name,
            version,
            apiVersion,
            downloadUrl,
            sizeBytes,
            sha256,
            minimumFrameworkVersion,
            repository.Url,
            repository.Source,
            ReadString(element, "author", "Author"),
            ReadString(element, "description", "Description"),
            ReadString(element, "punchline", "Punchline"),
            ReadOptionalString(element, "repo_url", "RepoUrl"),
            ReadOptionalString(element, "icon_url", "IconUrl"),
            ReadStringArray(element, "image_urls", "ImageUrls"),
            ReadOptionalString(element, "changelog", "Changelog"),
            ReadOptionalString(element, "last_update", "LastUpdate"),
            ReadBoolean(element, "is_hidden", "IsHide", "is_hide"),
            ReadBoolean(element, "testing_only", "IsTestingExclusive", "is_testing_exclusive"),
            ReadOptionalString(element, "entry", "Entry", "AssemblyPath"));
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        return ReadOptionalString(element, names) ?? "";
    }

    private static string? ReadOptionalString(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
                continue;

            if (value.ValueKind == JsonValueKind.String)
                return value.GetString()?.Trim();

            if (value.ValueKind == JsonValueKind.Number)
                return value.GetRawText();

            if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                return value.GetBoolean() ? "true" : "false";
        }

        return null;
    }

    private static long ReadLong(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number))
                return number;

            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out long parsed))
                return parsed;
        }

        return 0;
    }

    private static bool ReadBoolean(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            if (!element.TryGetProperty(name, out JsonElement value))
                continue;

            if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                return value.GetBoolean();

            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out bool parsed))
                return parsed;
        }

        return false;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, params string[] names)
    {
        foreach (string name in names)
        {
            if (!element.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
                continue;

            return value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToArray();
        }

        return Array.Empty<string>();
    }
}
