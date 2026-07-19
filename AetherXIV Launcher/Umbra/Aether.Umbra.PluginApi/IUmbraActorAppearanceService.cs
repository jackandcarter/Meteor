namespace Aether.Umbra.PluginApi;

/// <summary>
/// Indices carried by the legacy 1.23b actor-appearance table.
/// </summary>
public enum UmbraAppearanceSlot
{
    Size = 0,
    ColorInfo = 1,
    FaceInfo = 2,
    HairHighlight = 3,
    Voice = 4,
    MainHand = 5,
    OffHand = 6,
    SpecialMainHand = 7,
    SpecialOffHand = 8,
    Throwing = 9,
    Pack = 10,
    Pouch = 11,
    Head = 12,
    Body = 13,
    Legs = 14,
    Hands = 15,
    Feet = 16,
    Waist = 17,
    Neck = 18,
    LeftEar = 19,
    RightEar = 20,
    RightWrist = 21,
    LeftWrist = 22,
    RightRing = 23,
    LeftRing = 24,
    RightIndex = 25,
    LeftIndex = 26,
    Unknown27 = 27
}

public static class UmbraAppearanceSlots
{
    public const int Count = 28;

    public static bool IsDefined(UmbraAppearanceSlot slot) =>
        (int)slot is >= 0 and < Count;

    public static bool ContainsGraphicId(UmbraAppearanceSlot slot) =>
        (int)slot is >= (int)UmbraAppearanceSlot.MainHand and <= (int)UmbraAppearanceSlot.LeftIndex;
}

/// <summary>
/// Decodes the packed 30-bit graphic value used by legacy equipment appearance slots.
/// </summary>
public readonly record struct UmbraGraphicId(uint RawValue)
{
    private const uint ComponentMask = 0x3FF;

    public ushort WeaponId => (ushort)((RawValue >> 20) & ComponentMask);

    public ushort EquipmentId => (ushort)((RawValue >> 10) & ComponentMask);

    public ushort PackedVariant => (ushort)(RawValue & ComponentMask);

    public bool IsWeapon => WeaponId != 0;

    /// <summary>
    /// Returns the ten-bit variant for weapons or the five-bit variant for equipment.
    /// </summary>
    public ushort VariantId => IsWeapon
        ? PackedVariant
        : (ushort)((PackedVariant >> 5) & 0x1F);

    /// <summary>
    /// Returns the five-bit equipment color. Weapon values do not encode a color here.
    /// </summary>
    public byte ColorId => IsWeapon ? (byte)0 : (byte)(PackedVariant & 0x1F);
}

public enum UmbraAppearanceObservationSource
{
    Unknown,
    NetworkPacket,
    ClientMemory
}

/// <summary>
/// Immutable appearance state observed for one actor. This type does not grant mutation access.
/// </summary>
public sealed class UmbraActorAppearanceSnapshot
{
    private readonly uint[] values;
    private readonly IReadOnlyList<uint> readOnlyValues;

    public UmbraActorAppearanceSnapshot(
        uint actorId,
        uint modelId,
        IEnumerable<uint> values,
        long revision,
        DateTimeOffset observedAt,
        UmbraAppearanceObservationSource source)
    {
        ArgumentNullException.ThrowIfNull(values);
        this.values = values.ToArray();
        if (this.values.Length != UmbraAppearanceSlots.Count)
        {
            throw new ArgumentException(
                $"Legacy actor appearance snapshots require exactly {UmbraAppearanceSlots.Count} values.",
                nameof(values));
        }

        if (revision < 1)
            throw new ArgumentOutOfRangeException(nameof(revision), "Appearance revisions begin at one.");

        readOnlyValues = Array.AsReadOnly(this.values);

        ActorId = actorId;
        ModelId = modelId;
        Revision = revision;
        ObservedAt = observedAt;
        Source = source;
    }

    public uint ActorId { get; }

    public uint ModelId { get; }

    public long Revision { get; }

    public DateTimeOffset ObservedAt { get; }

    public UmbraAppearanceObservationSource Source { get; }

    public IReadOnlyList<uint> Values => readOnlyValues;

    public uint GetValue(UmbraAppearanceSlot slot)
    {
        if (!UmbraAppearanceSlots.IsDefined(slot))
            throw new ArgumentOutOfRangeException(nameof(slot));

        return values[(int)slot];
    }

    public bool TryGetGraphicId(UmbraAppearanceSlot slot, out UmbraGraphicId graphicId)
    {
        if (!UmbraAppearanceSlots.ContainsGraphicId(slot))
        {
            graphicId = default;
            return false;
        }

        graphicId = new UmbraGraphicId(values[(int)slot]);
        return true;
    }
}

/// <summary>
/// Provides read-only appearance observations from a framework-owned, verified client adapter.
/// </summary>
public interface IUmbraActorAppearanceService
{
    UmbraServiceAvailability Availability { get; }

    IReadOnlyCollection<UmbraActorAppearanceSnapshot> Snapshots { get; }

    bool TryGetSnapshot(uint actorId, out UmbraActorAppearanceSnapshot? snapshot);
}
