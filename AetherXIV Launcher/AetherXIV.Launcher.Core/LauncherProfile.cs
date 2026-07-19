namespace AetherXIV.Launcher.Core;

public sealed record LauncherProfile(
    string ClientRootPath,
    string PatchLibraryRootPath,
    string LauncherServiceUrl,
    string PatchBaseUrl,
    ServerProfile ServerProfile,
    WineRuntimeProfile RuntimeProfile,
    RuntimeSelectionMode RuntimeMode = RuntimeSelectionMode.AutomaticManaged,
    ClientLaunchHelperMode LaunchHelperMode = ClientLaunchHelperMode.Automatic,
    ClientGraphicsTarget GraphicsTarget = ClientGraphicsTarget.OpenGLCompatibility,
    string SavedUsername = "",
    bool RememberUsername = false,
    UmbraSettings? Umbra = null)
{
    public const string DemiDevUnitLauncherServiceUrl = "https://launcher.dev.demidevunit.com/launcher";

    public static LauncherProfile LocalDefault() => new(
        "",
        "",
        "http://127.0.0.1:8080/launcher",
        "",
        ServerProfile.LocalDefault(),
        LauncherPlatform.Current.RequiresCompatibilityRuntime
            ? WineRuntimeProfile.WinePrefix(
                "AetherXIV Managed",
                RuntimeInstallStore.ManagedPrefixPath,
                "wine")
            : WineRuntimeProfile.NativeWindows(),
        LauncherPlatform.Current.RequiresCompatibilityRuntime
            ? RuntimeSelectionMode.AutomaticManaged
            : RuntimeSelectionMode.CustomRuntime,
        ClientLaunchHelperMode.Automatic,
        ClientGraphicsTarget.OpenGLCompatibility,
        "",
        false,
        UmbraSettings.Default);

    public static LauncherProfile DemiDevUnitDefault() => LocalDefault() with
    {
        LauncherServiceUrl = DemiDevUnitLauncherServiceUrl,
        PatchBaseUrl = "",
        ServerProfile = ServerProfile.DemiDevUnitDefault()
    };

    public UmbraSettings EffectiveUmbra => (Umbra ?? UmbraSettings.Default).Normalize();
}
