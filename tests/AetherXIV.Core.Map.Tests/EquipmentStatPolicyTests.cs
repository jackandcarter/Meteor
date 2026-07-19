using AetherXIV.Core.Map.actors.chara.player;

namespace AetherXIV.Core.Map.Tests;

public sealed class EquipmentStatPolicyTests
{
    [Theory]
    [InlineData(26, 37, 0.10)]
    [InlineData(26, 30, 0.60)]
    [InlineData(10, 11, 0.90)]
    [InlineData(1, 3, 0.85)]
    [InlineData(26, 22, 1.00)]
    public void LevelAdjustmentMatchesInstalled123bClient(int playerLevel, int itemLevel, double expected)
    {
        Assert.Equal(expected, EquipmentStatPolicy.CalculateLevelAdjust(playerLevel, itemLevel), 6);
    }

    [Fact]
    public void AboveLevelHelmDefenseMatchesOfficialTraceDelta()
    {
        // Steel Barbut: level 37, defense 43, worn by the level 26 trace player.
        Assert.Equal(5, EquipmentStatPolicy.CalculateArmorDefense(43, 26, 37, false));

        // Steel Sallet (Green): level 30, defense 32, worn by the same player.
        Assert.Equal(20, EquipmentStatPolicy.CalculateArmorDefense(32, 26, 30, false));
    }

    [Fact]
    public void AtLevelBodyArmorUsesFullDefenseFromOfficialSwap()
    {
        Assert.Equal(45, EquipmentStatPolicy.CalculateArmorDefense(45, 26, 22, false));
        Assert.Equal(31, EquipmentStatPolicy.CalculateArmorDefense(31, 26, 21, false));
        Assert.Equal(-14, 31 - 45);
    }

    [Fact]
    public void HighQualityUsesClientMinimumIncrementAndMultiplier()
    {
        Assert.Equal(46, EquipmentStatPolicy.ApplyHighQualityValue(43, 1.05, true));
        Assert.Equal(2, EquipmentStatPolicy.ApplyHighQualityValue(1, 1.03, true));
        Assert.Equal(9, EquipmentStatPolicy.CalculateToolValue(9, false));
        Assert.Equal(10, EquipmentStatPolicy.CalculateToolValue(9, true));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(10, true)]
    public void OnlyClientOrdinaryBonusPairsAreUnconditional(int pairNumber, bool expected)
    {
        Assert.Equal(expected, EquipmentStatPolicy.IsOrdinaryBonusPair(pairNumber));
        Assert.Equal(pairNumber == 4, EquipmentStatPolicy.IsHighQualityBonusPair(pairNumber));
        Assert.Equal(expected, EquipmentStatPolicy.ShouldApplyBonusPair(pairNumber, false));
        Assert.Equal(expected || pairNumber == 4, EquipmentStatPolicy.ShouldApplyBonusPair(pairNumber, true));
    }

    [Fact]
    public void ParamNameIdsMapToExistingModifierIndices()
    {
        Assert.True(EquipmentStatPolicy.TryGetModifierId(15018, out uint attack));
        Assert.Equal(17u, attack);
        Assert.True(EquipmentStatPolicy.TryGetModifierId(15029, out uint magicEvasion));
        Assert.Equal(28u, magicEvasion);
        Assert.False(EquipmentStatPolicy.TryGetModifierId(-1, out _));
    }
}
