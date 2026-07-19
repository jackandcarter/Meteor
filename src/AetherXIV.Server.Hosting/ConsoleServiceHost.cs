using AetherXIV.Core;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace AetherXIV.Server.Hosting;

public sealed class ConsoleDiagnosticSink : IDiagnosticSink
{
    private readonly string serviceName;

    public ConsoleDiagnosticSink(string serviceName)
    {
        this.serviceName = serviceName;
    }

    public void Trace(string eventName, IReadOnlyDictionary<string, object?> fields)
    {
        string details = String.Join(
            " ",
            fields.Select(item => $"{item.Key}={item.Value}"));
        Console.WriteLine($"[{DateTimeOffset.UtcNow:O}] [{serviceName}] {eventName} {details}");
    }
}

public sealed class FileDiagnosticSink : IDiagnosticSink
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string serviceName;
    private readonly string path;
    private readonly string runDirectory;
    private readonly string runId;
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly object syncRoot = new();
    private long sequence;

    public FileDiagnosticSink(string serviceName, string diagnosticsDirectory)
    {
        this.serviceName = serviceName;
        Directory.CreateDirectory(diagnosticsDirectory);
        runId = SanitizePathSegment(Environment.GetEnvironmentVariable("AETHERXIV_TRACE_RUN_ID"))
            ?? $"standalone-{DateTimeOffset.UtcNow:yyyyMMddTHHmmss.fffZ}-{Environment.ProcessId}";
        runDirectory = Path.Combine(diagnosticsDirectory, "runs", runId);
        Directory.CreateDirectory(runDirectory);
        string safeServiceName = String.Concat(serviceName.Select(character =>
            Char.IsLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '_'));
        path = Path.Combine(runDirectory, $"{safeServiceName}.jsonl");
        File.WriteAllText(Path.Combine(diagnosticsDirectory, "latest.txt"), runDirectory + Environment.NewLine);
        File.WriteAllText(
            Path.Combine(runDirectory, $"{safeServiceName}.manifest.json"),
            JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["schema"] = "aetherxiv.trace.manifest.v1",
                ["runId"] = runId,
                ["service"] = serviceName,
                ["processId"] = Environment.ProcessId,
                ["startedAt"] = DateTimeOffset.UtcNow,
                ["eventsFile"] = path
            }, JsonOptions));
    }

    public string EventsFilePath => path;

    public string RunDirectory => runDirectory;

    public void Trace(string eventName, IReadOnlyDictionary<string, object?> fields)
    {
        long nextSequence = Interlocked.Increment(ref sequence);
        Dictionary<string, object?> entry = new()
        {
            ["schema"] = "aetherxiv.trace.event.v1",
            ["timestamp"] = DateTimeOffset.UtcNow,
            ["unixTimeMilliseconds"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["elapsedMilliseconds"] = elapsed.Elapsed.TotalMilliseconds,
            ["runId"] = runId,
            ["service"] = serviceName,
            ["event"] = eventName,
            ["category"] = EventCategory(eventName),
            ["sequence"] = nextSequence,
            ["eventId"] = $"{serviceName}:{Environment.ProcessId}:{nextSequence}",
            ["processId"] = Environment.ProcessId,
            ["managedThreadId"] = Environment.CurrentManagedThreadId
        };

        foreach (KeyValuePair<string, object?> field in fields)
            entry[field.Key] = NormalizeValue(field.Value);

        string line = JsonSerializer.Serialize(entry, JsonOptions);
        lock (syncRoot)
            File.AppendAllText(path, line + Environment.NewLine);
    }

    private static object? NormalizeValue(object? value) => value switch
    {
        null => null,
        string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => value,
        DateTimeOffset timestamp => timestamp,
        DateTime timestamp => timestamp,
        Guid id => id,
        Enum enumeration => enumeration.ToString(),
        _ => value.ToString()
    };

    private static string EventCategory(string eventName)
    {
        int separator = eventName.IndexOf('.', StringComparison.Ordinal);
        return separator <= 0 ? eventName : eventName[..separator];
    }

    private static string? SanitizePathSegment(string? value)
    {
        if (String.IsNullOrWhiteSpace(value))
            return null;
        string safe = String.Concat(value.Select(character =>
            Char.IsLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '_'));
        return String.IsNullOrWhiteSpace(safe) ? null : safe;
    }
}

public sealed class CompositeDiagnosticSink : IDiagnosticSink
{
    private readonly IReadOnlyList<IDiagnosticSink> sinks;

    public CompositeDiagnosticSink(params IDiagnosticSink[] sinks)
    {
        this.sinks = sinks;
    }

