namespace AetherXIV.Launcher.Core;

public static class RuntimeProfileResolver
{
    public static WineRuntimeProfile Resolve(
        RuntimeSelectionMode mode,
        ManagedRuntimeInstall? managedInstall,
        IEnumerable<RuntimeCandidate> detectedCandidates,
        WineRuntimeProfile customProfile,
        string managedPrefixPath)
    {
        ArgumentNullException.ThrowIfNull(detectedCandidates);
        ArgumentNullException.ThrowIfNull(customProfile);

        if (mode == RuntimeSelectionMode.CustomRuntime)
            return customProfile;

        if (mode == RuntimeSelectionMode.AutomaticManaged && managedInstall is not null)
            return managedInstall.ToWineRuntimeProfile(managedPrefixPath);

        RuntimeCandidate? detected = detectedCandidates.FirstOrDefault();
        if (detected is null)
            return customProfile;

        return CandidateToProfile(detected, managedPrefixPath);
    }

    public static WineRuntimeProfile CandidateToProfile(RuntimeCandidate candidate, string managedPrefixPath)
    {
        if (candidate.Kind == WineRuntimeKind.WinePrefix)
        {
            string prefixPath = string.IsNullOrWhiteSpace(candidate.BottleOrPrefix)
                ? managedPrefixPath
                : candidate.BottleOrPrefix;
            return WineRuntimeProfile.WinePrefix(candidate.Name, prefixPath, candidate.Command);
        }

        if (candidate.Kind == WineRuntimeKind.WhiskyBottle
            && !string.IsNullOrWhiteSpace(candidate.BottleOrPrefix)
            && WhiskyRuntimeEnvironment.TryCreateWineProfile(
                candidate.Command,
                candidate.BottleOrPrefix,
                out WineRuntimeProfile whiskyWineProfile,
                out _))
        {
            return whiskyWineProfile;
        }

        return candidate.ToProfile();
    }
}
