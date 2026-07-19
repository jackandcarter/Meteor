namespace AetherXIV.Operator;

public enum AetherXivDependencyStatus
{
    Passed,
    Warning,
    Failed
}

public sealed record AetherXivDependencyCheckStep(
    string Name,
    AetherXivDependencyStatus Status,
    string Message);

public sealed record AetherXivDependencyCheckResult(
    IReadOnlyList<AetherXivDependencyCheckStep> Steps)
{
    public bool CanStartServices => Steps.All(step => step.Status is not AetherXivDependencyStatus.Failed);
}

public sealed class AetherXivDependencyPreflightService
{
    public AetherXivDependencyCheckResult Run(AetherXivOperatorConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        AetherXivOperatorConfig normalized = config.Normalize();
        List<AetherXivDependencyCheckStep> steps = new();
        void Add(string name, AetherXivDependencyStatus status, string message) =>
            steps.Add(new AetherXivDependencyCheckStep(name, status, message));

        IReadOnlyList<AetherXivServiceDefinition> services = AetherXivServiceCatalog.CreateDefault(normalized);
        AddDirectoryCheck(steps, "workspace", normalized.WorkspaceRoot, "Workspace root");
        AddSourceOrPackageRootCheck(steps, normalized, services);
        AddDotnetCheck(steps, normalized.DotnetPath);
        AddDirectoryCheck(steps, "data-root", normalized.DataRoot, "Data root");
        AddDirectoryCheck(steps, "scripts-root", normalized.ScriptsRoot, "Scripts root");
        AddFileCheck(steps, "scripts-player", Path.Combine(normalized.ScriptsRoot, "player.lua"), "Player Lua script");
        if (Directory.Exists(normalized.ScriptsRoot))
        {
            steps.Add(LuaTreeManifestVerifier.Verify(
                normalized.ScriptsRoot,
                AetherXivOperatorPaths.ResolveLuaManifestPath(normalized.DataRoot)));
        }
        AddFileCheck(
            steps,
            "system-actors",
            AetherXivOperatorPaths.ResolveStaticActorsPath(normalized.DataRoot),
            "Packaged system actor data");
        AddDiagnosticsCheck(steps, normalized.DiagnosticsDirectory);

        foreach (AetherXivServiceDefinition service in services)
            AddServiceHostCheck(steps, service, normalized);

        if (String.IsNullOrWhiteSpace(normalized.WorldMapRoute))
            Add("world-map-route", AetherXivDependencyStatus.Failed, "World map route endpoint is not configured.");
        else
            Add("world-map-route", AetherXivDependencyStatus.Passed, $"World routes backend Map traffic to {normalized.WorldMapRoute}.");

        return new AetherXivDependencyCheckResult(steps);
    }

    private static void AddDirectoryCheck(
        List<AetherXivDependencyCheckStep> steps,
        string name,
        string path,
        string label)
    {
        steps.Add(Directory.Exists(path)
            ? new AetherXivDependencyCheckStep(name, AetherXivDependencyStatus.Passed, $"{label} exists: {path}")
            : new AetherXivDependencyCheckStep(name, AetherXivDependencyStatus.Failed, $"{label} is missing: {path}"));
    }

    private static void AddFileCheck(
        List<AetherXivDependencyCheckStep> steps,
        string name,
        string path,
        string label)
    {
        steps.Add(File.Exists(path)
            ? new AetherXivDependencyCheckStep(name, AetherXivDependencyStatus.Passed, $"{label} exists: {path}")
            : new AetherXivDependencyCheckStep(name, AetherXivDependencyStatus.Failed, $"{label} is missing: {path}"));
    }

    private static void AddSourceOrPackageRootCheck(
        List<AetherXivDependencyCheckStep> steps,
        AetherXivOperatorConfig config,
        IReadOnlyList<AetherXivServiceDefinition> services)
    {
        string solutionPath = Path.Combine(config.WorkspaceRoot, "AetherXIV.sln");
        if (File.Exists(solutionPath))
        {
            steps.Add(new AetherXivDependencyCheckStep("source-root", AetherXivDependencyStatus.Passed, $"Solution file exists: {solutionPath}"));
            return;
        }

        if (services.Any(service => service.HasPublishedExecutable(config)))
        {
            steps.Add(new AetherXivDependencyCheckStep("package-root", AetherXivDependencyStatus.Passed, $"Published service payload exists under: {config.WorkspaceRoot}"));
            return;
        }

        steps.Add(new AetherXivDependencyCheckStep("source-or-package-root", AetherXivDependencyStatus.Failed, $"Expected source solution {solutionPath} or published service payload under {config.WorkspaceRoot}."));
    }

    private static void AddServiceHostCheck(
        List<AetherXivDependencyCheckStep> steps,
        AetherXivServiceDefinition service,
        AetherXivOperatorConfig config)
    {
        string projectPath = service.ProjectPath(config);
        if (File.Exists(projectPath))
        {
            steps.Add(new AetherXivDependencyCheckStep(
                $"service-{service.Kind}",
                AetherXivDependencyStatus.Passed,
                $"{service.DisplayName} source project exists: {projectPath}"));
            return;
        }

        string executablePath = service.PublishedExecutablePath(config);
        if (File.Exists(executablePath))
        {
            steps.Add(new AetherXivDependencyCheckStep(
                $"service-{service.Kind}",
                AetherXivDependencyStatus.Passed,
                $"{service.DisplayName} published executable exists: {executablePath}"));
            return;
        }

        steps.Add(new AetherXivDependencyCheckStep(
            $"service-{service.Kind}",
            AetherXivDependencyStatus.Failed,
            $"{service.DisplayName} needs either source project {projectPath} or published executable {executablePath}."));
    }

    private static void AddDotnetCheck(
        List<AetherXivDependencyCheckStep> steps,
        string dotnetPath)
    {
        if (String.IsNullOrWhiteSpace(dotnetPath))
        {
            steps.Add(new AetherXivDependencyCheckStep("dotnet", AetherXivDependencyStatus.Failed, ".NET path is not configured."));
            return;
        }

        if (Path.IsPathRooted(dotnetPath))
        {
            steps.Add(File.Exists(dotnetPath)
                ? new AetherXivDependencyCheckStep("dotnet", AetherXivDependencyStatus.Passed, $".NET executable exists: {dotnetPath}")
                : new AetherXivDependencyCheckStep("dotnet", AetherXivDependencyStatus.Failed, $".NET executable is missing: {dotnetPath}"));
            return;
        }

        steps.Add(new AetherXivDependencyCheckStep("dotnet", AetherXivDependencyStatus.Warning, $".NET executable will be resolved from PATH at launch: {dotnetPath}"));
    }

    private static void AddDiagnosticsCheck(
        List<AetherXivDependencyCheckStep> steps,
        string diagnosticsDirectory)
    {
        try
        {
            Directory.CreateDirectory(diagnosticsDirectory);
            string probePath = Path.Combine(diagnosticsDirectory, $".aetherxiv-ui-write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probePath, "ok");
            File.Delete(probePath);
            steps.Add(new AetherXivDependencyCheckStep("diagnostics", AetherXivDependencyStatus.Passed, $"Diagnostics directory is writable: {diagnosticsDirectory}"));
        }
        catch (Exception ex)
        {
            steps.Add(new AetherXivDependencyCheckStep("diagnostics", AetherXivDependencyStatus.Failed, $"Diagnostics directory is not writable: {ex.Message}"));
        }
    }
}
