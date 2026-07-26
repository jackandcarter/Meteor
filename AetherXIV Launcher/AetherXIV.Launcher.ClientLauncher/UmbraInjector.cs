using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using AetherXIV.Launcher.Core;

namespace AetherXIV.Launcher.ClientLauncher;

internal static class UmbraInjector
{
    private const uint InjectionTimeoutMilliseconds = 10000;

    public static bool TryInject(
        IntPtr processHandle,
        uint processId,
        LaunchOptions options,
        Action<string>? log)
    {
        if (!options.Umbra.Enabled)
            return false;

        log?.Invoke("umbra_enabled=true");

        if (!options.Umbra.HasRequiredPaths)
        {
            log?.Invoke("umbra_injection_skipped=missing_required_paths");
            return false;
        }

        if (!File.Exists(options.Umbra.BootstrapPath))
        {
            log?.Invoke($"umbra_injection_skipped=bootstrap_missing");
            log?.Invoke($"umbra_bootstrap={options.Umbra.BootstrapPath}");
            return false;
        }

        if (!File.Exists(options.Umbra.FrameworkPath))
        {
            log?.Invoke("umbra_injection_skipped=framework_missing");
            log?.Invoke($"umbra_framework={options.Umbra.FrameworkPath}");
            return false;
        }

        string gameSha256 = UmbraCompatibility.ComputeSha256(options.GamePath);
        log?.Invoke($"umbra_game_sha256={gameSha256}");
        if (!UmbraCompatibility.IsKnownGameHash(gameSha256))
        {
            log?.Invoke("umbra_injection_skipped=unsupported_game_hash");
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.Umbra.LogPath)) ?? ".");
        Directory.CreateDirectory(options.Umbra.PluginDirectory);

        SetUmbraEnvironment(options.Umbra);

        try
        {
            if (IntPtr.Size == 8)
            {
                bool injected = TryInjectWithNativeX86Injector(processId, options.Umbra.BootstrapPath, options.LogPath, log);
                if (!injected)
                    return false;
            }
            else
            {
                InjectLoadLibrary(processHandle, options.Umbra.BootstrapPath, log);
            }

            log?.Invoke("umbra_injection_complete=true");
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke("umbra_injection_failed=true");
            log?.Invoke($"umbra_injection_error={ex.Message}");
            return false;
        }
    }

    internal static void SetUmbraEnvironment(UmbraLaunchOptions options)
    {
        Environment.SetEnvironmentVariable("AETHER_UMBRA_ENABLED", "1");
        Environment.SetEnvironmentVariable("AETHER_UMBRA_BOOTSTRAP", options.BootstrapPath);
        Environment.SetEnvironmentVariable("AETHER_UMBRA_FRAMEWORK", options.FrameworkPath);
        Environment.SetEnvironmentVariable("AETHER_UMBRA_PLUGIN_DIR", options.PluginDirectory);
        Environment.SetEnvironmentVariable("AETHER_UMBRA_CACHE_DIR", UmbraCacheDirectoryFor(options.PluginDirectory));
        Environment.SetEnvironmentVariable("AETHER_UMBRA_LOG", options.LogPath);
        Environment.SetEnvironmentVariable("AETHER_UMBRA_SAFE_MODE", options.SafeMode ? "1" : "0");
        Environment.SetEnvironmentVariable("AETHER_UMBRA_LOAD_DELAY_MS", options.LoadDelayMilliseconds.ToString());
        Environment.SetEnvironmentVariable("AETHER_UMBRA_REPOSITORY_URLS", string.Join(";", options.RepositoryUrls));
        Environment.SetEnvironmentVariable("AETHER_UMBRA_REPOSITORIES_JSON", options.RepositoriesJson);
        if (options.EnableManagedOnWine)
            Environment.SetEnvironmentVariable("AETHER_UMBRA_ENABLE_MANAGED_ON_WINE", "1");
        else
            Environment.SetEnvironmentVariable("AETHER_UMBRA_ENABLE_MANAGED_ON_WINE", null);
    }

    private static string UmbraCacheDirectoryFor(string pluginDirectory)
    {
        string pluginRoot = string.IsNullOrWhiteSpace(pluginDirectory)
            ? AppContext.BaseDirectory
            : Path.GetFullPath(pluginDirectory);
        return Path.Combine(Path.GetDirectoryName(pluginRoot) ?? pluginRoot, "Cache");
    }

    private static bool TryInjectWithNativeX86Injector(
        uint processId,
        string bootstrapPath,
        string helperLogPath,
        Action<string>? log)
    {
        string? injectorPath = FindNativeX86Injector();
        if (injectorPath is null)
        {
            log?.Invoke("umbra_injection_skipped=native_x86_injector_missing");
            return false;
        }

        log?.Invoke("umbra_injection_mode=native_x86_injector");
        log?.Invoke($"umbra_native_injector={injectorPath}");

        ProcessStartInfo startInfo = new()
        {
            FileName = injectorPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--pid");
        startInfo.ArgumentList.Add(processId.ToString());
        startInfo.ArgumentList.Add("--dll");
        startInfo.ArgumentList.Add(Path.GetFullPath(bootstrapPath));
        startInfo.ArgumentList.Add("--log");
        startInfo.ArgumentList.Add(Path.GetFullPath(helperLogPath));

        using Process? process = Process.Start(startInfo);
        if (process is null)
        {
            log?.Invoke("umbra_injection_failed=native_x86_injector_not_started");
            return false;
        }

        if (!process.WaitForExit((int)InjectionTimeoutMilliseconds + 5000))
        {
            log?.Invoke("umbra_injection_failed=native_x86_injector_timeout");
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                log?.Invoke($"umbra_native_injector_kill_failed={ex.Message}");
            }

            return false;
        }

        log?.Invoke($"umbra_native_injector_exit_code={process.ExitCode}");
        return process.ExitCode == 0;
    }

    private static string? FindNativeX86Injector()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(baseDirectory, "Umbra.NativeInjector.x86.exe"),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "win-x86", "Umbra.NativeInjector.x86.exe"))
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void InjectLoadLibrary(IntPtr processHandle, string dllPath, Action<string>? log)
    {
        string fullPath = Path.GetFullPath(dllPath);
        byte[] dllPathBytes = Encoding.Unicode.GetBytes(fullPath + '\0');
        IntPtr remotePath = NativeMethods.VirtualAllocEx(
            processHandle,
            IntPtr.Zero,
            (UIntPtr)dllPathBytes.Length,
            NativeMethods.AllocationType.MEM_COMMIT | NativeMethods.AllocationType.MEM_RESERVE,
            NativeMethods.MemoryProtectionFlags.PAGE_READWRITE);

        if (remotePath == IntPtr.Zero)
            throw new Win32Exception();

        try
        {
            if (!NativeMethods.WriteProcessMemory(
                    processHandle,
                    remotePath,
                    dllPathBytes,
                    (nuint)dllPathBytes.Length,
                    out nuint nativeBytesWritten))
            {
                throw new Win32Exception();
            }

            int bytesWritten = checked((int)nativeBytesWritten);
            if (bytesWritten != dllPathBytes.Length)
                throw new InvalidOperationException("Incomplete Umbra bootstrap path write.");

            IntPtr kernel32 = NativeMethods.GetModuleHandle("kernel32.dll");
            if (kernel32 == IntPtr.Zero)
                throw new Win32Exception();

            IntPtr loadLibrary = NativeMethods.GetProcAddress(kernel32, "LoadLibraryW");
            if (loadLibrary == IntPtr.Zero)
                throw new Win32Exception();

            log?.Invoke($"umbra_bootstrap={fullPath}");
            log?.Invoke($"umbra_remote_path=0x{remotePath.ToInt64():X}");

            IntPtr thread = NativeMethods.CreateRemoteThread(
                processHandle,
                IntPtr.Zero,
                0,
                loadLibrary,
                remotePath,
                0,
                out uint threadId);

            if (thread == IntPtr.Zero)
                throw new Win32Exception();

            try
            {
                log?.Invoke($"umbra_remote_thread_id={threadId}");
                uint waitResult = NativeMethods.WaitForSingleObject(thread, InjectionTimeoutMilliseconds);
                log?.Invoke($"umbra_remote_thread_wait=0x{waitResult:X8}");
                if (waitResult == NativeMethods.WaitFailed)
                    throw new Win32Exception();
                if (waitResult != NativeMethods.WaitObject0)
                    throw new TimeoutException("Umbra bootstrap injection timed out.");
            }
            finally
            {
                NativeMethods.CloseHandle(thread);
            }
        }
        finally
        {
            NativeMethods.VirtualFreeEx(
                processHandle,
                remotePath,
                UIntPtr.Zero,
                NativeMethods.FreeType.MEM_RELEASE);
        }
    }
}
