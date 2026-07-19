namespace AetherXIV.Operator;

public sealed record AetherXivLiveLogEntry(
    AetherXivManagedService Service,
    string Text);

public sealed record AetherXivLiveLogBatch(
    IReadOnlyList<AetherXivLiveLogEntry> Entries,
    IReadOnlyDictionary<AetherXivManagedService, int> DroppedByService)
{
    public bool IsEmpty => Entries.Count == 0 && DroppedByService.Count == 0;
}

/// <summary>
/// Keeps the AetherXIV Core app isolated from unbounded service-output bursts. This buffer only
/// governs the live preview; service-owned disk logs and diagnostic traces are unaffected.
/// </summary>
public sealed class AetherXivLiveLogBuffer
{
    private readonly object gate = new();
    private readonly Queue<AetherXivLiveLogEntry> pending = new();
    private readonly Dictionary<AetherXivManagedService, int> droppedByService = new();
    private readonly int capacity;

    public AetherXivLiveLogBuffer(int capacity = 10_000)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        this.capacity = capacity;
    }

    public int PendingCount
    {
        get
        {
            lock (gate)
                return pending.Count;
        }
    }

    public void Enqueue(AetherXivManagedService service, string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        lock (gate)
        {
            if (pending.Count == capacity)
            {
                AetherXivLiveLogEntry dropped = pending.Dequeue();
                droppedByService[dropped.Service] = droppedByService.GetValueOrDefault(dropped.Service) + 1;
            }

            pending.Enqueue(new AetherXivLiveLogEntry(service, text));
        }
    }

    public AetherXivLiveLogBatch Drain(int maxEntries)
    {
        if (maxEntries <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxEntries));

        lock (gate)
        {
            int count = Math.Min(maxEntries, pending.Count);
            AetherXivLiveLogEntry[] entries = new AetherXivLiveLogEntry[count];
            for (int index = 0; index < count; index++)
                entries[index] = pending.Dequeue();

            Dictionary<AetherXivManagedService, int> dropped = new(droppedByService);
            droppedByService.Clear();
            return new AetherXivLiveLogBatch(entries, dropped);
        }
    }
}
