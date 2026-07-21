namespace AetherXIV.Core.Map.Tests;

public sealed class ZoneTransitionPositionPolicyTests
{
    private const uint UldahZone = 175;

    [Fact]
    public void HallPositionIsRejectedAfterUldahTransitionBegins()
    {
        Assert.False(ZoneTransitionPositionPolicy.IsDestinationConsistent(
            hasExpectedPosition: true,
            currentZoneId: UldahZone,
            expectedZoneId: UldahZone,
            expectedX: -210.0f,
            expectedY: 190.0f,
            expectedZ: 25.0f,
            receivedX: 160.105f,
            receivedY: 0.0f,
            receivedZ: -145.482f));
    }

    [Fact]
    public void CapturedUldahArrivalCompletesPendingTransition()
    {
        Assert.True(ZoneTransitionPositionPolicy.IsDestinationConsistent(
            hasExpectedPosition: true,
            currentZoneId: UldahZone,
            expectedZoneId: UldahZone,
            expectedX: -210.0f,
            expectedY: 190.0f,
            expectedZ: 25.0f,
            receivedX: -210.0f,
            receivedY: 190.0f,
            receivedZ: 25.0f));
    }

    [Fact]
    public void SmallClientArrivalAdjustmentIsAccepted()
    {
        Assert.True(ZoneTransitionPositionPolicy.IsDestinationConsistent(
            hasExpectedPosition: true,
            currentZoneId: UldahZone,
            expectedZoneId: UldahZone,
            expectedX: -210.0f,
            expectedY: 190.0f,
            expectedZ: 25.0f,
            receivedX: -214.0f,
            receivedY: 191.0f,
            receivedZ: 28.0f));
    }

    [Fact]
    public void CapturedGridaniaPrivateAreaAcknowledgementIsAccepted()
    {
        Assert.True(ZoneTransitionPositionPolicy.IsDestinationConsistent(
            hasExpectedPosition: true,
            currentZoneId: 166,
            expectedZoneId: 166,
            expectedX: 362.4087f,
            expectedY: 4.0f,
            expectedZ: -703.8168f,
            receivedX: 354.3533f,
            receivedY: 3.750001f,
            receivedZ: -700.6393f));
    }

    [Fact]
    public void WrongCurrentZoneCannotAcknowledgeTransition()
    {
        Assert.False(ZoneTransitionPositionPolicy.IsDestinationConsistent(
            hasExpectedPosition: true,
            currentZoneId: 170,
            expectedZoneId: UldahZone,
            expectedX: -210.0f,
            expectedY: 190.0f,
            expectedZ: 25.0f,
            receivedX: -210.0f,
            receivedY: 190.0f,
            receivedZ: 25.0f));
    }

    [Fact]
    public void OrdinaryMovementIsUnaffectedWithoutPendingExpectation()
    {
        Assert.True(ZoneTransitionPositionPolicy.IsDestinationConsistent(
            hasExpectedPosition: false,
            currentZoneId: 170,
            expectedZoneId: 0,
            expectedX: 0.0f,
            expectedY: 0.0f,
            expectedZ: 0.0f,
            receivedX: 23.069f,
            receivedY: 0.0f,
            receivedZ: 5.685f));
    }

    [Fact]
    public void SameZonePrivateAreaFlipUsesResidentGeometryReload()
    {
        Assert.Equal(
            ZoneTransitionReloadRecipe.ResidentGeometry,
            ZoneTransitionReloadPolicy.Select(155, 155));
    }

    [Fact]
    public void CrossZoneTutorialReturnUsesFullMapReload()
    {
        Assert.Equal(
            ZoneTransitionReloadRecipe.FullMap,
            ZoneTransitionReloadPolicy.Select(166, 155));
    }
}
