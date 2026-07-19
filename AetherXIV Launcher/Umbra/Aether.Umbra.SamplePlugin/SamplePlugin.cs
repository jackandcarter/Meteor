using Aether.Umbra.PluginApi;

namespace Aether.Umbra.SamplePlugin;

public sealed class SamplePlugin : IUmbraPlugin
{
    private IUmbraPluginContext? context;
    private TimeSpan elapsed;
    private bool windowOpen = true;
    private bool showHeartbeat = true;
    private IUmbraChat? chat;
    private IDisposable? commandRegistration;

    public string Name => "Umbra SDK Sample";

    public void Initialize(IUmbraPluginContext context)
    {
        this.context = context;
        Directory.CreateDirectory(context.ConfigDirectory);
        chat = context.GetService<IUmbraChat>();
        IUmbraCommandManager? commands = context.GetService<IUmbraCommandManager>();
        commandRegistration = commands?.Register(
            new UmbraCommandRegistration(
                "/umbra-sample",
                "Prints an Umbra SDK sample message. Optional text is echoed."),
            invocation =>
            {
                string message = string.IsNullOrWhiteSpace(invocation.Arguments)
                    ? "Umbra API 2.0 command dispatch is active."
                    : invocation.Arguments;
                UmbraChatDeliveryResult? delivery = chat?.Print(message, "Umbra Sample");
                if (delivery is not { Succeeded: true })
                {
                    context.Logger.Warning(
                        $"chat print unavailable status={delivery?.Status.ToString() ?? "service-missing"}");
                }
            });
        context.Logger.Info(
            $"initialized api={context.ApiVersion} framework={context.FrameworkVersion} config={context.ConfigDirectory}");
    }

    public void Update(TimeSpan delta)
    {
        elapsed += delta;
        if (elapsed < TimeSpan.FromSeconds(10))
            return;

        elapsed = TimeSpan.Zero;
        if (showHeartbeat)
            context?.Logger.Info("heartbeat");
    }

    public void Draw(IUmbraDrawContext drawContext)
    {
        if (!windowOpen)
            return;

        drawContext.SetNextWindowSize(520.0f, 360.0f);
        bool visible = drawContext.BeginWindow("Umbra SDK Sample###UmbraSdkSample", ref windowOpen);
        try
        {
            if (!visible)
                return;

            drawContext.Icon(UmbraIcon.Umbra, UmbraTextTone.Accent, 34.0f);
            drawContext.SameLine();
            drawContext.Text("Umbra SDK Sample", UmbraTextTone.Normal, UmbraTextStyle.Title);
            drawContext.Badge($"API {context?.ApiVersion ?? "2.0"}", UmbraTextTone.Accent, UmbraIcon.Shield);
            drawContext.Separator();

            bool panelVisible = drawContext.BeginPanel("##SampleStatus", 0.0f, 170.0f, UmbraPanelStyle.Card);
            try
            {
                if (panelVisible)
                {
                    drawContext.Artwork(context?.PluginId ?? "sample", UmbraIcon.Plug, 72.0f);
                    drawContext.SameLine();
                    drawContext.Text(
                        $"Frame {drawContext.FrameNumber}\n{drawContext.ViewportWidth}x{drawContext.ViewportHeight}",
                        UmbraTextTone.Muted,
                        UmbraTextStyle.Body);
                    drawContext.Text(
                        chat?.Availability.CanPrint == true
                            ? "1.23b chat adapter ready"
                            : "1.23b chat adapter awaiting verified binding",
                        chat?.Availability.CanPrint == true ? UmbraTextTone.Success : UmbraTextTone.Warning,
                        UmbraTextStyle.Caption);
                    drawContext.Toggle("Heartbeat logging", ref showHeartbeat);
                    if (drawContext.Button(
                        "Open Plugin Manager",
                        UmbraButtonStyle.Primary,
                        UmbraIcon.Discover))
                    {
                        drawContext.RequestPluginManagerOpen();
                    }
                }
            }
            finally
            {
                drawContext.EndChild();
            }
        }
        finally
        {
            drawContext.EndWindow();
        }
    }

    public void Dispose()
    {
        commandRegistration?.Dispose();
        commandRegistration = null;
        chat = null;
        context?.Logger.Info("disposed");
        context = null;
    }
}
