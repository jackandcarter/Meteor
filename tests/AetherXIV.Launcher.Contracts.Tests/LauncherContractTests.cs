using System.Text.Json;
using AetherXIV.Launcher.Contracts;

namespace AetherXIV.Launcher.Contracts.Tests;

public sealed class LauncherContractTests
{
    [Fact]
    public void LocalAetherXiv2ProfileUsesCanonicalLocalPorts()
    {
        AetherXivLauncherServerProfile profile = AetherXivLauncherDefaults.LocalAetherXiv2;

        Assert.Equal(AetherXivServerGeneration.AetherXiv2, profile.Generation);
        Assert.Equal(8080, profile.LauncherEndpoint.Port);
        Assert.Equal(54994, profile.LobbyEndpoint.Port);
        Assert.Equal(54992, profile.WorldEndpoint.Port);
        Assert.Equal(1989, profile.MapEndpoint.Port);
    }

    [Fact]
    public void LocalConfigSerializesLauncherJsonContract()
    {
        string json = JsonSerializer.Serialize(AetherXivLauncherDefaults.LocalConfig);

        Assert.Contains("\"service_version\":1", json);
        Assert.Contains("\"server_name\":\"AetherXIV 2 Local\"", json);
        Assert.Contains("\"client_login_url\":\"../login/index.php\"", json);
        Assert.Contains("\"target_game_version\":\"2012.09.19.0001\"", json);
        Assert.Contains("\"plugin_catalog_urls\":[\"umbra/plugin-catalog\"]", json);
    }

    [Fact]
    public void AuthResponseUsesSessionIdJsonName()
    {
        LauncherAuthResponse response = new(true, "ok", "tester", "abc123");

        string json = JsonSerializer.Serialize(response);

        Assert.Contains("\"session_id\":\"abc123\"", json);
    }
}
