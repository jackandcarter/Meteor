using System.Runtime.InteropServices;
using Aether.Umbra.Framework;

namespace AetherXIV.Launcher.Tests;

public sealed class UmbraRenderBridgeTests
{
    [Fact]
    public void NativeRenderEventAbiIsStable()
    {
        Assert.Equal(1u, UmbraNativeRenderEvent.CurrentAbiVersion);
        Assert.Equal(32, Marshal.SizeOf<UmbraNativeRenderEvent>());
    }

    [Fact]
    public async Task RenderBridgeDispatchesFramesAndDeviceResetEvents()
    {
        string root = CreateTempDirectory();
        using UmbraRuntime runtime = await StartSafeModeRuntimeAsync(root);

        UmbraNativeRenderEvent frame = CreateEvent(UmbraNativeRenderEventKind.Frame) with
        {
            FrameNumber = 42,
            DeltaSeconds = 1.0f / 60.0f,
            ViewportWidth = 1280,
            ViewportHeight = 720
        };

        Assert.Equal(0, runtime.RenderBridge.Process(frame));
        Assert.Equal(Environment.CurrentManagedThreadId, runtime.RenderBridge.RenderThreadId);
        Assert.Equal(42, runtime.RenderBridge.FrameCount);

        Assert.Equal(0, runtime.RenderBridge.Process(CreateEvent(UmbraNativeRenderEventKind.BeforeReset)));
        Assert.Equal(0, runtime.RenderBridge.DeviceGeneration);
        Assert.Equal(0, runtime.RenderBridge.Process(CreateEvent(UmbraNativeRenderEventKind.AfterReset)));
        Assert.Equal(1, runtime.RenderBridge.DeviceGeneration);
    }

    [Fact]
    public async Task RenderBridgeRejectsIncompatibleAbiAndThreadChanges()
    {
        string root = CreateTempDirectory();
        using UmbraRuntime runtime = await StartSafeModeRuntimeAsync(root);

        UmbraNativeRenderEvent incompatible = CreateEvent(UmbraNativeRenderEventKind.Frame) with
        {
            AbiVersion = UmbraNativeRenderEvent.CurrentAbiVersion + 1
        };
        Assert.Equal(-2, runtime.RenderBridge.Process(incompatible));

        UmbraNativeRenderEvent frame = CreateEvent(UmbraNativeRenderEventKind.Frame) with { FrameNumber = 1 };
        Assert.Equal(0, runtime.RenderBridge.Process(frame));
        int otherThreadResult = int.MinValue;
        Thread otherThread = new(() => otherThreadResult = runtime.RenderBridge.Process(frame));
        otherThread.Start();
        Assert.True(otherThread.Join(TimeSpan.FromSeconds(5)), "Render-thread rejection did not complete.");
        Assert.Equal(-4, otherThreadResult);
    }

    private static UmbraNativeRenderEvent CreateEvent(UmbraNativeRenderEventKind kind)
    {
        return new UmbraNativeRenderEvent
        {
            Size = (uint)Marshal.SizeOf<UmbraNativeRenderEvent>(),
            AbiVersion = UmbraNativeRenderEvent.CurrentAbiVersion,
            Kind = kind
        };
    }

    private static Task<UmbraRuntime> StartSafeModeRuntimeAsync(string root)
    {
        string cache = Path.Combine(root, "Cache");
        string devBridge = Path.Combine(cache, "DevBridge");
        string logPath = Path.Combine(root, "Logs", "umbra.log");
        UmbraRuntimeOptions options = new(
            logPath,
            Path.Combine(root, "Plugins"),
            cache,
            devBridge,
            Path.Combine(devBridge, "control.json"),
            false,
            UmbraRuntimeOptions.DefaultDevBridgePort,
            true,
            Array.Empty<string>(),
            Array.Empty<UmbraRepositorySource>());
        return UmbraRuntime.StartAsync(options, UmbraRuntimeLog.Open(logPath));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetherxiv-render-bridge-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