    public void Trace(string eventName, IReadOnlyDictionary<string, object?> fields)
    {
        foreach (IDiagnosticSink sink in sinks)
            sink.Trace(eventName, fields);
    }
}

public static class ConsoleServiceHost
{
    public sealed record AssemblyIdentity(
        string Name,
        string Version,
        string ModuleVersionId,
        string? Path,
        string? Sha256,
        DateTimeOffset? LastWriteUtc,
        string? ReadError);

    public static AssemblyIdentity GetAssemblyIdentity(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        string? path = String.IsNullOrWhiteSpace(assembly.Location) ? null : assembly.Location;
        string? sha256 = null;
        DateTimeOffset? lastWriteUtc = null;
        string? readError = null;
        if (path is not null)
        {
            try
            {
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
                lastWriteUtc = File.GetLastWriteTimeUtc(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                readError = $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        return new AssemblyIdentity(
            assembly.GetName().Name ?? "unknown",
            assembly.GetName().Version?.ToString() ?? "unknown",
            assembly.ManifestModule.ModuleVersionId.ToString("D"),
            path,
            sha256,
            lastWriteUtc,
            readError);
    }

    public static async Task<int> RunUntilCancelledAsync(
        AetherXivServiceOptions options,
        Func<AetherXivServiceOptions, IDiagnosticSink, CancellationToken, ValueTask> onStart,
        CancellationToken cancellationToken = default,
        Func<AetherXivServiceOptions, IDiagnosticSink, CancellationToken, ValueTask>? onStop = null)
    {
        IDiagnosticSink diagnostics = CreateDiagnosticSink(options);
        AssemblyIdentity entryAssembly = GetAssemblyIdentity(
            Assembly.GetEntryAssembly() ?? typeof(ConsoleServiceHost).Assembly);
        diagnostics.Trace("service.config", new Dictionary<string, object?>
        {
            ["bind"] = options.BindEndpoint.ToString(),
            ["advertise"] = options.AdvertisedEndpoint.ToString(),
            ["database"] = $"{options.Database.User}@{options.Database.Host}:{options.Database.Port}/{options.Database.Database}",
            ["traceEnabled"] = options.TraceEnabled,
            ["diagnosticsDir"] = options.DiagnosticsDirectory,
            ["traceRunId"] = Environment.GetEnvironmentVariable("AETHERXIV_TRACE_RUN_ID"),
            ["traceRunDirectory"] = options.TraceEnabled && options.DiagnosticsDirectory is not null
                ? Path.Combine(options.DiagnosticsDirectory, "runs", Environment.GetEnvironmentVariable("AETHERXIV_TRACE_RUN_ID") ?? "standalone")
                : null,
            ["dataRoot"] = options.DataRoot,
            ["entryAssemblyName"] = entryAssembly.Name,
            ["entryAssemblyVersion"] = entryAssembly.Version,
            ["entryAssemblyMvid"] = entryAssembly.ModuleVersionId,
            ["entryAssemblyPath"] = entryAssembly.Path,
            ["entryAssemblySha256"] = entryAssembly.Sha256,
            ["entryAssemblyLastWriteUtc"] = entryAssembly.LastWriteUtc,
            ["entryAssemblyIdentityError"] = entryAssembly.ReadError
        });

        using CancellationTokenSource shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using IDisposable shutdownListener = ConsoleShutdownListener.Attach(
            shutdown,
            diagnostics,
            options.ServiceName);

        try
        {
            await onStart(options, diagnostics, shutdown.Token).ConfigureAwait(false);
            diagnostics.Trace("service.ready", new Dictionary<string, object?>
            {
                ["service"] = options.ServiceName
            });

            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, shutdown.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
            {
            }

            diagnostics.Trace("service.stopping", new Dictionary<string, object?>
            {
                ["service"] = options.ServiceName
            });
            if (onStop is not null)
                await onStop(options, diagnostics, CancellationToken.None).ConfigureAwait(false);

            diagnostics.Trace("service.stop", new Dictionary<string, object?>
            {
                ["service"] = options.ServiceName
            });
            return 0;
        }
        catch (Exception ex)
        {
            diagnostics.Trace("service.fatal", new Dictionary<string, object?>
            {
                ["error"] = ex.Message,
                ["exceptionType"] = ex.GetType().FullName
            });
            return 1;
        }
    }

    private static IDiagnosticSink CreateDiagnosticSink(AetherXivServiceOptions options)
    {
        ConsoleDiagnosticSink console = new(options.ServiceName);
        if (!options.TraceEnabled || String.IsNullOrWhiteSpace(options.DiagnosticsDirectory))
            return console;

        return new CompositeDiagnosticSink(
            console,
            new FileDiagnosticSink(options.ServiceName, options.DiagnosticsDirectory));
    }
}
