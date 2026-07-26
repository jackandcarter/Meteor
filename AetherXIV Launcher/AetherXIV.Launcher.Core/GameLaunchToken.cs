using System.Text;
using AetherXIV.Core.Common;

namespace AetherXIV.Launcher.Core;

public sealed record GameLaunchToken(string Token, uint TickCount)
{
    public string LaunchArgument => $" sqex0002{Token}!////";
}

public static class GameLaunchTokenGenerator
{
    public static GameLaunchToken Generate(string sessionId)
    {
        return Generate(sessionId, () => (uint)(Environment.TickCount & int.MaxValue));
    }

    public static GameLaunchToken Generate(string sessionId, Func<uint> tickProvider)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session id is required.", nameof(sessionId));

        sessionId = sessionId.Trim();

        if (sessionId.StartsWith("sessionId=", StringComparison.OrdinalIgnoreCase))
            sessionId = sessionId["sessionId=".Length..];

        if (sessionId.Length != 56)
        {
            throw new InvalidOperationException(
                $"Session id has unexpected size. Expected 56 characters, got {sessionId.Length}.");
        }

        uint tickCount = tickProvider();

        // Keep the legacy 1.23b launcher command shape.
        // Only change the session payload: raw 56-char session, no "sessionId=" prefix.
        string commandLine =
            $" T ={tickCount} /LANG =en-us /REGION =2 /SERVER_UTC =1356916742 /SESSION_ID ={sessionId}";

        byte[] commandBytes = Encoding.ASCII.GetBytes($"{commandLine}\0");
        byte[] key = Encoding.ASCII.GetBytes((tickCount & ~0xFFFFu).ToString("x8"));

        Blowfish blowfish = new(key);

        int encryptedLength = commandBytes.Length & ~0x7;
        if (encryptedLength > 0)
            blowfish.Encipher(commandBytes, 0, encryptedLength);

        string token = Convert.ToBase64String(commandBytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal);

        return new GameLaunchToken(token, tickCount);
    }
}
