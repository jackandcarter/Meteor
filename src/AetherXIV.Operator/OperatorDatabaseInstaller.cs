using System.Diagnostics;

namespace AetherXIV.Operator;

public sealed record AetherXivDatabaseInstallResult(
    bool Succeeded,
    int ExitCode,
    string Output,
    string PackageDirectory);

public sealed class AetherXivDatabaseInstaller
{
    private enum InstallMode
    {
        Setup,
        MigrateExisting,
        CleanMigrate
    }

    public Task<AetherXivDatabaseInstallResult> SetupAsync(
        AetherXivOperatorConfig config,
        AetherXivMariaDbAdminCredentials adminCredentials,
        CancellationToken cancellationToken = default) =>
        RunAsync(config, adminCredentials, InstallMode.Setup, cancellationToken);

    public Task<AetherXivDatabaseInstallResult> ApplyPendingMigrationsAsync(
        AetherXivOperatorConfig config,
        CancellationToken cancellationToken = default) =>
        RunAsync(config, adminCredentials: null, InstallMode.MigrateExisting, cancellationToken);

    public Task<AetherXivDatabaseInstallResult> RebuildCanonicalAsync(
        AetherXivOperatorConfig config,
        AetherXivMariaDbAdminCredentials adminCredentials,
        CancellationToken cancellationToken = default) =>
        RunAsync(config, adminCredentials, InstallMode.CleanMigrate, cancellationToken);

    public static string? FindPackageDirectory(params string?[] starts)
    {
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        foreach (string? start in starts)
        {
            if (String.IsNullOrWhiteSpace(start))
                continue;

            string fullPath = Path.GetFullPath(start);
            DirectoryInfo? directory = File.Exists(fullPath)
                ? new DirectoryInfo(Path.GetDirectoryName(fullPath)!)
                : new DirectoryInfo(fullPath);
            while (directory is not null)
            {
                if (visited.Add(directory.FullName))
                {
                    foreach (string candidate in new[]
                    {
                        Path.Combine(directory.FullName, "Database"),
                        Path.Combine(directory.FullName, "db", "direct-core"),
                        Path.Combine(directory.FullName, "bin", "build", "Debug", CurrentPlatformName(), "Database"),
                        Path.Combine(directory.FullName, "bin", "build", "Release", CurrentPlatformName(), "Database")
                    })
                    {
                        if (File.Exists(Path.Combine(candidate, "ffxiv_server.sql"))
                            && File.Exists(Path.Combine(candidate, "setup.sh"))
                            && File.Exists(Path.Combine(candidate, "setup.ps1")))
                            return candidate;
                    }
                }
                directory = directory.Parent;
            }
        }
        return null;
    }

    private static string CurrentPlatformName() =>
        OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "MacOS" : "Linux";

    private static async Task<AetherXivDatabaseInstallResult> RunAsync(
        AetherXivOperatorConfig config,
        AetherXivMariaDbAdminCredentials? adminCredentials,
        InstallMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (mode is not InstallMode.MigrateExisting)
            ArgumentNullException.ThrowIfNull(adminCredentials);
        AetherXivOperatorConfig normalized = config.Normalize();
        string? package = FindPackageDirectory(normalized.WorkspaceRoot, AppContext.BaseDirectory, Environment.CurrentDirectory);
        if (package is null)
            return new(false, -1, "The packaged Database installer could not be found.", "");

        ProcessStartInfo startInfo = new()
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = package,
            FileName = OperatingSystem.IsWindows() ? "powershell" : "/bin/bash"
        };
        if (OperatingSystem.IsWindows())
        {
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(Path.Combine(package, "setup.ps1"));
            if (mode is InstallMode.MigrateExisting)
                startInfo.ArgumentList.Add("-MigrateOnly");
            else if (mode is InstallMode.CleanMigrate)
                startInfo.ArgumentList.Add("-CleanMigrate");
        }
        else
        {
            startInfo.ArgumentList.Add(Path.Combine(package, "setup.sh"));
            if (mode is InstallMode.MigrateExisting)
                startInfo.ArgumentList.Add("--migrate-only");
            else if (mode is InstallMode.CleanMigrate)
                startInfo.ArgumentList.Add("--clean-migrate");
        }

        startInfo.Environment["AETHERXIV_DB_HOST"] = normalized.Database.Host;
        startInfo.Environment["AETHERXIV_DB_PORT"] = normalized.Database.Port.ToString();
        startInfo.Environment["AETHERXIV_DB_NAME"] = normalized.Database.Name;
        startInfo.Environment["AETHERXIV_DB_USER"] = normalized.Database.User;
        startInfo.Environment["AETHERXIV_DB_PASSWORD"] = normalized.Database.Password;
        if (adminCredentials is not null)
        {
            startInfo.Environment["AETHERXIV_DB_ADMIN_USER"] = adminCredentials.User;
            startInfo.Environment["AETHERXIV_DB_ADMIN_PASSWORD"] = adminCredentials.Password;
        }
        else
        {
            startInfo.Environment.Remove("AETHERXIV_DB_ADMIN_USER");
            startInfo.Environment.Remove("AETHERXIV_DB_ADMIN_PASSWORD");
        }

        if (!OperatingSystem.IsWindows())
        {
            string inheritedPath = startInfo.Environment.TryGetValue("PATH", out string? path) ? path ?? "" : "";
            string[] appBundleClientPaths =
            [
                "/usr/local/bin",
                "/usr/local/opt/mariadb/bin",
                "/opt/homebrew/bin",
                "/opt/homebrew/opt/mariadb/bin",
                "/opt/local/bin"
            ];
            startInfo.Environment["PATH"] = String.Join(
                Path.PathSeparator,
                appBundleClientPaths.Append(inheritedPath).Where(value => !String.IsNullOrWhiteSpace(value)));
        }

        using Process process = new() { StartInfo = startInfo };
        process.Start();
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        string output = String.Join(Environment.NewLine,
            new[] { await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false) }
                .Where(value => !String.IsNullOrWhiteSpace(value)))
            .Trim();
        return new(process.ExitCode == 0, process.ExitCode, output, package);
    }
}
