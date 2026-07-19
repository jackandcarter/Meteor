namespace Aether.Umbra.PluginApi;

public enum UmbraChatTone
{
    Normal,
    System,
    Error
}

public enum UmbraChatDeliveryStatus
{
    Delivered,
    Unavailable,
    Denied,
    Rejected,
    Failed
}

public sealed record UmbraChatAvailability(
    bool CanPrint,
    bool CanSubmit,
    string ClientAdapter);

public sealed record UmbraChatDeliveryResult(
    UmbraChatDeliveryStatus Status,
    string? Error = null)
{
    public bool Succeeded => Status == UmbraChatDeliveryStatus.Delivered;
}

public interface IUmbraChat
{
    UmbraChatAvailability Availability { get; }

    UmbraChatDeliveryResult Print(
        string message,
        string? tag = null,
        UmbraChatTone tone = UmbraChatTone.Normal);

    UmbraChatDeliveryResult Submit(string message);
}
