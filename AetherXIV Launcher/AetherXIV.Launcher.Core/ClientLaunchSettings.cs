namespace AetherXIV.Launcher.Core;

public enum ClientLaunchHelperMode
{
    Automatic,
    X86,
    X64,
    Arm64
}

public enum ClientGraphicsTarget
{
    OpenGLCompatibility,
    WineDefault,
    OpenGLThreaded,
    WineD3DVulkan
}
