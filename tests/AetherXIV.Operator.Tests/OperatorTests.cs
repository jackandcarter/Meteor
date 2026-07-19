using AetherXIV.Operator;

namespace AetherXIV.Operator.Tests;

public sealed class OperatorTests
{
    [Fact]
    public void LiveLogBufferBoundsPreviewWithoutChangingEntryText()
    {
        AetherXivLiveLogBuffer buffer = new(capacity: 3);
        buffer.Enqueue(AetherXivManagedService.Map, "map-one");
        buffer.Enqueue(AetherXivManagedService.World, "world-one");
        buffer.Enqueue(AetherXivManagedService.Map, "map-two");
        buffer.Enqueue(AetherXivManagedService.Lobby, "lobby-one");

        AetherXivLiveLogBatch batch = buffer.Drain(maxEntries: 10);

        Assert.Equal(["world-one", "map-two", "lobby-one"], batch.Entries.Select(entry => entry.Text));
        Assert.Equal(1, batch.DroppedByService[AetherXivManagedService.Map]);
        Assert.Equal(0, buffer.PendingCount);
    }

    [Fact]
    public void LiveLogBufferDrainsBurstsInBoundedBatches()
    {
        AetherXivLiveLogBuffer buffer = new(capacity: 10_000);
        Parallel.For(0, 5_000, index =>
            buffer.Enqueue(AetherXivManagedService.Map, $"line-{index}"));

        AetherXivLiveLogBatch first = buffer.Drain(maxEntries: 2_000);
        AetherXivLiveLogBatch second = buffer.Drain(maxEntries: 3_000);

        Assert.Equal(2_000, first.Entries.Count);
        Assert.Equal(3_000, second.Entries.Count);
        Assert.Empty(first.DroppedByService);
        Assert.Empty(second.DroppedByService);
        Assert.Equal(0, buffer.PendingCount);
    }

