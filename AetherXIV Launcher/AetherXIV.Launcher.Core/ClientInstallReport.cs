namespace AetherXIV.Launcher.Core;

public enum ClientInstallState
{
    Missing,
    BaseInstall,
    PatchRequired,
    Ready123b,
    UnknownVersion
}

public sealed record ClientVersionInfo(string? BootVersion, string? GameVersion)
{
    public const string BaseVersion = "2010.07.10.0000";
    public const string TargetBootVersion = "2010.09.18.0000";
    public const string TargetGameVersion = "2012.09.19.0001";

    public bool IsBaseInstall => BootVersion == BaseVersion && GameVersion == BaseVersion;

    public bool IsTargetVersion => BootVersion == TargetBootVersion && GameVersion == TargetGameVersion;

    public string DisplayText => $"boot={BootVersion ?? "missing"}, game={GameVersion ?? "missing"}";
}

public sealed record ClientInstallReport(
    string RootPath,
    bool RootExists,
    bool HasBootExecutable,
    bool HasUpdaterExecutable,
    bool HasDirectGameExecutable,
    bool HasConfigExecutable,
    bool HasStaticActors,
    ClientVersionInfo Version,
    ClientInstallState State,
    IReadOnlyList<string> RequiredActions)
{
    public bool IsLaunchReady => State == ClientInstallState.Ready123b && HasDirectGameExecutable && HasStaticActors;

    public static ClientInstallReport Create(ClientInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);

        bool rootExists = Directory.Exists(install.RootPath);
        bool hasBootExecutable = File.Exists(install.BootExecutablePath);
        bool hasUpdaterExecutable = File.Exists(install.UpdaterExecutablePath);
        bool hasDirectGameExecutable = File.Exists(install.DirectGameExecutablePath);
        bool hasConfigExecutable = File.Exists(install.ConfigExecutablePath);
        bool hasStaticActors = install.HasStaticActors;
        ClientVersionInfo version = new(
            ReadVersionFile(install.BootVersionPath),
            ReadVersionFile(install.GameVersionPath));

        List<string> actions = new();
        ClientInstallState state;

        if (!rootExists)
        {
            state = ClientInstallState.Missing;
            actions.Add("Select the FINAL FANTASY XIV 1.x client root.");
        }
        else if (version.IsTargetVersion && hasDirectGameExecutable)
        {
            state = ClientInstallState.Ready123b;
        }
        else if (version.IsBaseInstall)
        {
            state = ClientInstallState.BaseInstall;
            actions.Add("Apply the 1.x patch chain to reach 2012.09.19.0001.");
        }
        else if (IsKnownOlderVersion(version))
        {
            state = ClientInstallState.PatchRequired;
            actions.Add("Continue patching until game.ver is 2012.09.19.0001.");
        }
        else
        {
            state = ClientInstallState.UnknownVersion;
            actions.Add("Verify boot.ver and game.ver before launching.");
        }

        if (rootExists && !hasBootExecutable)
            actions.Add("ffxivboot.exe is missing.");

        if (rootExists && !hasUpdaterExecutable)
            actions.Add("ffxivupdater.exe is missing.");

        if (rootExists && !hasDirectGameExecutable)
            actions.Add("ffxivgame.exe is missing until the game patch chain is applied.");

        if (rootExists && !hasStaticActors)
            actions.Add("Select a client folder containing client/script/rq9q1797qvs.san or staticactors.bin.");

        return new ClientInstallReport(
            install.RootPath,
            rootExists,
            hasBootExecutable,
            hasUpdaterExecutable,
            hasDirectGameExecutable,
            hasConfigExecutable,
            hasStaticActors,
            version,
            state,
            actions);
    }

    private static bool IsKnownOlderVersion(ClientVersionInfo version)
    {
        return !string.IsNullOrWhiteSpace(version.BootVersion)
            && !string.IsNullOrWhiteSpace(version.GameVersion)
            && !version.IsTargetVersion;
    }

    private static string? ReadVersionFile(string path)
    {
        if (!File.Exists(path))
            return null;

        string value = File.ReadAllText(path).Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
