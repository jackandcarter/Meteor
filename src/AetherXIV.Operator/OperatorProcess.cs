using System.Diagnostics;

namespace AetherXIV.Operator;

public sealed class AetherXivServiceLogEventArgs : EventArgs
{
    public AetherXivServiceLogEventArgs(AetherXivManagedService service, string line, bool isError)
    {
        Service = service;
        Line = line;
        IsError = isError;
    }

    public AetherXivManagedService Service { get; }

    public string Line { get; }

    public bool IsError { get; }
}

public sealed class AetherXivServiceStateChangedEventArgs : EventArgs
{
    public AetherXivServiceStateChangedEventArgs(
        AetherXivManagedService service,
        AetherXivServiceRunState state,
        int? processId,
        int? exitCode)
    {
        Service = service;
        State = state;
        ProcessId = processId;
        ExitCode = exitCode;
    }

    public AetherXivManagedService Service { get; }

    public AetherXivServiceRunState State { get; }

    public int? ProcessId { get; }

    public int? ExitCode { get; }
}

public sealed class AetherXivServiceProcess : IDisposable
{
    private readonly object gate = new();
    private readonly AetherXivOperatorConfig config;
    private readonly string traceRunId;
    private readonly List<Process> exitedProcesses = [];
    private Process? process;

    public AetherXivServiceProcess(
        AetherXivServiceDefinition definition,
        AetherXivOperatorConfig config,
        string? traceRunId = null)
    {
        Definition = definition;
        this.config = config.Normalize();
        this.traceRunId = String.IsNullOrWhiteSpace(traceRunId)
            ? CreateTraceRunId()
            : traceRunId;
    }

    public event EventHandler<AetherXivServiceLogEventArgs>? LogReceived;

    public event EventHandler<AetherXivServiceStateChangedEventArgs>? StateChanged;

    public AetherXivServiceDefinition Definition { get; }

    public AetherXivServiceRunState State { get; private set; } = AetherXivServiceRunState.Stopped;

    public int? ProcessId { get; private set; }

    public int? ExitCode { get; private set; }

    public bool IsRunning => State is AetherXivServiceRunState.Starting or AetherXivServiceRunState.Running;

    public ProcessStartInfo CreateStartInfo()
    {
        string publishedExecutable = Definition.PublishedExecutablePath(config);
        bool usePublishedExecutable = File.Exists(publishedExecutable);
        ProcessStartInfo startInfo = new()
        {
            FileName = usePublishedExecutable ? publishedExecutable : config.DotnetPath,
            WorkingDirectory = usePublishedExecutable
                ? Path.GetDirectoryName(publishedExecutable) ?? config.WorkspaceRoot
                : config.WorkspaceRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        IReadOnlyList<string> arguments = usePublishedExecutable
            ? Definition.Arguments
            : Definition.BuildDotnetArguments(config);

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        startInfo.Environment["AETHERXIV_DEV_LOGGING"] = config.DevLogging.Enabled ? "1" : "0";
        startInfo.Environment["AETHERXIV_DEV_LOG_LEVEL"] = config.DevLogging.Level.ToString();
        startInfo.Environment["AETHERXIV_DEV_LOG_NETWORK"] = config.DevLogging.NetworkTrace ? "1" : "0";
        startInfo.Environment["AETHERXIV_DEV_LOG_SERVER"] = config.DevLogging.ServerTrace ? "1" : "0";
        startInfo.Environment["AETHERXIV_TRACE_RUN_ID"] = traceRunId;
        startInfo.Environment["AETHERXIV_DEV_DIAGNOSTICS"] = config.TraceEnabled ? "1" : "0";
        startInfo.Environment["AETHERXIV_DEV_DIAGNOSTICS_DIR"] = Path.Combine(
            config.DiagnosticsDirectory,
            traceRunId);

        return startInfo;
    }

    private static string CreateTraceRunId() =>
        $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmss.fffZ}-{Guid.NewGuid():N}";

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (IsRunning)
                return Task.CompletedTask;

            ExitCode = null;
            SetState(AetherXivServiceRunState.Starting);

            Process next = new()
            {
                StartInfo = CreateStartInfo(),
                EnableRaisingEvents = true
            };
            next.OutputDataReceived += (_, args) => EmitLog(args.Data, false);
            next.ErrorDataReceived += (_, args) => EmitLog(args.Data, true);
            next.Exited += (_, _) => HandleExited(next);

            if (!next.Start())
            {
                SetState(AetherXivServiceRunState.Failed);
                throw new InvalidOperationException($"Failed to start {Definition.DisplayName}.");
            }

            process = next;
            ProcessId = next.Id;
            EmitLog($"started pid={next.Id}", false);
            next.BeginOutputReadLine();
            next.BeginErrorReadLine();
            SetState(AetherXivServiceRunState.Running);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        Process? current;
        lock (gate)
        {
            current = process;
            if (current is null || current.HasExited)
            {
                SetState(AetherXivServiceRunState.Stopped);
                return;
            }

            SetState(AetherXivServiceRunState.Stopping);
        }

