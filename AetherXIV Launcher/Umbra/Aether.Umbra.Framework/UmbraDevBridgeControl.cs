using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aether.Umbra.Framework;

public sealed record UmbraDevBridgeControl(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("port")] int Port,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static UmbraDevBridgeControl Ensure(string path, bool enabled, int port)
    {
        if (TryRead(path) is { } existing)
            return existing;

        UmbraDevBridgeControl control = new(enabled, port, DateTimeOffset.UtcNow);
        Write(path, control);
        return control;
    }

    public static UmbraDevBridgeControl? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            UmbraDevBridgeControl? control = JsonSerializer.Deserialize<UmbraDevBridgeControl>(json, JsonOptions);
            if (control is null)
                return null;

            return control with
            {
                Port = control.Port is >= 1024 and <= 65535
                    ? control.Port
                    : UmbraRuntimeOptions.DefaultDevBridgePort
            };
        }
        catch
        {
            return null;
        }
    }

    public static void Write(string path, UmbraDevBridgeControl control)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(control, JsonOptions));
    }
}
