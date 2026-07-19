namespace AetherXIV.Launcher.Core;

public sealed record ManagedRuntimeInstall(
    string Name,
    string Version,
    string PlatformRid,
    string RuntimeKind,
    string InstallPath,
    string ExecutablePath,
    string PrefixArch,
    IReadOnlyDictionary<string, string> Environment,
    DateTimeOffset InstalledAt)
{
    public WineRuntimeProfile ToWineRuntimeProfile(string prefixPath)
    {
        return WineRuntimeProfile.WinePrefix(
            $"{Name} {Version}",
            prefixPath,
            ExecutablePath,
            Environment);
    }
}
