namespace Aether.Umbra.PluginApi;

public delegate void UmbraCommandHandler(UmbraCommandInvocation invocation);

public sealed record UmbraCommandRegistration(
    string Command,
    string HelpMessage = "",
    bool ShowInHelp = true);

public sealed record UmbraCommandInfo(
    string Command,
    string HelpMessage,
    bool ShowInHelp,
    string PluginId);

public sealed record UmbraCommandInvocation(
    string Command,
    string Arguments,
    string RawInput);

public enum UmbraCommandDispatchStatus
{
    Dispatched,
    NotFound,
    Invalid,
    Failed
}

public sealed record UmbraCommandDispatchResult(
    UmbraCommandDispatchStatus Status,
    string Command,
    string? Error = null)
{
    public bool Succeeded => Status == UmbraCommandDispatchStatus.Dispatched;
}

public interface IUmbraCommandManager
{
    IReadOnlyList<UmbraCommandInfo> Commands { get; }

    IDisposable Register(UmbraCommandRegistration registration, UmbraCommandHandler handler);

    bool Unregister(string command);

    UmbraCommandDispatchResult Dispatch(string content);
}
