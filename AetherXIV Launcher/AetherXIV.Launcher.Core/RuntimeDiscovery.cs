namespace AetherXIV.Launcher.Core;

public sealed record RuntimeCandidate(
    string Name,
    WineRuntimeKind Kind,
    string Command,
    string? BottleOrPrefix,
    string Source)
{
    public WineRuntimeProfile ToProfile()
    {
        return Kind switch
        {
            WineRuntimeKind.CrossOverBottle => WineRuntimeProfile.CrossOverBottle(Name, BottleOrPrefix ?? "AetherXIV", Command),
            WineRuntimeKind.WinePrefix => WineRuntimeProfile.WinePrefix(Name, BottleOrPrefix ?? "", Command),
            WineRuntimeKind.WhiskyBottle => WineRuntimeProfile.WhiskyBottle(Name, BottleOrPrefix ?? "", Command),
            _ => WineRuntimeProfile.Custom(Name, Command)
        };
    }
}

public static class RuntimeDiscovery
{
    private const string HomebrewWineStableCommand = "/Applications/Wine Stable.app/Contents/Resources/wine/bin/wine";

    public static IReadOnlyList<RuntimeCandidate> Discover() =>
        Discover(File.Exists, Directory.Exists, _ => Array.Empty<string>(), _ => Array.Empty<string>());

    public static IReadOnlyList<RuntimeCandidate> Discover(Func<string, bool> fileExists, Func<string, bool> directoryExists)
    {
        return Discover(fileExists, directoryExists, _ => Array.Empty<string>(), _ => Array.Empty<string>());
    }

    public static IReadOnlyList<RuntimeCandidate> Discover(
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists,
        Func<string, IReadOnlyList<string>> whiskyBottleLister)
    {
        return Discover(fileExists, directoryExists, whiskyBottleLister, _ => Array.Empty<string>());
    }

    public static IReadOnlyList<RuntimeCandidate> Discover(
        Func<string, bool> fileExists,
        Func<string, bool> directoryExists,
        Func<string, IReadOnlyList<string>> whiskyBottleLister,
        Func<string, IReadOnlyList<string>> homeWinePrefixLister,
        string? executableSearchPath = null)
    {
        ArgumentNullException.ThrowIfNull(fileExists);
        ArgumentNullException.ThrowIfNull(directoryExists);
        ArgumentNullException.ThrowIfNull(whiskyBottleLister);
        ArgumentNullException.ThrowIfNull(homeWinePrefixLister);

        List<RuntimeCandidate> candidates = new();

        AddIfFileExists(
            candidates,
            fileExists,
            "Homebrew Wine Stable",
            WineRuntimeKind.WinePrefix,
            HomebrewWineStableCommand,
            null,
            "Recognized Wine Stable app");

        AddWineCommandsFromPath(candidates, fileExists, executableSearchPath);

        return candidates;
    }

    private static void AddWineCommandsFromPath(
        List<RuntimeCandidate> candidates,
        Func<string, bool> fileExists,
        string? executableSearchPath)
    {
        AddIfCommandExists(
            candidates,
            fileExists,
            "System Wine",
            WineRuntimeKind.WinePrefix,
            "wine",
            null,
            "PATH",
            executableSearchPath);

        AddIfCommandExists(
            candidates,
            fileExists,
            "System Wine 64",
            WineRuntimeKind.WinePrefix,
            "wine64",
            null,
            "PATH",
            executableSearchPath);
    }

    private static void AddIfCommandExists(
        List<RuntimeCandidate> candidates,
        Func<string, bool> fileExists,
        string name,
        WineRuntimeKind kind,
        string command,
        string? bottleOrPrefix,
        string source,
        string? executableSearchPath)
    {
        string? resolvedCommand = ResolveExecutable(command, fileExists, executableSearchPath);
        if (resolvedCommand is null)
            return;

        if (candidates.Any(candidate => string.Equals(candidate.Command, resolvedCommand, StringComparison.Ordinal)))
            return;

        candidates.Add(new RuntimeCandidate(name, kind, resolvedCommand, bottleOrPrefix, source));
    }

    private static string? ResolveExecutable(
        string command,
        Func<string, bool> fileExists,
        string? executableSearchPath)
    {
        if (Path.IsPathFullyQualified(command))
            return fileExists(command) ? command : null;

        string searchPath = executableSearchPath ?? Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string directory in searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(directory, command);
            if (fileExists(candidate))
                return candidate;
        }

        return null;
    }

    private static void AddIfFileExists(
        List<RuntimeCandidate> candidates,
        Func<string, bool> fileExists,
        string name,
        WineRuntimeKind kind,
        string command,
        string? bottleOrPrefix,
        string source)
    {
        if (!fileExists(command))
            return;

        candidates.Add(new RuntimeCandidate(name, kind, command, bottleOrPrefix, source));
    }

    public static IReadOnlyList<string> ParseWhiskyBottleNames(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return Array.Empty<string>();

        List<string> bottles = new();
        using StringReader reader = new(output);
        while (reader.ReadLine() is { } line)
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith('|') || !trimmed.EndsWith('|'))
                continue;
            if (trimmed.Contains("Name", StringComparison.Ordinal)
                && trimmed.Contains("Windows Version", StringComparison.Ordinal))
                continue;

            string[] parts = trimmed
                .Split('|')
                .Select(part => part.Trim())
                .ToArray();
            if (parts.Length < 4)
                continue;

            string bottleName = parts[1];
            if (string.IsNullOrWhiteSpace(bottleName) || bottleName.StartsWith("-", StringComparison.Ordinal))
                continue;

            bottles.Add(bottleName);
        }

        return bottles.Distinct(StringComparer.Ordinal).ToArray();
    }
}