    [Fact]
    public void ConfigStoreRoundTripsDirectCoreDatabaseAndDiagnostics()
    {
        string root = Path.Combine(Path.GetTempPath(), $"aetherxiv-operator-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "operator-settings.json");
        try
        {
            AetherXivOperatorConfig config = AetherXivOperatorConfig.CreateDefault(root);
            AetherXivOperatorConfigStore.Save(config, path);

            AetherXivOperatorConfig loaded = AetherXivOperatorConfigStore.LoadOrCreate(path, root);

            Assert.Equal(config.Database, loaded.Database);
            Assert.Equal(config.DiagnosticsDirectory, loaded.DiagnosticsDirectory);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DefaultCatalogStartsBackendRouteBeforeClientFacingServices()
    {
        AetherXivOperatorConfig config = AetherXivOperatorConfig.CreateDefault(Environment.CurrentDirectory);

        AetherXivManagedService[] order = AetherXivServiceCatalog.CreateDefault(config)
            .OrderBy(service => service.StartOrder)
            .Select(service => service.Kind)
            .ToArray();

        Assert.Equal(
            [
                AetherXivManagedService.Map,
                AetherXivManagedService.World,
                AetherXivManagedService.Lobby,
                AetherXivManagedService.LauncherServices
            ],
            order);
    }

    [Fact]
    public void GracefulShutdownStopsNewLoginsThenPersistsMapBeforeWorldCloses()
    {
        Assert.Equal(
            [
                AetherXivManagedService.LauncherServices,
                AetherXivManagedService.Lobby,
                AetherXivManagedService.Map,
                AetherXivManagedService.World
            ],
            AetherXivServiceCatalog.GracefulShutdownOrder);
    }

    [Fact]
    public void WorldServiceUsesDirectCoreArgumentsAndDatabase()
    {
        AetherXivOperatorConfig config = AetherXivOperatorConfig.CreateDefault(Environment.CurrentDirectory) with
        {
            WorldMapRoute = "127.0.0.1:1989",
            WorldMapRouteZone = 209,
            Database = new AetherXivDatabaseConfig("db.local", 4406, "ax", "user", "pass")
        };

        AetherXivServiceDefinition world = AetherXivServiceCatalog.CreateDefault(config)
            .Single(service => service.Kind == AetherXivManagedService.World);

        Assert.EndsWith(Path.Combine("src", "AetherXIV.Core.World", "AetherXIV.Core.World.csproj"), world.ProjectRelativePath);
        Assert.Contains("--ip", world.Arguments);
        Assert.Contains("--port", world.Arguments);
        Assert.Contains("--host", world.Arguments);
        Assert.Contains("--db-port", world.Arguments);
        Assert.Contains("--db", world.Arguments);
        Assert.Contains("--user", world.Arguments);
        Assert.Contains("--p", world.Arguments);
        Assert.Contains("--no-console", world.Arguments);
        Assert.DoesNotContain("--map-route", world.Arguments);
        Assert.Contains("db.local", world.Arguments);
        Assert.Contains("4406", world.Arguments);
        Assert.Contains("ax", world.Arguments);
    }

    [Fact]
    public void ProcessStartInfoDoesNotUseShell()
    {
        AetherXivOperatorConfig config = AetherXivOperatorConfig.CreateDefault(Environment.CurrentDirectory) with
        {
            DotnetPath = "dotnet",
            DevLogging = new AetherXivDevLoggingConfig(true, AetherXivDevLogLevel.Verbose, true, false)
        };
        using AetherXivServiceSupervisor supervisor = new(config);

        System.Diagnostics.ProcessStartInfo startInfo = supervisor
            .Find(AetherXivManagedService.Map)
            .CreateStartInfo();

        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
        Assert.True(startInfo.RedirectStandardInput);
        Assert.True(startInfo.CreateNoWindow);
        Assert.Contains("run", startInfo.ArgumentList);
        Assert.Contains("--project", startInfo.ArgumentList);
        Assert.Equal("1", startInfo.Environment["AETHERXIV_DEV_LOGGING"]);
        Assert.Equal("Verbose", startInfo.Environment["AETHERXIV_DEV_LOG_LEVEL"]);
        Assert.Equal("1", startInfo.Environment["AETHERXIV_DEV_LOG_NETWORK"]);
        Assert.Equal("0", startInfo.Environment["AETHERXIV_DEV_LOG_SERVER"]);
        Assert.Equal("1", startInfo.Environment["AETHERXIV_DEV_DIAGNOSTICS"]);
        string traceRunId = Assert.IsType<string>(startInfo.Environment["AETHERXIV_TRACE_RUN_ID"]);
        Assert.False(String.IsNullOrWhiteSpace(traceRunId));
        Assert.Equal(
            Path.Combine(config.DiagnosticsDirectory, traceRunId),
            startInfo.Environment["AETHERXIV_DEV_DIAGNOSTICS_DIR"]);
        Assert.All(
            supervisor.Processes,
            process => Assert.Equal(
                traceRunId,
                process.CreateStartInfo().Environment["AETHERXIV_TRACE_RUN_ID"]));
    }

    [Fact]
    public void ConfigStoreRoundTripsSettings()
    {
        string root = CreateTempDirectory();
        string configPath = Path.Combine(root, "operator-settings.json");
        AetherXivOperatorConfig config = AetherXivOperatorConfig.CreateDefault(root) with
        {
            DotnetPath = "custom-dotnet",
            TraceEnabled = false,
            DevLogging = new AetherXivDevLoggingConfig(false, AetherXivDevLogLevel.Off, false, false),
            WorldMapRouteZone = 300,
            AutoRepairDatabase = false
        };

        AetherXivOperatorConfigStore.Save(config, configPath);
        AetherXivOperatorConfig loaded = AetherXivOperatorConfigStore.LoadOrCreate(configPath, root);

        Assert.Equal("custom-dotnet", loaded.DotnetPath);
        Assert.False(loaded.TraceEnabled);
        Assert.False(loaded.DevLogging.Enabled);
        Assert.Equal(AetherXivDevLogLevel.Off, loaded.DevLogging.Level);
        Assert.Equal(300u, loaded.WorldMapRouteZone);
        Assert.False(loaded.AutoRepairDatabase);
    }

    [Fact]
    public void DatabasePreflightResultRequiresOnlyPassedOrRepairedSteps()
    {
        AetherXivDatabasePreflightResult ok = new(
        [
            new AetherXivDatabasePreflightStep("connect", AetherXivDatabasePreflightStatus.Passed, ""),
            new AetherXivDatabasePreflightStep("migration", AetherXivDatabasePreflightStatus.Repaired, "")
        ]);
        AetherXivDatabasePreflightResult blocked = new(
        [
            new AetherXivDatabasePreflightStep("connect", AetherXivDatabasePreflightStatus.Passed, ""),
            new AetherXivDatabasePreflightStep("migration", AetherXivDatabasePreflightStatus.Missing, "")
        ]);
        AetherXivDatabasePreflightResult needsAdmin = new(
        [
            new AetherXivDatabasePreflightStep("database.bootstrap", AetherXivDatabasePreflightStatus.NeedsAdminCredentials, "")
        ]);
        AetherXivDatabasePreflightResult incompatible = new(
        [
            new AetherXivDatabasePreflightStep("database.compatibility", AetherXivDatabasePreflightStatus.Incompatible, "")
        ]);
        AetherXivDatabasePreflightResult needsMigration = new(
        [
            new AetherXivDatabasePreflightStep("database.compatibility", AetherXivDatabasePreflightStatus.NeedsMigration, "")
        ]);

        Assert.True(ok.CanStartServices);
        Assert.False(blocked.CanStartServices);
        Assert.True(needsAdmin.NeedsAdminCredentials);
        Assert.False(needsAdmin.CanStartServices);
        Assert.True(incompatible.RequiresCompatibilityMigration);
        Assert.False(incompatible.CanStartServices);
        Assert.True(needsMigration.RequiresInPlaceMigration);
        Assert.False(needsMigration.RequiresCompatibilityMigration);
        Assert.False(needsMigration.CanStartServices);
    }

    [Fact]
    public void DatabaseInstallerFindsSourceOwnedPackageWithoutLegacyDependency()
    {
        string root = CreateTempDirectory();
        string package = Path.Combine(root, "db", "direct-core");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "ffxiv_server.sql"), "-- baseline");
        File.WriteAllText(Path.Combine(package, "setup.sh"), "#!/bin/sh");
        File.WriteAllText(Path.Combine(package, "setup.ps1"), "param()");

        Assert.Equal(package, AetherXivDatabaseInstaller.FindPackageDirectory(Path.Combine(root, "src", "app")));
    }

    [Fact]
    public async Task DatabaseInstallerUsesConfiguredAccountForPendingMigrations()
    {
        string root = CreateTempDirectory();
        string package = Path.Combine(root, "Database");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "ffxiv_server.sql"), "-- baseline");
        File.WriteAllText(Path.Combine(package, "setup.ps1"), """
param([switch]$MigrateOnly)
if (-not $MigrateOnly) { exit 11 }
if ($env:AETHERXIV_DB_USER -ne 'saved-user') { exit 12 }
if ($env:AETHERXIV_DB_PASSWORD -ne 'saved-password') { exit 13 }
if ($env:AETHERXIV_DB_ADMIN_USER -or $env:AETHERXIV_DB_ADMIN_PASSWORD) { exit 14 }
Write-Output 'configured-account-migration'
""");
        File.WriteAllText(Path.Combine(package, "setup.sh"), """
#!/usr/bin/env bash
[[ "${1:-}" == "--migrate-only" ]] || exit 11
[[ "${AETHERXIV_DB_USER:-}" == "saved-user" ]] || exit 12
[[ "${AETHERXIV_DB_PASSWORD:-}" == "saved-password" ]] || exit 13
[[ -z "${AETHERXIV_DB_ADMIN_USER:-}" && -z "${AETHERXIV_DB_ADMIN_PASSWORD:-}" ]] || exit 14
echo configured-account-migration
""");

