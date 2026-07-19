namespace Aether.Umbra.PluginApi;

public interface IUmbraLogger
{
    void Info(string message);

    void Warning(string message);

    void Error(string message, Exception? exception = null);
}
