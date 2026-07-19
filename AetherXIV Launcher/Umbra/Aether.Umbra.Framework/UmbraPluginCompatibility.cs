namespace Aether.Umbra.Framework;

public static class UmbraPluginCompatibility
{
    public static bool SupportsApi(string? requestedApiVersion)
    {
        if (!TryParseVersion(requestedApiVersion, out Version? requested)
            || !TryParseVersion(UmbraFrameworkInfo.ApiVersion, out Version? current))
        {
            return false;
        }

        return requested!.Major == current!.Major
            && requested.Minor <= current.Minor;
    }

    public static bool SupportsFramework(string? minimumFrameworkVersion)
    {
        if (!TryParseVersion(minimumFrameworkVersion, out Version? minimum)
            || !TryParseVersion(UmbraFrameworkInfo.Version, out Version? current))
        {
            return false;
        }

        return current! >= minimum!;
    }

    public static void Validate(UmbraPluginManifest manifest)
    {
        if (!SupportsApi(manifest.ApiVersion))
        {
            throw new InvalidDataException(
                $"Umbra plugin {manifest.Id} requires API {manifest.ApiVersion}; this runtime provides API {UmbraFrameworkInfo.ApiVersion}.");
        }

        if (!SupportsFramework(manifest.MinimumFrameworkVersion))
        {
            throw new InvalidDataException(
                $"Umbra plugin {manifest.Id} requires framework {manifest.MinimumFrameworkVersion} or later; this runtime is {UmbraFrameworkInfo.Version}.");
        }
    }

    private static bool TryParseVersion(string? value, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim();
        int suffix = normalized.IndexOfAny(['-', '+']);
        if (suffix >= 0)
            normalized = normalized[..suffix];

        return Version.TryParse(normalized, out version);
    }
}
