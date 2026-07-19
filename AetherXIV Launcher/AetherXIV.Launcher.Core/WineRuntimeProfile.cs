namespace AetherXIV.Launcher.Core;

public enum WineRuntimeKind
{
    NativeWindows,
    CrossOverBottle,
    WinePrefix,
    WhiskyBottle,
    CustomCommand
}

public enum RuntimeSelectionMode
{
    AutomaticManaged,
    DetectedRuntime,
    CustomRuntime
}

public sealed record WineRuntimeProfile(
    string Name,
    WineRuntimeKind Kind,
    string Command,
    string? BottleName,
    string? PrefixPath,
    Dictionary<string, string> Environment)
{
    public const string DefaultDirect3DConfig = "renderer=gl,csmt=0";
    public const string OpenGLThreadedDirect3DConfig = "renderer=gl,csmt=1";
    public const string VulkanDirect3DConfig = "renderer=vulkan,csmt=0";

    public static WineRuntimeProfile NativeWindows()
    {
        return new WineRuntimeProfile(
            "Windows native",
            WineRuntimeKind.NativeWindows,
            "",
            null,
            null,
            new Dictionary<string, string>());
    }

    public static WineRuntimeProfile CrossOverBottle(string name, string bottleName, string command = "wine")
    {
        return new WineRuntimeProfile(
            name,
            WineRuntimeKind.CrossOverBottle,
            command,
            bottleName,
            null,
            new Dictionary<string, string>
            {
                ["CX_BOTTLE"] = bottleName
            });
    }

    public static WineRuntimeProfile WinePrefix(
        string name,
        string prefixPath,
        string command = "wine",
        IReadOnlyDictionary<string, string>? environment = null)
    {
        Dictionary<string, string> variables = environment is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(environment);
        variables.TryAdd("WINE_D3D_CONFIG", DefaultDirect3DConfig);
        variables["WINEPREFIX"] = prefixPath;

        return new WineRuntimeProfile(name, WineRuntimeKind.WinePrefix, command, null, prefixPath, variables);
    }

    public static WineRuntimeProfile WhiskyBottle(string name, string bottleName, string command)
    {
        return new WineRuntimeProfile(
            name,
            WineRuntimeKind.WhiskyBottle,
            command,
            bottleName,
            null,
            new Dictionary<string, string>());
    }

    public static WineRuntimeProfile Custom(string name, string command)
    {
        return new WineRuntimeProfile(
            name,
            WineRuntimeKind.CustomCommand,
            command,
            null,
            null,
            new Dictionary<string, string>());
    }

    public WineRuntimeProfile WithGraphicsTarget(ClientGraphicsTarget graphicsTarget)
    {
        Dictionary<string, string> variables = new(Environment);
        switch (graphicsTarget)
        {
            case ClientGraphicsTarget.WineDefault:
                variables.Remove("WINE_D3D_CONFIG");
                break;
            case ClientGraphicsTarget.OpenGLThreaded:
                variables["WINE_D3D_CONFIG"] = OpenGLThreadedDirect3DConfig;
                break;
            case ClientGraphicsTarget.WineD3DVulkan:
                variables["WINE_D3D_CONFIG"] = VulkanDirect3DConfig;
                break;
            default:
                variables["WINE_D3D_CONFIG"] = DefaultDirect3DConfig;
                break;
        }

        return this with { Environment = variables };
    }

    public string BuildArguments(string windowsExecutablePath, string? applicationArguments = null)
    {
        if (Kind == WineRuntimeKind.NativeWindows)
            return applicationArguments ?? "";

        if (string.IsNullOrWhiteSpace(Command))
            throw new InvalidOperationException("Runtime command is required.");

        if (string.IsNullOrWhiteSpace(windowsExecutablePath))
            throw new InvalidOperationException("Windows executable path is required.");

        if (Kind == WineRuntimeKind.WhiskyBottle)
        {
            if (string.IsNullOrWhiteSpace(BottleName))
                throw new InvalidOperationException("Whisky bottle name is required.");

            string whiskyArguments = $"run {CommandLineArguments.Quote(BottleName)} {CommandLineArguments.Quote(windowsExecutablePath)}";
            return string.IsNullOrWhiteSpace(applicationArguments)
                ? whiskyArguments
                : $"{whiskyArguments} -- {applicationArguments}";
        }

        string arguments = CommandLineArguments.Quote(windowsExecutablePath);
        return string.IsNullOrWhiteSpace(applicationArguments)
            ? arguments
            : $"{arguments} {applicationArguments}";
    }
}
