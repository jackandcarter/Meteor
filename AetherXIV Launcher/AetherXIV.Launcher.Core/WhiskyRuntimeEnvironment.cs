using System.Diagnostics;

namespace AetherXIV.Launcher.Core;

public static class WhiskyRuntimeEnvironment
{
    public static bool TryCreateWineProfile(
        string whiskyCommand,
        string bottleName,
        out WineRuntimeProfile profile,
        out string error)
    {
        profile = WineRuntimeProfile.WhiskyBottle("Whisky", bottleName, whiskyCommand);
        error = "";

        if (string.IsNullOrWhiteSpace(whiskyCommand))
        {
            error = "Whisky command is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(bottleName))
        {
            error = "Whisky bottle name is required.";
            return false;
        }

        try
        {
            using Process process = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = whiskyCommand,
                    Arguments = $"shellenv {CommandLineArguments.Quote(bottleName)}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill(true);
                error = "Whisky shellenv timed out.";
                return false;
            }

            if (process.ExitCode != 0)
            {
                error = string.IsNullOrWhiteSpace(stderr)
                    ? $"Whisky shellenv failed with exit code {process.ExitCode}."
                    : stderr.Trim();
                return false;
            }

            return TryCreateWineProfileFromShellEnv(
                $"Whisky - {bottleName}",
                output,
                out profile,
                out error);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryCreateWineProfileFromShellEnv(
        string name,
        string shellEnv,
        out WineRuntimeProfile profile,
        out string error)
    {
        Dictionary<string, string> environment = ParseExports(shellEnv);
        string? prefix = GetExpandedValue(environment, "WINEPREFIX");
        string? path = GetExpandedValue(environment, "PATH");
        string wineName = GetExpandedValue(environment, "WINE") ?? "wine64";

        if (string.IsNullOrWhiteSpace(prefix))
        {
            profile = WineRuntimeProfile.Custom(name, wineName);
            error = "Whisky shellenv did not provide WINEPREFIX.";
            return false;
        }

        string command = ResolveWineCommand(path, wineName);
        environment["PATH"] = path ?? Environment.GetEnvironmentVariable("PATH") ?? "";

        profile = WineRuntimeProfile.WinePrefix(name, ExpandHome(prefix), command, environment);
        error = "";
        return true;
    }

    public static Dictionary<string, string> ParseExports(string shellEnv)
    {
        Dictionary<string, string> environment = new(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(shellEnv))
            return environment;

        using StringReader reader = new(shellEnv);
        while (reader.ReadLine() is { } line)
        {
            string trimmed = line.Trim();
            if (!trimmed.StartsWith("export ", StringComparison.Ordinal))
                continue;

            string assignment = trimmed["export ".Length..];
            int equals = assignment.IndexOf('=');
            if (equals <= 0)
                continue;

            string key = assignment[..equals].Trim();
            string value = assignment[(equals + 1)..].Trim();
            environment[key] = Unquote(value);
        }

        return environment;
    }

    private static string? GetExpandedValue(IReadOnlyDictionary<string, string> environment, string key)
    {
        return environment.TryGetValue(key, out string? value)
            ? ExpandShellValue(value)
            : null;
    }

    private static string ResolveWineCommand(string? path, string wineName)
    {
        if (Path.IsPathFullyQualified(wineName))
            return wineName;

        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (string directory in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                    continue;

                string candidate = Path.Combine(ExpandHome(directory), wineName);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return wineName;
    }

    private static string ExpandShellValue(string value)
    {
        string expanded = ExpandHome(value);
        string currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        return expanded.Replace("$PATH", currentPath, StringComparison.Ordinal);
    }

    private static string ExpandHome(string value)
    {
        if (value == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (value.StartsWith("~/", StringComparison.Ordinal))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), value[2..]);
        return value;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1];
        return value;
    }
}
