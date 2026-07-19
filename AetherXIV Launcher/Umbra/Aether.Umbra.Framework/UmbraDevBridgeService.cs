using System.Net;
using System.Text;
using System.Text.Json;

namespace Aether.Umbra.Framework;

public sealed class UmbraDevBridgeService : IDisposable
{
    private readonly UmbraRuntimeOptions options;
    private readonly UmbraRuntimeLog log;
    private readonly UmbraReadOnlyMemory memory;
    private readonly UmbraDevBridgeEvents events;
    private readonly object gate = new();
    private HttpListener? listener;
    private CancellationTokenSource? serverStop;
    private Task? serverTask;
    private int activePort;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public UmbraDevBridgeService(UmbraRuntimeOptions options, UmbraRuntimeLog log, UmbraReadOnlyMemory memory)
    {
        this.options = options;
        this.log = log;
        this.memory = memory;
        events = new UmbraDevBridgeEvents(options, log);
    }

    public bool IsRunning
    {
        get
        {
            lock (gate)
                return listener is not null;
        }
    }

    public object Status => new
    {
        running = IsRunning,
        host = "127.0.0.1",
        port = activePort == 0 ? options.DevBridgePort : activePort,
        control_path = options.DevBridgeControlPath,
        capture = events.CaptureStatus(),
        process = memory.ProcessStatus()
    };

    public Task StartAsync(int port, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            if (listener is not null)
                return Task.CompletedTask;

            activePort = port is >= 1024 and <= 65535 ? port : options.DevBridgePort;
            HttpListener http = new();
            http.Prefixes.Add($"http://127.0.0.1:{activePort}/");
            http.Start();
            listener = http;
            serverStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            serverTask = Task.Run(() => ServeAsync(http, serverStop.Token));
            log.Info($"umbra_dev_bridge_started host=127.0.0.1 port={activePort}");
            events.Record("bridge.start", new { host = "127.0.0.1", port = activePort });
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        HttpListener? http;
        CancellationTokenSource? stop;
        Task? task;
        lock (gate)
        {
            http = listener;
            stop = serverStop;
            task = serverTask;
            listener = null;
            serverStop = null;
            serverTask = null;
        }

        if (http is null)
            return;

        events.Record("bridge.stop");
        log.Info("umbra_dev_bridge_stopping=true");
        stop?.Cancel();
        http.Close();
        if (task is not null)
        {
            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch
            {
                // Listener shutdown often interrupts a pending accept.
            }
        }

        stop?.Dispose();
        log.Info("umbra_dev_bridge_stopped=true");
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    private async Task ServeAsync(HttpListener http, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await http.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (!cancellationToken.IsCancellationRequested)
                    log.Warning("umbra_dev_bridge_accept_failed=true");
                return;
            }

            _ = Task.Run(() => HandleAsync(context, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        string path = context.Request.Url?.AbsolutePath ?? "/";
        string remote = context.Request.RemoteEndPoint?.Address.ToString() ?? "";
        if (!IPAddress.IsLoopback(context.Request.RemoteEndPoint?.Address ?? IPAddress.None))
        {
            log.Warning($"umbra_dev_bridge_rejected_remote address={remote}");
            await WriteJsonAsync(context, 403, new { error = "loopback only" }, cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            log.Info($"umbra_dev_bridge_request method={context.Request.HttpMethod} path={path} remote={remote}");
            events.Record("bridge.request", new { method = context.Request.HttpMethod, path });

            if (context.Request.HttpMethod == "GET" && path == "/status")
            {
                await WriteJsonAsync(context, 200, Status, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/events")
            {
                int limit = ParseQueryInt(context, "limit", 100, 1, 512);
                await WriteJsonAsync(context, 200, new { events = events.Recent(limit) }, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/logs")
            {
                int limit = ParseQueryInt(context, "limit", 120, 1, 1000);
                await WriteJsonAsync(context, 200, new { lines = ReadLastLines(options.LogPath, limit) }, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/capture/start")
            {
                using JsonDocument body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
                string? name = ReadString(body.RootElement, "name");
                await WriteJsonAsync(context, 200, events.StartCapture(name), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/capture/pause")
            {
                await WriteJsonAsync(context, 200, events.PauseCapture(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/capture/stop")
            {
                await WriteJsonAsync(context, 200, events.StopCapture(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/memory/peek")
            {
                using JsonDocument body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
                nuint address = ReadAddress(body.RootElement, "address");
                int size = ReadInt(body.RootElement, "size", 64);
                UmbraMemoryPeekResult result = memory.Peek(address, size);
                events.Record("memory.peek", new { result.Address, result.RequestedSize, result.ReadSize, result.Success, result.Error });
                await WriteJsonAsync(context, result.Success ? 200 : 400, result with { Bytes = Array.Empty<byte>() }, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/scan/pattern")
            {
                using JsonDocument body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
                nuint start = ReadAddress(body.RootElement, "start");
                int size = ReadInt(body.RootElement, "size", 4096);
                string? patternText = ReadString(body.RootElement, "pattern");
                if (!UmbraBytePattern.TryParse(patternText, out UmbraBytePattern pattern, out string? error))
                {
                    await WriteJsonAsync(context, 400, new { error }, cancellationToken).ConfigureAwait(false);
                    return;
                }

                UmbraMemoryScanResult result = memory.Scan(start, size, pattern);
                events.Record("memory.scan", new { result.Start, result.Size, matches = result.Matches.Count, result.Success, result.Error });
                await WriteJsonAsync(context, result.Success ? 200 : 400, result, cancellationToken).ConfigureAwait(false);
                return;
            }

            await WriteJsonAsync(context, 404, new { error = "not found" }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Error($"umbra_dev_bridge_request_failed path={path}", ex);
            await WriteJsonAsync(context, 500, new { error = ex.Message }, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<JsonDocument> ReadBodyAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(context.Request.InputStream, context.Request.ContentEncoding);
        string body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
            body = "{}";

        return JsonDocument.Parse(body);
    }

    private static async Task WriteJsonAsync(
        HttpListenerContext context,
        int status,
        object payload,
        CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        context.Response.Close();
    }

    private static int ParseQueryInt(HttpListenerContext context, string key, int fallback, int min, int max)
    {
        string? value = context.Request.QueryString[key];
        return int.TryParse(value, out int parsed) ? Math.Clamp(parsed, min, max) : fallback;
    }

    private static string? ReadString(JsonElement element, string key)
    {
        return element.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int ReadInt(JsonElement element, string key, int fallback)
    {
        if (!element.TryGetProperty(key, out JsonElement value))
            return fallback;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            return number;

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
            return number;

        return fallback;
    }

    private static nuint ReadAddress(JsonElement element, string key)
    {
        string? text = ReadString(element, key);
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return nuint.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out nuint hex) ? hex : 0;

        return nuint.TryParse(text, out nuint dec) ? dec : 0;
    }

    private static IReadOnlyList<string> ReadLastLines(string path, int limit)
    {
        if (!File.Exists(path))
            return Array.Empty<string>();

        Queue<string> lines = new();
        foreach (string line in File.ReadLines(path))
        {
            lines.Enqueue(line);
            while (lines.Count > limit)
                lines.Dequeue();
        }

        return lines.ToArray();
    }
}