        EmitLog("shutdown requested", false);
        if (!TrySendShutdownCommand(current))
            TryCloseMainWindow(current);

        Task waitTask = current.WaitForExitAsync(cancellationToken);
        Task completed = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
        if (completed != waitTask && !HasExited(current))
        {
            EmitLog($"graceful shutdown timed out after {timeout.TotalSeconds:0.#}s; terminating process tree", true);
            try
            {
                current.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }

        if (!HasExited(current))
            await current.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
    }

    private bool TrySendShutdownCommand(Process current)
    {
        try
        {
            current.StandardInput.WriteLine(AetherXIV.Server.Hosting.ConsoleShutdownListener.ShutdownCommand);
            current.StandardInput.Flush();
            EmitLog("shutdown command sent", false);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void TryCloseMainWindow(Process current)
    {
        try
        {
            if (current.CloseMainWindow())
                EmitLog("close-main-window requested", false);
        }
        catch (InvalidOperationException)
        {
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            process?.Dispose();
            process = null;
            foreach (Process exitedProcess in exitedProcesses)
                exitedProcess.Dispose();
            exitedProcesses.Clear();
        }
    }

    private void HandleExited(Process exitedProcess)
    {
        lock (gate)
        {
            ExitCode = SafeExitCode(exitedProcess);
            ProcessId = null;
            if (ReferenceEquals(process, exitedProcess))
                process = null;
            SetState(ExitCode == 0 ? AetherXivServiceRunState.Exited : AetherXivServiceRunState.Failed);
            EmitLog($"exited code={ExitCode}", ExitCode != 0);
            exitedProcesses.Add(exitedProcess);
        }
    }

    private static bool HasExited(Process current)
    {
        try
        {
            return current.HasExited;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static int? SafeExitCode(Process current)
    {
        try
        {
            return current.ExitCode;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private void EmitLog(string? line, bool isError)
    {
        if (String.IsNullOrWhiteSpace(line))
            return;

        LogReceived?.Invoke(this, new AetherXivServiceLogEventArgs(Definition.Kind, line, isError));
    }

    private void SetState(AetherXivServiceRunState state)
    {
        State = state;
        StateChanged?.Invoke(this, new AetherXivServiceStateChangedEventArgs(
            Definition.Kind,
            State,
            ProcessId,
            ExitCode));
    }
}

public sealed class AetherXivServiceSupervisor : IDisposable
{
    private readonly IReadOnlyList<AetherXivServiceProcess> processes;

    public AetherXivServiceSupervisor(AetherXivOperatorConfig config)
    {
        AetherXivOperatorConfig normalized = config.Normalize();
        string traceRunId = $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmss.fffZ}-{Guid.NewGuid():N}";
        processes = AetherXivServiceCatalog.CreateDefault(normalized)
            .Select(definition => new AetherXivServiceProcess(definition, normalized, traceRunId))
            .OrderBy(process => process.Definition.StartOrder)
            .ToArray();

        foreach (AetherXivServiceProcess process in processes)
        {
            process.LogReceived += (_, args) => LogReceived?.Invoke(this, args);
            process.StateChanged += (_, args) => StateChanged?.Invoke(this, args);
        }
    }

    public event EventHandler<AetherXivServiceLogEventArgs>? LogReceived;

    public event EventHandler<AetherXivServiceStateChangedEventArgs>? StateChanged;

    public IReadOnlyList<AetherXivServiceProcess> Processes => processes;

    public bool HasRunningServices => processes.Any(process => process.IsRunning);

    public Task StartAsync(AetherXivManagedService service, CancellationToken cancellationToken = default) =>
        Find(service).StartAsync(cancellationToken);

    public Task StopAsync(
        AetherXivManagedService service,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default) =>
        Find(service).StopAsync(timeout ?? TimeSpan.FromSeconds(5), cancellationToken);

    public async Task StartStackAsync(CancellationToken cancellationToken = default)
    {
        foreach (AetherXivServiceProcess process in processes.OrderBy(process => process.Definition.StartOrder))
            await process.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopStackAsync(CancellationToken cancellationToken = default)
    {
        foreach (AetherXivManagedService service in AetherXivServiceCatalog.GracefulShutdownOrder)
        {
            AetherXivServiceProcess process = Find(service);
            await process.StopAsync(StopTimeoutFor(service), cancellationToken).ConfigureAwait(false);
        }
    }

    public AetherXivServiceProcess Find(AetherXivManagedService service) =>
        processes.First(process => process.Definition.Kind == service);

    public void Dispose()
    {
        foreach (AetherXivServiceProcess process in processes)
            process.Dispose();
    }

    private static TimeSpan StopTimeoutFor(AetherXivManagedService service) =>
        service == AetherXivManagedService.Map
            ? TimeSpan.FromSeconds(20)
            : TimeSpan.FromSeconds(10);
}
