using System.Text;
using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

internal interface IUmbraChatTransport
{
    UmbraChatAvailability Availability { get; }

    UmbraChatDeliveryResult Print(string message, string? tag, UmbraChatTone tone);

    UmbraChatDeliveryResult Submit(string message);
}

internal sealed class UmbraChatService(UmbraRuntimeLog log)
{
    internal const int MaximumMessageBytes = 0x1ff;

    private IUmbraChatTransport transport = new UnavailableChatTransport();

    internal IUmbraChat CreateScope(string pluginId, bool allowPrint, bool allowSubmit) =>
        new ScopedChat(this, pluginId, allowPrint, allowSubmit);

    internal void SetTransport(IUmbraChatTransport value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Volatile.Write(ref transport, value);
        UmbraChatAvailability availability = value.Availability;
        log.Info(
            $"umbra_chat_transport_changed adapter={availability.ClientAdapter} " +
            $"print={availability.CanPrint} submit={availability.CanSubmit}");
    }

    private UmbraChatAvailability GetAvailability(bool allowPrint, bool allowSubmit)
    {
        UmbraChatAvailability native = Volatile.Read(ref transport).Availability;
        return native with
        {
            CanPrint = allowPrint && native.CanPrint,
            CanSubmit = allowSubmit && native.CanSubmit
        };
    }

    private UmbraChatDeliveryResult Print(
        string pluginId,
        bool allowed,
        string message,
        string? tag,
        UmbraChatTone tone)
    {
        UmbraChatDeliveryResult? rejected = Validate(pluginId, allowed, "print", message);
        if (rejected is not null)
            return rejected;

        IUmbraChatTransport current = Volatile.Read(ref transport);
        if (!current.Availability.CanPrint)
        {
            log.Warning($"umbra_chat_print_unavailable plugin={pluginId} adapter={current.Availability.ClientAdapter}");
            return new UmbraChatDeliveryResult(
                UmbraChatDeliveryStatus.Unavailable,
                "The 1.23b native chat-print adapter has not been resolved for this client build.");
        }

        try
        {
            return current.Print(message, string.IsNullOrWhiteSpace(tag) ? null : tag.Trim(), tone);
        }
        catch (Exception ex)
        {
            log.Error($"umbra_chat_print_failed plugin={pluginId}", ex);
            return new UmbraChatDeliveryResult(UmbraChatDeliveryStatus.Failed, ex.Message);
        }
    }

    private UmbraChatDeliveryResult Submit(string pluginId, bool allowed, string message)
    {
        UmbraChatDeliveryResult? rejected = Validate(pluginId, allowed, "submit", message);
        if (rejected is not null)
            return rejected;

        IUmbraChatTransport current = Volatile.Read(ref transport);
        if (!current.Availability.CanSubmit)
        {
            log.Warning($"umbra_chat_submit_unavailable plugin={pluginId} adapter={current.Availability.ClientAdapter}");
            return new UmbraChatDeliveryResult(
                UmbraChatDeliveryStatus.Unavailable,
                "The 1.23b native chat-submit adapter has not been resolved for this client build.");
        }

        try
        {
            return current.Submit(message);
        }
        catch (Exception ex)
        {
            log.Error($"umbra_chat_submit_failed plugin={pluginId}", ex);
            return new UmbraChatDeliveryResult(UmbraChatDeliveryStatus.Failed, ex.Message);
        }
    }

    private UmbraChatDeliveryResult? Validate(string pluginId, bool allowed, string operation, string message)
    {
        if (!allowed)
        {
            log.Warning($"umbra_chat_{operation}_denied plugin={pluginId}");
            return new UmbraChatDeliveryResult(
                UmbraChatDeliveryStatus.Denied,
                $"Plugin manifest does not declare chat.{operation}.");
        }

        if (string.IsNullOrWhiteSpace(message))
            return new UmbraChatDeliveryResult(UmbraChatDeliveryStatus.Rejected, "Chat message must not be empty.");

        int bytes = Encoding.UTF8.GetByteCount(message);
        if (bytes > MaximumMessageBytes)
        {
            return new UmbraChatDeliveryResult(
                UmbraChatDeliveryStatus.Rejected,
                $"The 1.23b chat buffer accepts at most {MaximumMessageBytes} UTF-8 bytes; received {bytes}.");
        }

        return null;
    }

    private sealed class ScopedChat(
        UmbraChatService owner,
        string pluginId,
        bool allowPrint,
        bool allowSubmit) : IUmbraChat
    {
        public UmbraChatAvailability Availability => owner.GetAvailability(allowPrint, allowSubmit);

        public UmbraChatDeliveryResult Print(
            string message,
            string? tag = null,
            UmbraChatTone tone = UmbraChatTone.Normal) =>
            owner.Print(pluginId, allowPrint, message, tag, tone);

        public UmbraChatDeliveryResult Submit(string message) =>
            owner.Submit(pluginId, allowSubmit, message);
    }

    private sealed class UnavailableChatTransport : IUmbraChatTransport
    {
        public UmbraChatAvailability Availability { get; } = new(
            false,
            false,
            "ffxiv-1.23b-unresolved");

        public UmbraChatDeliveryResult Print(string message, string? tag, UmbraChatTone tone) =>
            new(UmbraChatDeliveryStatus.Unavailable);

        public UmbraChatDeliveryResult Submit(string message) =>
            new(UmbraChatDeliveryStatus.Unavailable);
    }
}
