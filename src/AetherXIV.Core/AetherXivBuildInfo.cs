namespace AetherXIV.Core;

public static class AetherXivBuildInfo
{
    public const string ProductVersion = "2.0";
    public const int BuildNumber = 21990;

    public static string VersionText => $"v{ProductVersion}";
    public static string BuildText => $"Build {BuildNumber}";
}
