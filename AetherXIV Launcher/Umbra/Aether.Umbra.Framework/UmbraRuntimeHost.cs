namespace Aether.Umbra.Framework;

public static class UmbraRuntimeHost
{
    private static readonly object Gate = new();
    private static UmbraRuntime? current;

    public static async Task<UmbraRuntime> StartOrGetAsync(UmbraRuntimeOptions options, UmbraRuntimeLog log)
    {
        lock (Gate)
        {
            if (current is not null)
                return current;
        }

        UmbraRuntime runtime = await UmbraRuntime.StartAsync(options, log).ConfigureAwait(false);
        lock (Gate)
        {
            if (current is not null)
            {
                runtime.Dispose();
                return current;
            }

            current = runtime;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
            return current;
        }
    }

    public static void Stop()
    {
        UmbraRuntime? runtime;
        lock (Gate)
        {
            runtime = current;
            current = null;
        }

        runtime?.Dispose();
    }

    public static bool TryGet(out UmbraRuntime? runtime)
    {
        lock (Gate)
        {
            runtime = current;
            return runtime is not null;
        }
    }
}
