using System.Diagnostics;

namespace AetherXIV.Launcher.Core;

public sealed record RuntimeDependencyInstallPlan(
    bool IsSupported,
    string ElevationCommand,
    string PackageManagerCommand,
    IReadOnlyList<string> Arguments,
    string Description);

public sealed record RuntimeDependencyInstallResult(bool Succeeded, string Message);

public static class RuntimeDependencyInstaller
{
    public static RuntimeDependencyInstallPlan CreateLinuxPlan(string? osReleaseText, bool hasPolicyKit)
    {
        Dictionary<string, string> values = ParseOsRelease(osReleaseText);
        string id = values.GetValueOrDefault("ID") ?? "";
        string idLike = values.GetValueOrDefault("ID_LIKE") ?? "";
        string family = $"{id} {idLike}".ToLowerInvariant();

        if (!hasPolicyKit)
            return Unsupported("Automatic installation requires the system PolicyKit prompt (pkexec).");

        if (family.Contains("debian", StringComparison.Ordinal) || family.Contains("ubuntu", StringComparison.Ordinal))
        {
            return new RuntimeDependencyInstallPlan(
                true,
                "/usr/bin/pkexec",
                FindFirstExisting("/usr/bin/apt-get", "/bin/apt-get"),
                ["install", "-y", "wine64"],
                "Install the distribution Wine host dependencies with apt");
        }

        if (id == "steamos")
            return Unsupported("SteamOS system packages are immutable; install the reported libraries in a persistent environment, then verify again.");

        if (family.Contains("arch", StringComparison.Ordinal))
        {
            return new RuntimeDependencyInstallPlan(
                true,
                "/usr/bin/pkexec",
                FindFirstExisting("/usr/bin/pacman", "/bin/pacman"),
                ["-S", "--needed", "--noconfirm", "wine"],
                "Install the distribution Wine host dependencies with pacman");
        }

        if (family.Contains("fedora", StringComparison.Ordinal) || family.Contains("rhel", StringComparison.Ordinal))
        {
            return new RuntimeDependencyInstallPlan(
                true,
                "/usr/bin/pkexec",
                FindFirstExisting("/usr/bin/dnf5", "/usr/bin/dnf", "/bin/dnf"),
                ["install", "-y", "wine"],
                "Install the distribution Wine host dependencies with dnf");
        }

        return Unsupported("This Linux distribution is not supported for automatic dependency installation.");
    }

    public static RuntimeDependencyInstallPlan CreateCurrentLinuxPlan()
    {
        string osRelease = File.Exists("/etc/os-release") ? File.ReadAllText("/etc/os-release") : "";
        bool hasPolicyKit = File.Exists("/usr/bin/pkexec") || File.Exists("/bin/pkexec");
        RuntimeDependencyInstallPlan plan = CreateLinuxPlan(osRelease, hasPolicyKit);
        if (!plan.IsSupported)
            return plan;

        if (string.IsNullOrWhiteSpace(plan.PackageManagerCommand) || !File.Exists(plan.PackageManagerCommand))
            return Unsupported("The distribution package manager could not be found.");

        string elevation = File.Exists("/usr/bin/pkexec") ? "/usr/bin/pkexec" : "/bin/pkexec";
        return plan with { ElevationCommand = elevation };
    }

    public static async Task<RuntimeDependencyInstallResult> InstallAsync(
        RuntimeDependencyInstallPlan plan,
        CancellationToken cancellationToken = default)
    {
        if (!plan.IsSupported)
            return new RuntimeDependencyInstallResult(false, plan.Description);

        ProcessStartInfo startInfo = new()
        {
            FileName = plan.ElevationCommand,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add(plan.PackageManagerCommand);
        foreach (string argument in plan.Arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = new() { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new RuntimeDependencyInstallResult(false, ex.Message);
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string output = await outputTask;
        string error = await errorTask;
        if (process.ExitCode == 0)
            return new RuntimeDependencyInstallResult(true, "Dependency installation completed.");

        string detail = FirstUsefulLine(error, output);
        return new RuntimeDependencyInstallResult(
            false,
            $"Dependency installation exited with code {process.ExitCode}: {detail}");
    }

    private static RuntimeDependencyInstallPlan Unsupported(string description) =>
        new(false, "", "", Array.Empty<string>(), description);

    private static string FindFirstExisting(params string[] paths) =>
        paths.FirstOrDefault(File.Exists) ?? paths.FirstOrDefault() ?? "";

    private static string FirstUsefulLine(params string[] values)
    {
        foreach (string value in values)
        {
            string? line = value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(line))
                return line;
        }
        return "No package-manager details were returned.";
    }

    private static Dictionary<string, string> ParseOsRelease(string? text)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in (text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int separator = line.IndexOf('=');
            if (separator > 0)
                values[line[..separator]] = line[(separator + 1)..].Trim().Trim('"', '\'');
        }
        return values;
    }
}
