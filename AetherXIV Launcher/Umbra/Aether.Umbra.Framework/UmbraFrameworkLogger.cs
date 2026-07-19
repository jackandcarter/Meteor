using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

public sealed class UmbraFrameworkLogger(UmbraRuntimeLog log, string scope) : IUmbraLogger
{
    public void Info(string message)
    {
        log.Info($"{scope}.info={message}");
    }

    public void Warning(string message)
    {
        log.Warning($"{scope}.warning={message}");
    }

    public void Error(string message, Exception? exception = null)
    {
        if (exception is null)
            log.Error($"{scope}.error={message}");
        else
            log.Error($"{scope}.error={message}", exception);
    }
}
