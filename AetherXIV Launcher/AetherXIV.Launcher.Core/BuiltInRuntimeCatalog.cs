namespace AetherXIV.Launcher.Core;

public static class BuiltInRuntimeCatalog
{
    private static readonly IReadOnlyDictionary<string, RuntimeArtifact> Artifacts =
        new Dictionary<string, RuntimeArtifact>(StringComparer.OrdinalIgnoreCase)
        {
            ["osx-arm64"] = CreateMacOsArtifact("osx-arm64"),
            ["osx-x64"] = CreateMacOsArtifact("osx-x64"),
            ["linux-x64"] = new RuntimeArtifact(
                "AetherXIV Wine (Linux)",
                "11.0",
                "linux-x64",
                "wine",
                "https://github.com/Kron4ek/Wine-Builds/releases/download/11.0/wine-11.0-amd64-wow64.tar.xz",
                "tar.xz",
                73_144_724,
                "39574efa1132c3ca0d5c77dd2eddbe4a49cca0d6cc2c290ff4924493a1c40314",
                "wine-11.0-amd64-wow64/bin/wine",
                "win64",
                RuntimeEnvironment(),
                true,
                true,
                0)
        };

    public static RuntimeArtifact? Find(string runtimeIdentifier)
    {
        return String.IsNullOrWhiteSpace(runtimeIdentifier)
            ? null
            : Artifacts.GetValueOrDefault(runtimeIdentifier);
    }

    public static RuntimeCatalog ForPlatform(string runtimeIdentifier)
    {
        RuntimeArtifact? artifact = Find(runtimeIdentifier);
        return new RuntimeCatalog(
            runtimeIdentifier,
            artifact is null ? Array.Empty<RuntimeArtifact>() : new[] { artifact });
    }

    private static RuntimeArtifact CreateMacOsArtifact(string runtimeIdentifier)
    {
        return new RuntimeArtifact(
            "AetherXIV Wine Stable (macOS)",
            "11.0_1",
            runtimeIdentifier,
            "wine",
            "https://github.com/Gcenx/macOS_Wine_builds/releases/download/11.0_1/wine-stable-11.0_1-osx64.tar.xz",
            "tar.xz",
            185_303_032,
            "b50dc50ec7f41d58b115a6b685d4d1315ba3c797bd3aa0f49213f2703cb82388",
            "Wine Stable.app/Contents/Resources/wine/bin/wine",
            "win64",
            RuntimeEnvironment(),
            true,
            true,
            0);
    }

    private static IReadOnlyDictionary<string, string> RuntimeEnvironment() =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WINEDEBUG"] = "-all"
        };
}
