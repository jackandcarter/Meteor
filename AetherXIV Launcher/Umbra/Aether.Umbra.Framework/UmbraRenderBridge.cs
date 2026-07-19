using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

internal enum UmbraNativeRenderEventKind : uint
{
    Frame = 1,
    BeforeReset = 2,
    AfterReset = 3
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct UmbraNativeRenderEvent
{
    public const uint CurrentAbiVersion = 1;

    public uint Size;
    public uint AbiVersion;
    public UmbraNativeRenderEventKind Kind;
    public uint FrameNumber;
    public float DeltaSeconds;
    public uint ViewportWidth;
    public uint ViewportHeight;
    public uint Reserved;

    public bool IsPluginManagerOpen => (Reserved & 1u) != 0;
}

public sealed class UmbraRenderBridge
{
    private readonly UmbraRuntime runtime;
    private int renderThreadId;
    private int deviceGeneration;
    private long frameCount;

    internal UmbraRenderBridge(UmbraRuntime runtime)
    {
        this.runtime = runtime;
    }

    public int RenderThreadId => Volatile.Read(ref renderThreadId);

    public int DeviceGeneration => Volatile.Read(ref deviceGeneration);

    public long FrameCount => Interlocked.Read(ref frameCount);

    internal int Process(in UmbraNativeRenderEvent renderEvent)
    {
        if (renderEvent.AbiVersion != UmbraNativeRenderEvent.CurrentAbiVersion
            || renderEvent.Size < Marshal.SizeOf<UmbraNativeRenderEvent>())
        {
            runtime.Log.Warning(
                $"umbra_render_bridge_abi_rejected version={renderEvent.AbiVersion} size={renderEvent.Size}");
            return -2;
        }

        switch (renderEvent.Kind)
        {
            case UmbraNativeRenderEventKind.Frame:
                return ProcessFrame(renderEvent);
            case UmbraNativeRenderEventKind.BeforeReset:
                runtime.Log.Info($"umbra_render_device_reset_begin generation={DeviceGeneration}");
                return 0;
            case UmbraNativeRenderEventKind.AfterReset:
                int generation = Interlocked.Increment(ref deviceGeneration);
                runtime.Log.Info($"umbra_render_device_reset_complete generation={generation}");
                return 0;
            default:
                runtime.Log.Warning($"umbra_render_bridge_event_unknown kind={(uint)renderEvent.Kind}");
                return -3;
        }
    }

