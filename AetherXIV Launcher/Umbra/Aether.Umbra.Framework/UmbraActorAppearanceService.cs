using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

/// <summary>
/// Framework-owned appearance cache. Only a verified adapter may publish observations.
/// </summary>
internal sealed class UmbraActorAppearanceService : IUmbraActorAppearanceService
{
    private readonly object gate = new();
    private readonly Dictionary<uint, UmbraActorAppearanceSnapshot> snapshots = new();
    private long revision;

    public UmbraServiceAvailability Availability { get; private set; } = new(
        false,
        "ffxiv-1.23b-appearance-unresolved",
        null,
        "No verified legacy client appearance adapter is active.");

    public IReadOnlyCollection<UmbraActorAppearanceSnapshot> Snapshots
    {
        get
        {
            lock (gate)
                return snapshots.Values.OrderBy(snapshot => snapshot.ActorId).ToArray();
        }
    }

    public bool TryGetSnapshot(uint actorId, out UmbraActorAppearanceSnapshot? snapshot)
    {
        lock (gate)
            return snapshots.TryGetValue(actorId, out snapshot);
    }

    internal void ActivateVerifiedAdapter(string adapter, UmbraClientBuildProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapter);
        ArgumentNullException.ThrowIfNull(profile);

        if (!UmbraClientBuildCatalog.TryResolveSha256(profile.ExecutableSha256, out UmbraClientBuildProfile? supported)
            || supported != profile)
        {
            throw new InvalidOperationException("Appearance adapters may only activate for a cataloged exact client build.");
        }

        Availability = new UmbraServiceAvailability(true, adapter, profile.Id);
    }

    internal UmbraActorAppearanceSnapshot Observe(
        uint actorId,
        uint modelId,
        IEnumerable<uint> values,
        DateTimeOffset observedAt,
        UmbraAppearanceObservationSource source)
    {
        if (!Availability.IsAvailable)
            throw new InvalidOperationException("Cannot publish appearance observations without a verified client adapter.");

        UmbraActorAppearanceSnapshot current;
        lock (gate)
        {
            current = new UmbraActorAppearanceSnapshot(
                actorId,
                modelId,
                values,
                checked(++revision),
                observedAt,
                source);
            snapshots[actorId] = current;
        }

        return current;
    }

    internal bool Remove(uint actorId)
    {
        lock (gate)
            return snapshots.Remove(actorId);
    }

    internal void Deactivate(string reason)
    {
        lock (gate)
            snapshots.Clear();

        Availability = new UmbraServiceAvailability(
            false,
            "ffxiv-1.23b-appearance-unresolved",
            null,
            string.IsNullOrWhiteSpace(reason) ? "The client appearance adapter was deactivated." : reason);
    }
}