        try
        {
            AetherXivOperatorConfig config = AetherXivOperatorConfig.CreateDefault(root) with
            {
                Database = new AetherXivDatabaseConfig("127.0.0.1", 3306, "ffxiv_server", "saved-user", "saved-password")
            };

            AetherXivDatabaseInstallResult result = await new AetherXivDatabaseInstaller()
                .ApplyPendingMigrationsAsync(config);

            Assert.True(result.Succeeded, result.Output);
            Assert.Contains("configured-account-migration", result.Output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DatabaseInstallerUsesAdminCredentialsForFreshAndCleanSetup()
    {
        string root = CreateTempDirectory();
        string package = Path.Combine(root, "Database");
        Directory.CreateDirectory(package);
        File.WriteAllText(Path.Combine(package, "ffxiv_server.sql"), "-- baseline");
        File.WriteAllText(Path.Combine(package, "setup.ps1"), """
param([switch]$CleanMigrate)
if ($env:AETHERXIV_DB_USER -ne 'saved-user') { exit 11 }
if ($env:AETHERXIV_DB_PASSWORD -ne 'saved-password') { exit 12 }
if ($env:AETHERXIV_DB_ADMIN_USER -ne 'admin-user') { exit 13 }
if ($env:AETHERXIV_DB_ADMIN_PASSWORD -ne 'admin-password') { exit 14 }
if ($CleanMigrate) { Write-Output 'clean-setup' } else { Write-Output 'fresh-setup' }
""");
        File.WriteAllText(Path.Combine(package, "setup.sh"), """
#!/usr/bin/env bash
[[ "${AETHERXIV_DB_USER:-}" == "saved-user" ]] || exit 11
[[ "${AETHERXIV_DB_PASSWORD:-}" == "saved-password" ]] || exit 12
[[ "${AETHERXIV_DB_ADMIN_USER:-}" == "admin-user" ]] || exit 13
[[ "${AETHERXIV_DB_ADMIN_PASSWORD:-}" == "admin-password" ]] || exit 14
case "${1:-}" in
  "") echo fresh-setup ;;
  --clean-migrate) echo clean-setup ;;
  *) exit 15 ;;
esac
""");

        try
        {
            AetherXivOperatorConfig config = AetherXivOperatorConfig.CreateDefault(root) with
            {
                Database = new AetherXivDatabaseConfig("127.0.0.1", 3306, "ffxiv_server", "saved-user", "saved-password")
            };
            AetherXivMariaDbAdminCredentials admin = new("admin-user", "admin-password");
            AetherXivDatabaseInstaller installer = new();

            AetherXivDatabaseInstallResult fresh = await installer.SetupAsync(config, admin);
            AetherXivDatabaseInstallResult clean = await installer.CleanMigrateAsync(config, admin);

            Assert.True(fresh.Succeeded, fresh.Output);
            Assert.Contains("fresh-setup", fresh.Output, StringComparison.Ordinal);
            Assert.True(clean.Succeeded, clean.Output);
            Assert.Contains("clean-setup", clean.Output, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DatabaseCompatibilityContractIsExplicitAndVersioned()
    {
        Assert.Equal("direct-core", AetherXivDatabaseCompatibility.Key);
        Assert.Equal(2u, AetherXivDatabaseCompatibility.SchemaGeneration);
        Assert.Equal(1u, AetherXivDatabaseCompatibility.SchemaVersion);
        Assert.Equal("aetherxiv-direct-core-v2", AetherXivDatabaseCompatibility.CompatibilityId);
        Assert.Contains("baseline", AetherXivDatabaseCompatibility.BaselineId, StringComparison.Ordinal);
        Assert.Equal("20260719_000015_hall_of_flames_push_circle.sql", AetherXivDatabaseCompatibility.LatestDirectCoreMigration);
        Assert.Equal("zone-service-npcs-1.23b", AetherXivDatabaseCompatibility.NpcServiceCatalogId);
        Assert.Equal(64, AetherXivDatabaseCompatibility.NpcServiceCatalogHash.Length);
    }

    [Theory]
    [InlineData("#f2f4fa", "#F2F4FA")]
    [InlineData("#80ffffff", "#80FFFFFF")]
    [InlineData("not-a-color", "#DEFAULT")]
    public void LauncherContentColorsAreNormalized(string input, string expected)
    {
        Assert.Equal(expected, LauncherContentAdminService.NormalizeColor(input, "#DEFAULT"));
    }

    [Fact]
    public void DependencyPreflightAcceptsMinimalRunnableWorkspace()
    {
        AetherXivOperatorConfig config = CreateMinimalWorkspaceConfig();
        AetherXivDependencyCheckResult result = new AetherXivDependencyPreflightService().Run(config);

        Assert.True(result.CanStartServices);
        Assert.Contains(result.Steps, step => step.Name == "dotnet" && step.Status == AetherXivDependencyStatus.Warning);
        Assert.Contains(result.Steps, step => step.Name == "scripts-player" && step.Status == AetherXivDependencyStatus.Passed);
        Assert.Contains(result.Steps, step => step.Name == "scripts-integrity" && step.Status == AetherXivDependencyStatus.Passed);
        Assert.Contains(result.Steps, step => step.Name == "system-actors" && step.Status == AetherXivDependencyStatus.Passed);
        Assert.Contains(result.Steps, step => step.Name == "service-Map" && step.Status == AetherXivDependencyStatus.Passed);
    }

    [Fact]
    public void DependencyPreflightBlocksWhenPlayerScriptIsMissing()
    {
        AetherXivOperatorConfig config = CreateMinimalWorkspaceConfig();
        File.Delete(Path.Combine(config.ScriptsRoot, "player.lua"));

        AetherXivDependencyCheckResult result = new AetherXivDependencyPreflightService().Run(config);

        Assert.False(result.CanStartServices);
        Assert.Contains(result.Steps, step => step.Name == "scripts-player" && step.Status == AetherXivDependencyStatus.Failed);
    }

    [Fact]
    public void DependencyPreflightWarnsButStartsWhenPackagedLuaDiffersFromManifest()
    {
        AetherXivOperatorConfig config = CreateMinimalWorkspaceConfig();
        File.AppendAllText(Path.Combine(config.ScriptsRoot, "player.lua"), "\n-- drift");

        AetherXivDependencyCheckResult result = new AetherXivDependencyPreflightService().Run(config);

        Assert.True(result.CanStartServices);
        Assert.Contains(result.Steps, step =>
            step.Name == "scripts-integrity"
            && step.Status == AetherXivDependencyStatus.Warning
            && step.Message.Contains("changed=player.lua", StringComparison.Ordinal));
    }

    [Fact]
    public void DependencyPreflightWarnsButStartsWhenLuaScriptIsAdded()
    {
        AetherXivOperatorConfig config = CreateMinimalWorkspaceConfig();
        File.WriteAllText(Path.Combine(config.ScriptsRoot, "new-service.lua"), "-- in development");

        AetherXivDependencyCheckResult result = new AetherXivDependencyPreflightService().Run(config);

        Assert.True(result.CanStartServices);
        Assert.Contains(result.Steps, step =>
            step.Name == "scripts-integrity"
            && step.Status == AetherXivDependencyStatus.Warning
            && step.Message.Contains("extra=new-service.lua", StringComparison.Ordinal));
    }

    [Fact]
    public void DependencyPreflightWarnsButStartsWhenLuaInventoryIsMissing()
    {
        AetherXivOperatorConfig config = CreateMinimalWorkspaceConfig();
        File.Delete(AetherXivOperatorPaths.ResolveLuaManifestPath(config.DataRoot));

        AetherXivDependencyCheckResult result = new AetherXivDependencyPreflightService().Run(config);

        Assert.True(result.CanStartServices);
        Assert.Contains(result.Steps, step =>
            step.Name == "scripts-integrity"
            && step.Status == AetherXivDependencyStatus.Warning
            && step.Message.Contains("inventory is missing", StringComparison.Ordinal));
    }

    [Fact]
    public void DependencyPreflightBlocksWhenSystemActorDataIsMissing()
    {
        AetherXivOperatorConfig config = CreateMinimalWorkspaceConfig();
        File.Delete(AetherXivOperatorPaths.ResolveStaticActorsPath(config.DataRoot));

        AetherXivDependencyCheckResult result = new AetherXivDependencyPreflightService().Run(config);

        Assert.False(result.CanStartServices);
        Assert.Contains(result.Steps, step =>
            step.Name == "system-actors"
            && step.Status == AetherXivDependencyStatus.Failed);
    }

    [Fact]
    public void DependencyPreflightAcceptsPublishedServerExecutablesWithoutSourceProjects()
    {
        AetherXivOperatorConfig config = CreatePublishedWorkspaceConfig();

        AetherXivDependencyCheckResult result = new AetherXivDependencyPreflightService().Run(config);

        Assert.True(result.CanStartServices);
        Assert.Contains(result.Steps, step =>
            step.Name == "service-Map"
            && step.Status == AetherXivDependencyStatus.Passed
            && step.Message.Contains("published executable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProcessStartInfoUsesPublishedExecutableWhenAvailable()
    {
        AetherXivOperatorConfig config = CreatePublishedWorkspaceConfig();
        using AetherXivServiceSupervisor supervisor = new(config);

        System.Diagnostics.ProcessStartInfo startInfo = supervisor
            .Find(AetherXivManagedService.Map)
            .CreateStartInfo();

        Assert.EndsWith(Path.Combine("servers", "map", HostExecutableName("AetherXIV.Core.Map")), startInfo.FileName);
        Assert.DoesNotContain("run", startInfo.ArgumentList);
        Assert.Contains("--ip", startInfo.ArgumentList);
        Assert.DoesNotContain("--bind", startInfo.ArgumentList);
    }

    [Fact]
    public void OperatorPathsFindsPackagedRootFromAppBundleMacOsDirectory()
    {
        AetherXivOperatorConfig config = CreatePublishedWorkspaceConfig();
        string macOsDirectory = Path.Combine(config.WorkspaceRoot, "AetherXIV Core.app", "Contents", "MacOS");
        string resourcesDirectory = Path.Combine(config.WorkspaceRoot, "AetherXIV Core.app", "Contents", "Resources");
        Directory.CreateDirectory(macOsDirectory);
        CopyDirectory(Path.Combine(config.WorkspaceRoot, "servers"), Path.Combine(resourcesDirectory, "servers"));

        string found = AetherXivOperatorPaths.FindWorkspaceRoot(macOsDirectory);

        Assert.Equal(resourcesDirectory, found);
        Assert.True(AetherXivOperatorPaths.IsPackagedRoot(resourcesDirectory));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"aetherxiv-operator-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static AetherXivOperatorConfig CreateMinimalWorkspaceConfig()
    {
        string root = CreateTempDirectory();
        File.WriteAllText(Path.Combine(root, "AetherXIV.sln"), "");
        Directory.CreateDirectory(Path.Combine(root, "Data", "scripts"));
        File.WriteAllText(Path.Combine(root, "Data", "scripts", "player.lua"), "-- test");
        WriteSingleScriptManifest(root);
        WriteSystemActorFixture(root);

        AetherXivOperatorConfig config = AetherXivOperatorConfig.CreateDefault(root) with
        {
            DotnetPath = "dotnet",
            DiagnosticsDirectory = Path.Combine(root, "diagnostics"),
            WorldMapRoute = "127.0.0.1:1989"
        };

        foreach (AetherXivServiceDefinition service in AetherXivServiceCatalog.CreateDefault(config))
        {
            string projectPath = service.ProjectPath(config);
            Directory.CreateDirectory(Path.GetDirectoryName(projectPath) ?? root);
            File.WriteAllText(projectPath, "<Project />");
        }

        return config;
    }

    private static AetherXivOperatorConfig CreatePublishedWorkspaceConfig()
    {
        string root = CreateTempDirectory();
        string mapRoot = Path.Combine(root, "servers", "map");
        string scriptsRoot = Path.Combine(mapRoot, "scripts");
        Directory.CreateDirectory(scriptsRoot);
        File.WriteAllText(Path.Combine(scriptsRoot, "player.lua"), "-- test");
        WriteSingleScriptManifest(root, mapRoot);
        File.WriteAllBytes(Path.Combine(mapRoot, "staticactors.bin"), "test"u8.ToArray());

        AetherXivOperatorConfig config = AetherXivOperatorConfig.CreateDefault(root) with
        {
            DotnetPath = "dotnet",
            DiagnosticsDirectory = Path.Combine(root, "diagnostics"),
            WorldMapRoute = "127.0.0.1:1989"
        };

        foreach (AetherXivServiceDefinition service in AetherXivServiceCatalog.CreateDefault(config))
        {
            string executablePath = service.PublishedExecutablePath(config);
            Directory.CreateDirectory(Path.GetDirectoryName(executablePath) ?? root);
            File.WriteAllText(executablePath, "");
        }

        return config;
    }

    private static string HostExecutableName(string baseName) =>
        OperatingSystem.IsWindows() ? $"{baseName}.exe" : baseName;

    private static void WriteSingleScriptManifest(string root, string? packagedMapRoot = null)
    {
        string contentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("-- test"u8.ToArray()))
            .ToLowerInvariant();
        using System.Security.Cryptography.IncrementalHash tree =
            System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        tree.AppendData("player.lua"u8);
        tree.AppendData([0]);
        tree.AppendData(Convert.FromHexString(contentHash));
        string treeHash = Convert.ToHexString(tree.GetHashAndReset()).ToLowerInvariant();
        string path;
        if (packagedMapRoot is null)
        {
            string directory = Path.Combine(root, "Data", "seeds", "lua-tree");
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "manifest.json");
        }
        else
        {
            Directory.CreateDirectory(packagedMapRoot);
            path = Path.Combine(packagedMapRoot, "scripts.manifest.json");
        }
        File.WriteAllText(
            path,
            System.Text.Json.JsonSerializer.Serialize(new
            {
                schema = "aetherxiv.lua-tree-manifest.v1",
                source = "test",
                fileCount = 1,
                treeSha256 = treeHash,
                files = new Dictionary<string, string> { ["player.lua"] = contentHash }
            }));
    }

    private static void WriteSystemActorFixture(string root)
    {
        string directory = Path.Combine(root, "Data", "seeds", "static-actors");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "staticactors.bin"), "test"u8.ToArray());
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)));
    }
}
