namespace AetherXIV.Data;

public sealed record CharacterEquipmentVisualComposition(
    CharacterAppearanceRecord Appearance,
    IReadOnlyList<uint> MissingItemVisualIds);

public static class CharacterEquipmentVisualComposer
{
    public const ushort EquipSlotMainHand = 0;
    public const ushort EquipSlotOffHand = 1;
    public const ushort EquipSlotThrowingWeapon = 4;
    public const ushort EquipSlotPack = 5;
    public const ushort EquipSlotHead = 8;
    public const ushort EquipSlotUndershirt = 9;
    public const ushort EquipSlotBody = 10;
    public const ushort EquipSlotUndergarment = 11;
    public const ushort EquipSlotLegs = 12;
    public const ushort EquipSlotHands = 13;
    public const ushort EquipSlotFeet = 14;
    public const ushort EquipSlotWaist = 15;
    public const ushort EquipSlotNeck = 16;
    public const ushort EquipSlotEars = 17;
    public const ushort EquipSlotWrists = 19;
    public const ushort EquipSlotRightFinger = 21;
    public const ushort EquipSlotLeftFinger = 22;

    public static CharacterEquipmentVisualComposition Compose(
        CharacterAppearanceRecord baseAppearance,
        IEnumerable<CharacterEquipmentSlotRecord> equipment,
        IReadOnlyDictionary<uint, ItemVisualRecord> itemVisuals)
    {
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(itemVisuals);

        Dictionary<ushort, CharacterEquipmentSlotRecord> equipmentBySlot = equipment
            .Where(row => row.ItemId != 0)
            .OrderBy(row => row.SlotId)
            .ToDictionary(row => row.SlotId, row => row);
        HashSet<uint> missing = [];

        uint mainHand = baseAppearance.MainHand;
        uint offHand = baseAppearance.OffHand;
        uint spMainHand = baseAppearance.SpMainHand;
        uint spOffHand = baseAppearance.SpOffHand;
        uint throwing = baseAppearance.Throwing;
        uint pack = baseAppearance.Pack;
        uint head = baseAppearance.Head;
        uint body = baseAppearance.Body;
        uint legs = baseAppearance.Legs;
        uint hands = baseAppearance.Hands;
        uint feet = baseAppearance.Feet;
        uint waist = baseAppearance.Waist;
        uint neck = baseAppearance.Neck;
        uint leftEar = baseAppearance.LeftEar;
        uint rightEar = baseAppearance.RightEar;
        uint leftWrist = baseAppearance.LeftWrist;
        uint rightWrist = baseAppearance.RightWrist;
        uint leftFinger = baseAppearance.LeftFinger;
        uint rightFinger = baseAppearance.RightFinger;

        if (TryGetVisual(EquipSlotUndershirt, out ItemVisualRecord undershirt))
            body = PackVisual(undershirt);
        if (TryGetVisual(EquipSlotUndergarment, out ItemVisualRecord undergarment))
            legs = PackVisual(undergarment);

        if (TryGetVisual(EquipSlotMainHand, out ItemVisualRecord mainHandVisual))
        {
            mainHand = PackVisual(mainHandVisual);
            uint pairedOffHand = PackOffHandVisual(mainHandVisual);
            if (pairedOffHand != 0)
                offHand = pairedOffHand;

            ApplyMainHandSpecialGraphics(
                mainHandVisual.ItemId,
                ref offHand,
                ref spMainHand,
                ref spOffHand);
        }

        if (TryGetVisual(EquipSlotOffHand, out ItemVisualRecord offHandVisual))
        {
            uint graphic = PackVisual(offHandVisual);
            if (IsWeaverWeapon(offHandVisual.ItemId) || IsGoldsmithWeapon(offHandVisual.ItemId))
            {
                spOffHand = graphic;
            }
            else
            {
                offHand = graphic;
                if (IsAlchemistWeapon(offHandVisual.ItemId))
                    spOffHand = PackVisual(offHandVisual.WeaponId + 1, offHandVisual.EquipmentId, offHandVisual.VariantId, colorId: 0);
            }
        }

        if (TryGetVisual(EquipSlotThrowingWeapon, out ItemVisualRecord throwingVisual))
            throwing = PackVisual(throwingVisual);
        if (TryGetVisual(EquipSlotPack, out ItemVisualRecord packVisual))
            pack = PackVisual(packVisual);
        if (TryGetVisual(EquipSlotHead, out ItemVisualRecord headVisual))
            head = PackVisual(headVisual);
        if (TryGetVisual(EquipSlotBody, out ItemVisualRecord bodyVisual))
            body = PackVisual(bodyVisual);
        if (TryGetVisual(EquipSlotLegs, out ItemVisualRecord legsVisual))
            legs = PackVisual(legsVisual);
        if (TryGetVisual(EquipSlotHands, out ItemVisualRecord handsVisual))
            hands = PackVisual(handsVisual);
        if (TryGetVisual(EquipSlotFeet, out ItemVisualRecord feetVisual))
            feet = PackVisual(feetVisual);
        if (TryGetVisual(EquipSlotWaist, out ItemVisualRecord waistVisual))
            waist = PackVisual(waistVisual);
        if (TryGetVisual(EquipSlotNeck, out ItemVisualRecord neckVisual))
            neck = PackVisual(neckVisual);
        if (TryGetVisual(EquipSlotEars, out ItemVisualRecord earVisual))
            leftEar = rightEar = PackVisual(earVisual);
        if (TryGetVisual(EquipSlotWrists, out ItemVisualRecord wristVisual))
            leftWrist = rightWrist = PackVisual(wristVisual);
        if (TryGetVisual(EquipSlotRightFinger, out ItemVisualRecord rightFingerVisual))
            rightFinger = PackVisual(rightFingerVisual);
        if (TryGetVisual(EquipSlotLeftFinger, out ItemVisualRecord leftFingerVisual))
            leftFinger = PackVisual(leftFingerVisual);

        CharacterAppearanceRecord appearance = baseAppearance with
        {
            MainHand = mainHand,
            OffHand = offHand,
            SpMainHand = spMainHand,
            SpOffHand = spOffHand,
            Throwing = throwing,
            Pack = pack,
            Head = head,
            Body = body,
            Legs = legs,
            Hands = hands,
            Feet = feet,
            Waist = waist,
            Neck = neck,
            LeftEar = leftEar,
            RightEar = rightEar,
            LeftWrist = leftWrist,
            RightWrist = rightWrist,
            LeftFinger = leftFinger,
            RightFinger = rightFinger
        };
        return new CharacterEquipmentVisualComposition(appearance, missing.OrderBy(itemId => itemId).ToArray());

        bool TryGetVisual(ushort slotId, out ItemVisualRecord visual)
        {
            visual = default!;
            if (!equipmentBySlot.TryGetValue(slotId, out CharacterEquipmentSlotRecord? equipped))
                return false;

            if (itemVisuals.TryGetValue(equipped.ItemId, out ItemVisualRecord? found))
            {
                visual = found;
                return true;
            }

            missing.Add(equipped.ItemId);
            return false;
        }
    }

