using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Aether.Umbra.Framework;

public static class UmbraInProcessEntryPoint
{
    private static readonly object Gate = new();
    private static bool started;

    [UnmanagedCallersOnly(EntryPoint = "UmbraBootstrap", CallConvs = [typeof(CallConvStdcall)])]
    public static int UmbraBootstrap()
    {
        return StartRuntimeThread(null, "hostfxr");
    }

    public static int UmbraBootstrapCoreClr(IntPtr args, int sizeBytes)
    {
        return StartRuntimeThread(ReadUtf16Argument(args, sizeBytes), "coreclr");
    }

    private static int StartRuntimeThread(string? earlyLogPath, string host)
    {
        lock (Gate)
        {
            if (started)
            {
                EarlyLog(earlyLogPath, $"umbra_managed_bootstrap_already_started host={host}");
                return 0;
            }

            started = true;
        }

        EarlyLog(earlyLogPath, $"umbra_managed_bootstrap_entered host={host}");
        Thread thread = new(() =>
        {
            try
            {
                EarlyLog(earlyLogPath, $"umbra_managed_runtime_thread_start host={host}");
                int result = UmbraBootstrapRunner.RunFromEnvironmentAsync(earlyLogPath).GetAwaiter().GetResult();
                EarlyLog(earlyLogPath, $"umbra_managed_runtime_thread_result={result}");
            }
            catch (Exception ex)
            {
                EarlyLog(earlyLogPath, $"umbra_managed_runtime_thread_failed error={ex}");
            }
        })
        {
            IsBackground = true,
            Name = "Aether Umbra Runtime"
        };
        thread.Start();
        return 0;
    }

    private static string? ReadUtf16Argument(IntPtr args, int sizeBytes)
    {
        if (args == IntPtr.Zero || sizeBytes <= 2)
            return null;

        int charCount = sizeBytes / 2;
        string? value = Marshal.PtrToStringUni(args, charCount);
        return string.IsNullOrWhiteSpace(value) ? null : value.TrimEnd('\0');
    }

    private static void EarlyLog(string? path, string message)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
            // Native bootstrap logging will still record the host-side outcome.
        }
    }
}
