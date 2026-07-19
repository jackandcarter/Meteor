using Aether.Umbra.PluginApi;

namespace Aether.Umbra.SamplePlugin;

/// <summary>
/// Test fixture used to verify that Umbra quarantines repeatedly failing plugins.
/// It is not the entry point selected by the sample manifest.
/// </summary>
public sealed class FaultingSamplePlugin : IUmbraPlugin
{
    public string Name => "Umbra Fault Containment Fixture";

    public void Initialize(IUmbraPluginContext context)
    {
        context.Logger.Info("initialized for fault-containment test");
    }

    public void Update(TimeSpan delta)
    {
        throw new InvalidOperationException("Intentional sample update failure.");
    }

    public void Draw(IUmbraDrawContext drawContext)
    {
    }

    public void Dispose()
    {
    }
}
