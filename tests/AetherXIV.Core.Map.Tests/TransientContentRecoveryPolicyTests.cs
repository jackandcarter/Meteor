using AetherXIV.Core.Map.actors.area;

namespace AetherXIV.Core.Map.Tests;

public sealed class TransientContentRecoveryPolicyTests
{
    [Theory]
    [InlineData("SimpleContent30010")]
    [InlineData("SimpleContentMan0g101")]
    public void DynamicContentNamesAreTransient(string privateAreaName)
    {
        Assert.True(TransientContentRecoveryPolicy.IsTransient(privateAreaName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PrivateAreaMasterPast")]
    public void PersistentPrivateAreasAreNotCleared(string? privateAreaName)
    {
        Assert.False(TransientContentRecoveryPolicy.IsTransient(privateAreaName!));
    }

    [Fact]
    public void Man0g1EscortRecoversAtWhiteWolfGate()
    {
        bool found = TransientContentRecoveryPolicy.TryGetRecoveryPoint(
            "SimpleContentMan0g101",
            out uint zoneId,
            out float x,
            out float y,
            out float z,
            out float rotation);

        Assert.True(found);
        Assert.Equal(155u, zoneId);
        Assert.Equal(-194.73f, x);
        Assert.Equal(3.54f, y);
        Assert.Equal(-1021.33f, z);
        Assert.Equal(-1.642f, rotation);
    }

    [Fact]
    public void GridaniaOpeningBattleRecoversBesideYda()
    {
        bool found = TransientContentRecoveryPolicy.TryGetRecoveryPoint(
            "SimpleContent30010",
            out uint zoneId,
            out float x,
            out float y,
            out float z,
            out float rotation);

        Assert.True(found);
        Assert.Equal(166u, zoneId);
        Assert.Equal(354.84f, x);
        Assert.Equal(3.77f, y);
        Assert.Equal(-699.71f, z);
        Assert.Equal(-0.791f, rotation);
    }

    [Theory]
    [InlineData("SimpleContent30002")]
    [InlineData("PrivateAreaMasterPast")]
    [InlineData("")]
    public void UnknownContentDoesNotInventARecoveryPoint(string privateAreaName)
    {
        Assert.False(TransientContentRecoveryPolicy.TryGetRecoveryPoint(
            privateAreaName,
            out _,
            out _,
            out _,
            out _,
            out _));
    }
}
