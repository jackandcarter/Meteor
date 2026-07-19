using AetherXIV.Core;
using AetherXIV.Data;

namespace AetherXIV.Data.Tests;

public sealed class CharacterEquipmentVisualComposerTests
{
    [Fact]
    public void PackVisualMatchesLegacyAppearanceGraphicPacking()
    {
        ItemVisualRecord visual = ItemVisual(7000001, weaponId: 0, equipmentId: 4, variantId: 3, colorId: 5);

        uint packed = CharacterEquipmentVisualComposer.PackVisual(visual);

        Assert.Equal((4u << 10) | ((3u & 0x1F) << 5) | 5u, packed);
    }

    [Fact]
    public void ComposeMapsEquippedItemGraphicsToPlayerAppearanceSlots()
    {
        CharacterId characterId = new(42);
        CharacterAppearanceRecord baseAppearance = Appearance(characterId);
        ItemVisualRecord mainHand = ItemVisual(4020001, weaponId: 58, equipmentId: 1, variantId: 0, colorId: 0, offHandWeaponId: 59, offHandEquipmentId: 1, offHandVariantId: 0);
        ItemVisualRecord undershirt = ItemVisual(7100001, weaponId: 0, equipmentId: 2, variantId: 1, colorId: 0);
        ItemVisualRecord body = ItemVisual(7200001, weaponId: 0, equipmentId: 3, variantId: 2, colorId: 1);
        ItemVisualRecord ears = ItemVisual(7300001, weaponId: 0, equipmentId: 4, variantId: 3, colorId: 0);
        ItemVisualRecord wrists = ItemVisual(7400001, weaponId: 0, equipmentId: 5, variantId: 4, colorId: 0);
        CharacterEquipmentSlotRecord[] equipment =
        [
            Equip(characterId, CharacterEquipmentVisualComposer.EquipSlotMainHand, mainHand.ItemId),
            Equip(characterId, CharacterEquipmentVisualComposer.EquipSlotUndershirt, undershirt.ItemId),
            Equip(characterId, CharacterEquipmentVisualComposer.EquipSlotBody, body.ItemId),
            Equip(characterId, CharacterEquipmentVisualComposer.EquipSlotEars, ears.ItemId),
            Equip(characterId, CharacterEquipmentVisualComposer.EquipSlotWrists, wrists.ItemId)
        ];

        CharacterEquipmentVisualComposition result = CharacterEquipmentVisualComposer.Compose(
            baseAppearance,
            equipment,
            new[] { mainHand, undershirt, body, ears, wrists }.ToDictionary(row => row.ItemId));

        Assert.Empty(result.MissingItemVisualIds);
        Assert.Equal(CharacterEquipmentVisualComposer.PackVisual(mainHand), result.Appearance.MainHand);
        Assert.Equal(CharacterEquipmentVisualComposer.PackOffHandVisual(mainHand), result.Appearance.OffHand);
        Assert.Equal(CharacterEquipmentVisualComposer.PackVisual(body), result.Appearance.Body);
        Assert.Equal(CharacterEquipmentVisualComposer.PackVisual(ears), result.Appearance.LeftEar);
        Assert.Equal(CharacterEquipmentVisualComposer.PackVisual(ears), result.Appearance.RightEar);
        Assert.Equal(CharacterEquipmentVisualComposer.PackVisual(wrists), result.Appearance.LeftWrist);
        Assert.Equal(CharacterEquipmentVisualComposer.PackVisual(wrists), result.Appearance.RightWrist);
    }

    [Fact]
    public void ComposeAppliesConfirmedCraftingSpecialGraphics()
    {
        CharacterId characterId = new(42);
        ItemVisualRecord goldsmithMainHand = ItemVisual(6040001, weaponId: 700, equipmentId: 1, variantId: 0, colorId: 0);

        CharacterEquipmentVisualComposition result = CharacterEquipmentVisualComposer.Compose(
            Appearance(characterId),
            [Equip(characterId, CharacterEquipmentVisualComposer.EquipSlotMainHand, goldsmithMainHand.ItemId)],
            new[] { goldsmithMainHand }.ToDictionary(row => row.ItemId));

        Assert.Equal(CharacterEquipmentVisualComposer.PackVisual(goldsmithMainHand), result.Appearance.MainHand);
        Assert.Equal(CharacterEquipmentVisualComposer.PackVisual(729, 1, 0, 0), result.Appearance.OffHand);
        Assert.Equal(CharacterEquipmentVisualComposer.PackVisual(898, 1, 0, 0), result.Appearance.SpMainHand);
    }

    [Fact]
    public void ComposeReportsMissingVisualRowsWithoutGuessing()
    {
        CharacterId characterId = new(42);
        CharacterAppearanceRecord baseAppearance = Appearance(characterId) with { MainHand = 123u };

        CharacterEquipmentVisualComposition result = CharacterEquipmentVisualComposer.Compose(
            baseAppearance,
            [Equip(characterId, CharacterEquipmentVisualComposer.EquipSlotMainHand, 4020001)],
            new Dictionary<uint, ItemVisualRecord>());

        Assert.Equal([4020001u], result.MissingItemVisualIds);
        Assert.Equal(123u, result.Appearance.MainHand);
    }

    private static CharacterEquipmentSlotRecord Equip(CharacterId characterId, ushort slotId, uint itemId)
    {
        return new CharacterEquipmentSlotRecord(characterId, slotId, itemId, DyeId: 0);
    }

    private static ItemVisualRecord ItemVisual(
        uint itemId,
        uint weaponId,
        uint equipmentId,
        uint variantId,
        uint colorId,
        uint offHandWeaponId = 0,
        uint offHandEquipmentId = 0,
        uint offHandVariantId = 0)
    {
        return new ItemVisualRecord(
            itemId,
            weaponId,
            equipmentId,
            variantId,
            colorId,
            offHandWeaponId,
            offHandEquipmentId,
            offHandVariantId,
            new ProvenanceRef(EvidenceStatus.RepoConfirmed, "test", $"item:{itemId}", String.Empty));
    }

    private static CharacterAppearanceRecord Appearance(CharacterId characterId)
    {
        return new CharacterAppearanceRecord(
            characterId,
            ModelId: 1,
            Tribe: 1,
            Size: 1,
            HairStyle: 0,
            HairHighlightColor: 0,
            HairVariation: 0,
            FaceType: 0,
            Characteristics: 0,
            CharacteristicsColor: 0,
            FaceEyebrows: 0,
            FaceIrisSize: 0,
            FaceEyeShape: 0,
            FaceNose: 0,
            FaceFeatures: 0,
            FaceMouth: 0,
            Ears: 0,
            HairColor: 0,
            SkinColor: 0,
            EyeColor: 0,
            Voice: 0,
            MainHand: 0,
            OffHand: 0,
            SpMainHand: 0,
            SpOffHand: 0,
            Throwing: 0,
            Pack: 0,
            Pouch: 0,
            Head: 0,
            Body: 0,
            Legs: 0,
            Hands: 0,
            Feet: 0,
            Waist: 0,
            Neck: 0,
            LeftEar: 0,
            RightEar: 0,
            LeftWrist: 0,
            RightWrist: 0,
            LeftIndex: 0,
            RightIndex: 0,
            LeftFinger: 0,
            RightFinger: 0);
    }
}
