using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AetherXIV.Launcher.Core;

public sealed record RuntimeLaunchResult(int ProcessId, string LogPath);

public static class RuntimeLaunchDiagnostics
{
    private static readonly Regex SensitiveArgumentRegex = new(
        @"(--session(?:\s+|=))(?:""[^""]*""|'[^']*'|\S+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string CreateLogPath(string prefix = "launch", string? logsRoot = null)
    {
        string root = logsRoot ?? RuntimeInstallStore.LogsRoot;
        Directory.CreateDirectory(root);
        string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(root, $"{prefix}-{timestamp}.log");
    }

    public static RuntimeLaunchResult StartWithLogging(ProcessStartInfo startInfo, string? logPath = null)
    {
        ArgumentNullException.ThrowIfNull(startInfo);

        string outputPath = logPath ?? CreateLogPath();
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        StreamWriter writer = new(outputPath, append: false);
        writer.WriteLine($"started_at={DateTimeOffset.Now:O}");
        writer.WriteLine($"file={startInfo.FileName}");
        writer.WriteLine($"arguments={RedactSensitiveArguments(startInfo.Arguments)}");
        WriteEnvironmentProbe(startInfo.Environment, writer);
        writer.Flush();

        Process process = new() { StartInfo = startInfo, EnableRaisingEvents = true };
        object gate = new();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
                return;

            lock (gate)
            {
                writer.WriteLine(eventArgs.Data);
                writer.Flush();
            }
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
                return;

            lock (gate)
            {
                writer.WriteLine(eventArgs.Data);
                writer.Flush();
            }
        };

        process.Exited += (_, _) =>
        {
            lock (gate)
            {
                writer.WriteLine($"exited_at={DateTimeOffset.Now:O}");
                writer.WriteLine($"exit_code={process.ExitCode}");
                writer.Dispose();
                process.Dispose();
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch
        {
            writer.Dispose();
            process.Dispose();
            throw;
        }

        return new RuntimeLaunchResult(process.Id, outputPath);
    }

    public static string RedactSensitiveArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return arguments;

        return SensitiveArgumentRegex.Replace(arguments, match => $"{match.Groups[1].Value}<redacted>");
    }

    private static void WriteEnvironmentProbe(IDictionary<string, string?> environment, TextWriter writer)
    {
        string[] keys =
        {
            "WINEPREFIX",
            "WINE_D3D_CONFIG",
            "WINEDEBUG",
            "CX_BOTTLE",
            "AETHERXIV_SERVER_HOST",
            "AETHERXIV_LOBBY_PORT",
            "AETHERXIV_WORLD_PORT",
            "AETHERXIV_MAP_PORT",
            "AETHER_UMBRA_ENABLED",
            "AETHER_UMBRA_BOOTSTRAP",
            "AETHER_UMBRA_FRAMEWORK",
            "AETHER_UMBRA_PLUGIN_DIR",
            "AETHER_UMBRA_CACHE_DIR",
            "AETHER_UMBRA_LOG",
            "AETHER_UMBRA_SAFE_MODE",
            "AETHER_UMBRA_LOAD_DELAY_MS",
            "AETHER_UMBRA_ENABLE_MANAGED_ON_WINE"
        };

        foreach (string key in keys)
        {
            if (environment.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
                writer.WriteLine($"env.{key}={value}");
        }
    }
}
