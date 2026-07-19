using System.Globalization;
using AetherXIV.Core;

namespace AetherXIV.Server.Host;

public sealed class ConsoleDiagnosticSink : IDiagnosticSink
{
    private readonly object gate = new();

    public void Trace(string eventName, IReadOnlyDictionary<string, object?> fields)
    {
        string timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        string pairs = string.Join(' ', fields.Select(field => $"{field.Key}={field.Value ?? "-"}"));
        string line = pairs.Length > 0 ? $"{timestamp} {eventName} {pairs}" : $"{timestamp} {eventName}";

        lock (gate)
        {
            Console.Out.WriteLine(line);
        }
    }
}
