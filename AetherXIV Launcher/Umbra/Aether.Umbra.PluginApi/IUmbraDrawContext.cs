namespace Aether.Umbra.PluginApi;

public interface IUmbraDrawContext
{
    ulong FrameNumber { get; }

    TimeSpan DeltaTime { get; }

    int ViewportWidth { get; }

    int ViewportHeight { get; }

    float AvailableContentWidth { get; }

    float ContentRegionWidth { get; }

    int DeviceGeneration { get; }

    bool IsRenderThread { get; }

    bool IsPluginManagerOpen { get; }

    void RequestPluginManagerOpen();

    bool BeginWindow(string title, ref bool isOpen);

    void EndWindow();

    void Text(string text);

    void Text(string text, UmbraTextTone tone);

    void Text(string text, UmbraTextTone tone, UmbraTextStyle style);

    bool InputText(string label, ref string value, string hint = "", int maximumLength = 256);

    bool Button(string label);

    bool Button(
        string label,
        UmbraButtonStyle style,
        UmbraIcon icon = UmbraIcon.None,
        float width = 0.0f,
        float height = 0.0f);

    bool Checkbox(string label, ref bool value);

    bool Toggle(string label, ref bool value);

    void SameLine();

    void Separator();

    void Spacing(float height = 8.0f);

    void Icon(UmbraIcon icon, UmbraTextTone tone = UmbraTextTone.Normal, float size = 20.0f);

    void Badge(string text, UmbraTextTone tone, UmbraIcon icon = UmbraIcon.None);

    void Artwork(string seed, UmbraIcon icon = UmbraIcon.Plug, float size = 72.0f);

    void SetNextWindowSize(float width, float height, bool firstUseOnly = true);

    bool BeginChild(string id, float height, bool border = true);

    bool BeginPanel(
        string id,
        float width,
        float height,
        UmbraPanelStyle style = UmbraPanelStyle.Card);

    void EndChild();
}
