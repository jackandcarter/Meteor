using AetherXIV.Core.Map.actors.chara.player;
using AetherXIV.Core.Map.actors.area;

namespace AetherXIV.Core.Map.Tests;

public sealed class ZoneInventoryRefreshPolicyTests
{
    [Fact]
    public void LoginAndCrossZoneRefreshesResendItemDefinitions()
    {
        Assert.True(ZoneInventoryRefreshPolicy.ShouldResendItemDefinitions(
            ZoneInventoryRefreshMode.Full));
    }

    [Fact]
    public void SameZoneContentReloadRetainsKnownItemDefinitions()
    {
        Assert.False(ZoneInventoryRefreshPolicy.ShouldResendItemDefinitions(
            ZoneInventoryRefreshMode.RetainKnownItemDefinitions));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void GridaniaOpeningWolvesUseRetailTutorialExperience(uint battleNpcId)
    {
        Assert.True(GridaniaOpeningTutorialPolicy.IsTutorialWolf(
            GridaniaOpeningTutorialPolicy.ContentAreaName,
            battleNpcId));
        Assert.Equal((ushort)1000, GridaniaOpeningTutorialPolicy.WolfExperience);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void GridaniaOpeningPolicyRejectsNonWolfActors(uint battleNpcId)
    {
        Assert.False(GridaniaOpeningTutorialPolicy.IsTutorialWolf(
            GridaniaOpeningTutorialPolicy.ContentAreaName,
            battleNpcId));
    }

    [Fact]
    public void BattleCompletionSignalIsScopedToPlayerActor()
    {
        Assert.Equal("battleComplete:1157627909",
            GridaniaOpeningTutorialPolicy.BuildBattleCompleteSignal(0x45000005));
    }
}
