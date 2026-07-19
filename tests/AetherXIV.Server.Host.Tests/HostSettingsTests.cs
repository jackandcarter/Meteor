using AetherXIV.Lobby;
using AetherXIV.Map;
using AetherXIV.Server.Host;
using AetherXIV.World;

namespace AetherXIV.Server.Host.Tests;

public sealed class HostSettingsTests
{
    private static Func<string, string?> EnvFrom(Dictionary<string, string> values)
    {
        return key => values.TryGetValue(key, out string? value) ? value : null;
    }

    [Theory]
    [InlineData("lobby")]
    [InlineData("world")]
    [InlineData("map")]
    public void ResolveDefaultsToTheFoundationEndpointForEachService(string service)
    {
        HostSettings settings = HostSettings.Resolve(service, EnvFrom([]));

        (string expectedName, string expectedHost, ushort expectedPort) = service switch
        {
            "lobby" => (LobbyFoundation.ServiceName, LobbyFoundation.DefaultEndpoint.Host, LobbyFoundation.DefaultEndpoint.Port),
            "world" => (WorldFoundation.ServiceName, WorldFoundation.DefaultEndpoint.Host, WorldFoundation.DefaultEndpoint.Port),
            "map" => (MapFoundation.ServiceName, MapFoundation.DefaultEndpoint.Host, MapFoundation.DefaultEndpoint.Port),
            _ => throw new InvalidOperationException()
        };

        Assert.Equal(expectedName, settings.ServiceName);
        Assert.Equal(expectedName, settings.Runtime.ServiceName);
        Assert.Equal(expectedHost, settings.Runtime.BindEndpoint.Host);
        Assert.Equal(expectedPort, settings.Runtime.BindEndpoint.Port);
    }

    [Fact]
    public void CliArgBeatsEnvVariable()
    {
        Dictionary<string, string> env = new() { ["AETHERXIV_SERVICE"] = "map" };

        HostSettings settings = HostSettings.Resolve("lobby", EnvFrom(env));

        Assert.Equal(LobbyFoundation.ServiceName, settings.ServiceName);
    }

    [Fact]
    public void EnvOverridesBindHostAndPort()
    {
        Dictionary<string, string> env = new()
        {
            ["AETHERXIV_BIND_HOST"] = "0.0.0.0",
            ["AETHERXIV_BIND_PORT"] = "9999"
        };

        HostSettings settings = HostSettings.Resolve("world", EnvFrom(env));

        Assert.Equal("0.0.0.0", settings.Runtime.BindEndpoint.Host);
        Assert.Equal((ushort)9999, settings.Runtime.BindEndpoint.Port);
    }

    [Fact]
    public void EnvOverridesEveryDatabaseField()
    {
        Dictionary<string, string> env = new()
        {
            ["AETHERXIV_DB_HOST"] = "db.internal",
            ["AETHERXIV_DB_PORT"] = "3307",
            ["AETHERXIV_DB_NAME"] = "customdb",
            ["AETHERXIV_DB_USER"] = "customuser",
            ["AETHERXIV_DB_PASSWORD"] = "custompass"
        };

        HostSettings settings = HostSettings.Resolve("lobby", EnvFrom(env));

        Assert.Equal("db.internal", settings.Database.Host);
        Assert.Equal((ushort)3307, settings.Database.Port);
        Assert.Equal("customdb", settings.Database.Database);
        Assert.Equal("customuser", settings.Database.User);
        Assert.Equal("custompass", settings.Database.Password);
    }

    [Fact]
    public void DatabaseFieldsDefaultWhenUnset()
    {
        HostSettings settings = HostSettings.Resolve("lobby", EnvFrom([]));

        Assert.Equal("localhost", settings.Database.Host);
        Assert.Equal((ushort)3306, settings.Database.Port);
        Assert.Equal("aetherxiv2", settings.Database.Database);
        Assert.Equal("aetherxiv", settings.Database.User);
        Assert.Equal("aether_dev", settings.Database.Password);
    }

    [Fact]
    public void UnknownServiceThrowsWithValidValuesListed()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => HostSettings.Resolve("nonsense", EnvFrom([])));

        Assert.Contains("lobby", ex.Message);
        Assert.Contains("world", ex.Message);
        Assert.Contains("map", ex.Message);
    }

    [Fact]
    public void MissingServiceThrowsWithValidValuesListed()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () => HostSettings.Resolve(null, EnvFrom([])));

        Assert.Contains("lobby", ex.Message);
        Assert.Contains("world", ex.Message);
        Assert.Contains("map", ex.Message);
    }

    [Fact]
    public void NonNumericBindPortThrows()
    {
        Dictionary<string, string> env = new() { ["AETHERXIV_BIND_PORT"] = "not-a-port" };

        Assert.Throws<ArgumentException>(() => HostSettings.Resolve("lobby", EnvFrom(env)));
    }

    [Fact]
    public void DbWaitSecondsOverrideWorks()
    {
        Dictionary<string, string> env = new() { ["AETHERXIV_DB_WAIT_SECONDS"] = "120" };

        HostSettings settings = HostSettings.Resolve("lobby", EnvFrom(env));

        Assert.Equal(TimeSpan.FromSeconds(120), settings.DbWait);
    }

    [Fact]
    public void DbWaitSecondsDefaultsToSixty()
    {
        HostSettings settings = HostSettings.Resolve("lobby", EnvFrom([]));

        Assert.Equal(TimeSpan.FromSeconds(60), settings.DbWait);
    }
}
