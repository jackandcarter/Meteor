namespace Aether.Umbra.Framework;

public sealed record UmbraClientBuildProfile(
    string Id,
    string DisplayVersion,
    string ExecutableFileName,
    string ExecutableSha256,
    string Architecture,
    uint PreferredImageBase);

/// <summary>
/// Exact executable identities for which Umbra may activate client interop.
/// Unknown hashes intentionally resolve to no profile.
/// </summary>
public static class UmbraClientBuildCatalog
{
    public const string Legacy123bBuildId = "ffxiv-1.23b-2012.09.19.0001";

    public static UmbraClientBuildProfile Legacy123b { get; } = new(
        Legacy123bBuildId,
        "Final Fantasy XIV 1.23b / 2012.09.19.0001",
        "ffxivgame.exe",
        "9341f2b4567440b310a4d494f5cc5599ca334ba51c8042247317ff466492f2e9",
        "x86",
        0x00400000);

    public static IReadOnlyList<UmbraClientBuildProfile> Profiles { get; } =
        Array.AsReadOnly<UmbraClientBuildProfile>([Legacy123b]);

    public static bool TryResolveSha256(string? sha256, out UmbraClientBuildProfile? profile)
    {
        profile = null;
        if (string.IsNullOrWhiteSpace(sha256))
            return false;

        string normalized = sha256.Trim();
        profile = Profiles.FirstOrDefault(candidate =>
            string.Equals(candidate.ExecutableSha256, normalized, StringComparison.OrdinalIgnoreCase));
        return profile is not null;
    }
}
