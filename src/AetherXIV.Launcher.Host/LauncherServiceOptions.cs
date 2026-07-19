using AetherXIV.Core;
using AetherXIV.Data;

namespace AetherXIV.Launcher.Host;

public sealed record LauncherServiceOptions(
    ServerEndpoint BindEndpoint,
    MariaDbOptions Database,
    bool AllowLocalAccountCreation,
    TimeSpan SessionLifetime)
{
    public static LauncherServiceOptions FromArgs(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        Dictionary<string, string> values = ParseArgs(args);
        return new LauncherServiceOptions(
            ReadEndpoint(values, "bind", "AETHERXIV_LAUNCHER_BIND", new ServerEndpoint("127.0.0.1", 8080)),
            new MariaDbOptions(
                ReadString(values, "db-host", "AETHERXIV_DB_HOST", "127.0.0.1"),
                ReadUShort(values, "db-port", "AETHERXIV_DB_PORT", 3306),
                ReadString(values, "db-name", "AETHERXIV_DB_NAME", "ffxiv_server"),
                ReadString(values, "db-user", "AETHERXIV_DB_USER", "aetherxiv"),
                ReadString(values, "db-password", "AETHERXIV_DB_PASSWORD", "aether_dev")),
            ReadBool(values, "allow-account-create", "AETHERXIV_LAUNCHER_ALLOW_ACCOUNT_CREATE", true),
            TimeSpan.FromHours(ReadInt(values, "session-hours", "AETHERXIV_LAUNCHER_SESSION_HOURS", 24)));
    }

    private static Dictionary<string, string> ParseArgs(IReadOnlyList<string> args)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < args.Count; index++)
        {
            string arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
                continue;

            string key = arg[2..];
            string value = "true";
            int equals = key.IndexOf('=', StringComparison.Ordinal);
            if (equals >= 0)
            {
                value = key[(equals + 1)..];
                key = key[..equals];
            }
            else if (index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++index];
            }

            values[key] = value;
        }

        return values;
    }

    private static ServerEndpoint ReadEndpoint(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentKey,
        ServerEndpoint fallback)
    {
        string? raw = ReadOptionalString(values, key, environmentKey);
        return raw is null ? fallback : ParseEndpoint(raw);
    }

    private static ServerEndpoint ParseEndpoint(string raw)
    {
        string[] parts = raw.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || String.IsNullOrWhiteSpace(parts[0]) || !UInt16.TryParse(parts[1], out ushort port))
            throw new FormatException($"Endpoint '{raw}' must be in host:port form.");

        return new ServerEndpoint(parts[0], port);
    }

    private static string ReadString(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentKey,
        string fallback)
    {
        return ReadOptionalString(values, key, environmentKey) ?? fallback;
    }

    private static string? ReadOptionalString(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentKey)
    {
        if (values.TryGetValue(key, out string? value) && !String.IsNullOrWhiteSpace(value))
            return value;

        string? env = Environment.GetEnvironmentVariable(environmentKey);
        return String.IsNullOrWhiteSpace(env) ? null : env;
    }

    private static bool ReadBool(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentKey,
        bool fallback)
    {
        string? raw = ReadOptionalString(values, key, environmentKey);
        return raw is null ? fallback : Boolean.Parse(raw);
    }

    private static int ReadInt(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentKey,
        int fallback)
    {
        string? raw = ReadOptionalString(values, key, environmentKey);
        return raw is null ? fallback : Int32.Parse(raw);
    }

    private static ushort ReadUShort(
        IReadOnlyDictionary<string, string> values,
        string key,
        string environmentKey,
        ushort fallback)
    {
        string? raw = ReadOptionalString(values, key, environmentKey);
        return raw is null ? fallback : UInt16.Parse(raw);
    }
}