    public static uint PackVisual(ItemVisualRecord visual)
    {
        return PackVisual(visual.WeaponId, visual.EquipmentId, visual.VariantId, visual.ColorId);
    }

    public static uint PackOffHandVisual(ItemVisualRecord visual)
    {
        return PackVisual(visual.OffHandWeaponId, visual.OffHandEquipmentId, visual.OffHandVariantId, colorId: 0);
    }

    public static uint PackVisual(uint weaponId, uint equipmentId, uint variantId, uint colorId)
    {
        uint mixedVariantId = weaponId == 0
            ? ((variantId & 0x1F) << 5) | colorId
            : variantId;
        return ((weaponId & 0x3FF) << 20)
            | ((equipmentId & 0x3FF) << 10)
            | (mixedVariantId & 0x3FF);
    }

    private static void ApplyMainHandSpecialGraphics(
        uint itemId,
        ref uint offHand,
        ref uint spMainHand,
        ref uint spOffHand)
    {
        if (IsCarpenterWeapon(itemId))
        {
            spMainHand = PackVisual(898, 4, 0, 0);
            spOffHand = PackVisual(898, 4, 0, 0);
        }
        else if (IsBlacksmithWeapon(itemId))
        {
            spMainHand = PackVisual(899, 1, 0, 0);
            spOffHand = PackVisual(899, 1, 0, 0);
        }
        else if (IsArmorerWeapon(itemId))
        {
            spMainHand = PackVisual(899, 2, 0, 0);
            spOffHand = PackVisual(899, 2, 0, 0);
        }
        else if (IsGoldsmithWeapon(itemId))
        {
            offHand = PackVisual(729, 1, 0, 0);
            spMainHand = PackVisual(898, 1, 0, 0);
        }
        else if (IsTannerWeapon(itemId))
        {
            spMainHand = PackVisual(898, 3, 0, 0);
            spOffHand = PackVisual(898, 3, 0, 0);
        }
        else if (IsWeaverWeapon(itemId))
        {
            spMainHand = 0;
        }
        else if (IsAlchemistWeapon(itemId))
        {
            spMainHand = PackVisual(900, 1, 0, 0);
        }
        else if (IsCulinarianWeapon(itemId))
        {
            spMainHand = PackVisual(900, 2, 0, 0);
            spOffHand = PackVisual(898, 2, 0, 0);
        }
        else
        {
            spMainHand = 0;
            spOffHand = 0;
        }
    }

    private static bool IsCarpenterWeapon(uint itemId) => itemId is >= 6010000 and <= 6019999;

    private static bool IsBlacksmithWeapon(uint itemId) => itemId is >= 6020000 and <= 6029999;

    private static bool IsArmorerWeapon(uint itemId) => itemId is >= 6030000 and <= 6039999;

    private static bool IsGoldsmithWeapon(uint itemId) => itemId is >= 6040000 and <= 6049999;

    private static bool IsTannerWeapon(uint itemId) => itemId is >= 6050000 and <= 6059999;

    private static bool IsWeaverWeapon(uint itemId) => itemId is >= 6060000 and <= 6069999;

    private static bool IsAlchemistWeapon(uint itemId) => itemId is >= 6070000 and <= 6079999;

    private static bool IsCulinarianWeapon(uint itemId) => itemId is >= 6080000 and <= 6089999;
}
