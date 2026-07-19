namespace Aether.Umbra.PluginApi;

/// <summary>
/// Describes whether an Umbra service has a verified adapter for the active client.
/// </summary>
public sealed record UmbraServiceAvailability(
    bool IsAvailable,
    string Adapter,
    string? ClientBuildId = null,
    string? Reason = null);
