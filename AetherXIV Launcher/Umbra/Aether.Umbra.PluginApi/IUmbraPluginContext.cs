namespace Aether.Umbra.PluginApi;

public interface IUmbraPluginContext
{
    string PluginId { get; }

    string ApiVersion { get; }

    string FrameworkVersion { get; }

    string ConfigDirectory { get; }

    IReadOnlyCollection<string> Capabilities { get; }

    CancellationToken ShutdownToken { get; }

    IUmbraLogger Logger { get; }

    bool HasCapability(string capability);

    TService? GetService<TService>() where TService : class;
}
