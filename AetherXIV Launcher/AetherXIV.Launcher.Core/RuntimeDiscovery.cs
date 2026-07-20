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

    public static IReadOnlyList<RuntimeCandidate> Discover()
    {
        List<RuntimeCandidate> candidates = Discover(
            File.Exists,
            Directory.Exists,
            _ => Array.Empty<string>(),
            _ => Array.Empty<string>(),
            BuildExecutableSearchPath()).ToList();
        AddUserInstalledWineRunners(candidates);
        return candidates;
    }

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

    public static string? ResolveExecutable(
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

    public static string BuildExecutableSearchPath(string? inheritedPath = null)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] standardDirectories =
        [
            "/usr/bin",
            "/usr/local/bin",
            "/opt/homebrew/bin",
            "/opt/local/bin",
            string.IsNullOrWhiteSpace(home) ? "" : Path.Combine(home, ".local", "bin"),
            string.IsNullOrWhiteSpace(home) ? "" : Path.Combine(home, ".nix-profile", "bin")
        ];
        string path = inheritedPath ?? Environment.GetEnvironmentVariable("PATH") ?? "";
        return string.Join(
            Path.PathSeparator,
            path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Concat(standardDirectories)
                .Where(directory => !string.IsNullOrWhiteSpace(directory))
                .Distinct(StringComparer.Ordinal));
    }

    private static void AddUserInstalledWineRunners(List<RuntimeCandidate> candidates)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            return;

        string[] roots =
        [
            Path.Combine(home, ".local", "share", "lutris", "runners", "wine"),
            Path.Combine(home, ".local", "share", "bottles", "runners"),
            Path.Combine(home, ".steam", "root", "compatibilitytools.d"),
            Path.Combine(home, ".local", "share", "Steam", "compatibilitytools.d")
        ];
        foreach (string root in roots)
        {
            if (!Directory.Exists(root))
                continue;

            try
            {
                IEnumerable<string> runnerCommands = new[] { "wine", "wine64" }
                    .SelectMany(fileName => Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories));
                foreach (string command in runnerCommands)
                {
                    string normalized = Path.GetFullPath(command);
                    if (candidates.Any(candidate => string.Equals(candidate.Command, normalized, StringComparison.Ordinal)))
                        continue;

                    string source = root.Contains("lutris", StringComparison.OrdinalIgnoreCase)
                        ? "Lutris runner"
                        : root.Contains("bottles", StringComparison.OrdinalIgnoreCase)
                            ? "Bottles runner"
                            : "Steam compatibility tool";
                    candidates.Add(new RuntimeCandidate(
                        $"{source} ({new FileInfo(normalized).Directory?.Parent?.Name ?? "Wine"})",
                        WineRuntimeKind.WinePrefix,
                        normalized,
                        null,
                        source));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // One unreadable runner directory must not hide other valid runtimes.
            }
        }
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