    private int ProcessFrame(in UmbraNativeRenderEvent renderEvent)
    {
        int currentThread = Environment.CurrentManagedThreadId;
        int knownThread = Volatile.Read(ref renderThreadId);
        if (knownThread == 0)
        {
            Interlocked.CompareExchange(ref renderThreadId, currentThread, 0);
            knownThread = Volatile.Read(ref renderThreadId);
            runtime.Log.Info($"umbra_render_thread_managed_id={knownThread}");
        }

        if (knownThread != currentThread)
        {
            runtime.Log.Warning(
                $"umbra_render_thread_rejected expected={knownThread} actual={currentThread}");
            return -4;
        }

        TimeSpan delta = TimeSpan.FromSeconds(Math.Clamp(renderEvent.DeltaSeconds, 0.0f, 0.25f));
        runtime.SynchronizePluginManagerOpen(renderEvent.IsPluginManagerOpen);
        ulong frameNumber = renderEvent.FrameNumber;
        Interlocked.Exchange(ref frameCount, renderEvent.FrameNumber);

        UmbraDrawContext context = new(
            runtime,
            frameNumber,
            delta,
            (int)Math.Min(renderEvent.ViewportWidth, int.MaxValue),
            (int)Math.Min(renderEvent.ViewportHeight, int.MaxValue),
            DeviceGeneration,
            knownThread);

        runtime.Plugins.Update(delta);
        runtime.Draw(context);
        return 0;
    }
}

internal interface IUmbraDrawContextRecovery
{
    void RecoverAfterPluginCallback();
}

internal sealed class UmbraDrawContext(
    UmbraRuntime runtime,
    ulong frameNumber,
    TimeSpan deltaTime,
    int viewportWidth,
    int viewportHeight,
    int deviceGeneration,
    int renderThreadId) : IUmbraDrawContext, IUmbraDrawContextRecovery
{
    private int openWindowDepth;
    private int openChildDepth;

    public ulong FrameNumber { get; } = frameNumber;

    public TimeSpan DeltaTime { get; } = deltaTime;

    public int ViewportWidth { get; } = viewportWidth;

    public int ViewportHeight { get; } = viewportHeight;

    public float AvailableContentWidth
    {
        get
        {
            EnsureWindow();
            return Math.Max(0.0f, UmbraNativeUi.GetAvailableContentWidth());
        }
    }

    public float ContentRegionWidth
    {
        get
        {
            EnsureWindow();
            return Math.Max(0.0f, UmbraNativeUi.GetContentRegionWidth());
        }
    }

    public int DeviceGeneration { get; } = deviceGeneration;

    public bool IsRenderThread => Environment.CurrentManagedThreadId == renderThreadId;

    public bool IsPluginManagerOpen => runtime.PluginManager.IsOpen;

    public void RequestPluginManagerOpen()
    {
        EnsureRenderThread();
        runtime.RequestPluginManagerOpen();
        UmbraNativeUi.SetPluginManagerOpen(true);
    }

    public bool BeginWindow(string title, ref bool isOpen)
    {
        EnsureRenderThread();
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        int nativeOpen = isOpen ? 1 : 0;
        bool visible = UmbraNativeUi.BeginWindow(title, ref nativeOpen);
        isOpen = nativeOpen != 0;
        openWindowDepth++;
        return visible;
    }

    public void EndWindow()
    {
        EnsureRenderThread();
        if (openWindowDepth <= 0)
            throw new InvalidOperationException("Umbra EndWindow was called without a matching BeginWindow.");
        if (openChildDepth > 0)
            throw new InvalidOperationException("Umbra EndWindow was called while a child region is still open.");

        UmbraNativeUi.EndWindow();
        openWindowDepth--;
    }

    public void Text(string text)
    {
        EnsureWindow();
        UmbraNativeUi.Text(text ?? "");
    }

    public void Text(string text, UmbraTextTone tone)
    {
        EnsureWindow();
        if (!Enum.IsDefined(tone))
            throw new ArgumentOutOfRangeException(nameof(tone));
        UmbraNativeUi.Text(text ?? "", tone);
    }

    public void Text(string text, UmbraTextTone tone, UmbraTextStyle style)
    {
        EnsureWindow();
        if (!Enum.IsDefined(tone))
            throw new ArgumentOutOfRangeException(nameof(tone));
        if (!Enum.IsDefined(style))
            throw new ArgumentOutOfRangeException(nameof(style));
        UmbraNativeUi.Text(text ?? "", tone, style);
    }

    public bool InputText(string label, ref string value, string hint = "", int maximumLength = 256)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return UmbraNativeUi.InputText(label, ref value, hint, maximumLength);
    }

