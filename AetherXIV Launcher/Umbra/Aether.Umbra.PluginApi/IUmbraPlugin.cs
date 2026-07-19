namespace Aether.Umbra.PluginApi;

public interface IUmbraPlugin : IDisposable
{
    string Name { get; }

    void Initialize(IUmbraPluginContext context);

    void Update(TimeSpan delta);

    void Draw(IUmbraDrawContext drawContext);
}
