namespace AetherXIV.Core.Map.Tests;

public sealed class SeamlessBoundaryPolicyTests
{
    private const float DestinationX1 = 22.0f;
    private const float DestinationZ1 = -7.0f;
    private const float DestinationX2 = 23.0f;
    private const float DestinationZ2 = 22.0f;
    private const float MergeX1 = -7.0f;
    private const float MergeZ1 = -26.0f;
    private const float MergeX2 = -1.0f;
    private const float MergeZ2 = -4.0f;

    [Fact]
    public void CapturedUldahMovementCrossesNarrowDestinationBoundary()
    {
        Assert.True(SeamlessBoundaryPolicy.ReachedDestination(
            destinationIsMerged: true,
            previousX: 21.923f,
            previousZ: 4.938f,
            currentX: 23.069f,
            currentZ: 5.685f,
            DestinationX1,
            DestinationZ1,
            DestinationX2,
            DestinationZ2,
            MergeX1,
            MergeZ1,
            MergeX2,
            MergeZ2));
    }

    [Fact]
    public void CapturedUldahMergeRemainsActiveBetweenMergeAndDestinationBoxes()
    {
        Assert.True(SeamlessBoundaryPolicy.IsWithinTransitionCorridor(
            x: 2.637f,
            z: -9.044f,
            DestinationX1,
            DestinationZ1,
            DestinationX2,
            DestinationZ2,
            MergeX1,
            MergeZ1,
            MergeX2,
            MergeZ2));
    }

    [Fact]
    public void ParallelMovementOutsideTheGateDoesNotTriggerDestination()
    {
        Assert.False(SeamlessBoundaryPolicy.ReachedDestination(
            destinationIsMerged: true,
            previousX: 21.923f,
            previousZ: 24.0f,
            currentX: 23.069f,
            currentZ: 24.0f,
            DestinationX1,
            DestinationZ1,
            DestinationX2,
            DestinationZ2,
            MergeX1,
            MergeZ1,
            MergeX2,
            MergeZ2));
    }

    [Fact]
    public void UnmergedMovementCannotEnterDestinationWithoutCrossingMergeBox()
    {
        Assert.False(SeamlessBoundaryPolicy.ReachedDestination(
            destinationIsMerged: false,
            previousX: 21.923f,
            previousZ: 4.938f,
            currentX: 23.069f,
            currentZ: 5.685f,
            DestinationX1,
            DestinationZ1,
            DestinationX2,
            DestinationZ2,
            MergeX1,
            MergeZ1,
            MergeX2,
            MergeZ2));
    }

    [Fact]
    public void SingleMovementCrossingMergeAndDestinationCanEnter()
    {
        Assert.True(SeamlessBoundaryPolicy.ReachedDestination(
            destinationIsMerged: false,
            previousX: -8.0f,
            previousZ: -14.0f,
            currentX: 24.0f,
            currentZ: 6.0f,
            DestinationX1,
            DestinationZ1,
            DestinationX2,
            DestinationZ2,
            MergeX1,
            MergeZ1,
            MergeX2,
            MergeZ2));
    }
}