    public bool Button(string label)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return UmbraNativeUi.Button(label);
    }

    public bool Button(
        string label,
        UmbraButtonStyle style,
        UmbraIcon icon = UmbraIcon.None,
        float width = 0.0f,
        float height = 0.0f)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (!Enum.IsDefined(style))
            throw new ArgumentOutOfRangeException(nameof(style));
        if (!Enum.IsDefined(icon))
            throw new ArgumentOutOfRangeException(nameof(icon));
        return UmbraNativeUi.Button(label, style, icon, Math.Max(0.0f, width), Math.Max(0.0f, height));
    }

    public bool Checkbox(string label, ref bool value)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        int nativeValue = value ? 1 : 0;
        bool changed = UmbraNativeUi.Checkbox(label, ref nativeValue);
        value = nativeValue != 0;
        return changed;
    }

    public bool Toggle(string label, ref bool value)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        int nativeValue = value ? 1 : 0;
        bool changed = UmbraNativeUi.Toggle(label, ref nativeValue);
        value = nativeValue != 0;
        return changed;
    }

    public void SameLine()
    {
        EnsureWindow();
        UmbraNativeUi.SameLine();
    }

    public void Separator()
    {
        EnsureWindow();
        UmbraNativeUi.Separator();
    }

    public void Spacing(float height = 8.0f)
    {
        EnsureWindow();
        UmbraNativeUi.Spacing(Math.Max(0.0f, height));
    }

    public void Icon(UmbraIcon icon, UmbraTextTone tone = UmbraTextTone.Normal, float size = 20.0f)
    {
        EnsureWindow();
        if (!Enum.IsDefined(icon))
            throw new ArgumentOutOfRangeException(nameof(icon));
        if (!Enum.IsDefined(tone))
            throw new ArgumentOutOfRangeException(nameof(tone));
        UmbraNativeUi.Icon(icon, tone, Math.Clamp(size, 8.0f, 96.0f));
    }

    public void Badge(string text, UmbraTextTone tone, UmbraIcon icon = UmbraIcon.None)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        UmbraNativeUi.Badge(text, tone, icon);
    }

    public void Artwork(string seed, UmbraIcon icon = UmbraIcon.Plug, float size = 72.0f)
    {
        EnsureWindow();
        UmbraNativeUi.Artwork(seed ?? "", icon, Math.Clamp(size, 32.0f, 160.0f));
    }

    public void SetNextWindowSize(float width, float height, bool firstUseOnly = true)
    {
        EnsureRenderThread();
        UmbraNativeUi.SetNextWindowSize(Math.Max(0.0f, width), Math.Max(0.0f, height), firstUseOnly);
    }

    public bool BeginChild(string id, float height, bool border = true)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        bool visible = UmbraNativeUi.BeginChild(id, Math.Max(0.0f, height), border);
        openChildDepth++;
        return visible;
    }

    public bool BeginPanel(
        string id,
        float width,
        float height,
        UmbraPanelStyle style = UmbraPanelStyle.Card)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Enum.IsDefined(style))
            throw new ArgumentOutOfRangeException(nameof(style));
        bool visible = UmbraNativeUi.BeginPanel(
            id,
            Math.Max(0.0f, width),
            Math.Max(0.0f, height),
            style);
        openChildDepth++;
        return visible;
    }

    public void EndChild()
    {
        EnsureRenderThread();
        if (openChildDepth <= 0)
            throw new InvalidOperationException("Umbra EndChild was called without a matching BeginChild.");
        UmbraNativeUi.EndChild();
        openChildDepth--;
    }

    public void RecoverAfterPluginCallback()
    {
        while (openChildDepth > 0)
        {
            try
            {
                UmbraNativeUi.EndChild();
            }
            finally
            {
                openChildDepth--;
            }
        }

        while (openWindowDepth > 0)
        {
            try
            {
                UmbraNativeUi.EndWindow();
            }
            finally
            {
                openWindowDepth--;
            }
        }
    }

    private void EnsureWindow()
    {
        EnsureRenderThread();
        if (openWindowDepth <= 0)
            throw new InvalidOperationException("Umbra UI operations require an open window.");
    }

    private void EnsureRenderThread()
    {
        if (!IsRenderThread)
            throw new InvalidOperationException("Umbra draw operations must run on the render thread.");
    }
}

public static class UmbraManagedRenderEntryPoint
{
    [UnmanagedCallersOnly(EntryPoint = "UmbraRenderBridge", CallConvs = [typeof(CallConvStdcall)])]
    public static int UmbraRenderBridge(nint eventPointer, int sizeBytes)
    {
        return Process(eventPointer, sizeBytes);
    }

    public static int UmbraRenderBridgeCoreClr(nint eventPointer, int sizeBytes)
    {
        return Process(eventPointer, sizeBytes);
    }

    private static int Process(nint eventPointer, int sizeBytes)
    {
        try
        {
            if (eventPointer == nint.Zero || sizeBytes < Marshal.SizeOf<UmbraNativeRenderEvent>())
                return -1;

            if (!UmbraRuntimeHost.TryGet(out UmbraRuntime? runtime) || runtime is null)
                return 1;

            UmbraNativeRenderEvent renderEvent = Marshal.PtrToStructure<UmbraNativeRenderEvent>(eventPointer);
            return runtime.RenderBridge.Process(renderEvent);
        }
        catch (Exception ex)
        {
            if (UmbraRuntimeHost.TryGet(out UmbraRuntime? runtime) && runtime is not null)
                runtime.Log.Error("umbra_render_bridge_failed=true", ex);
            return -100;
        }
    }
}
