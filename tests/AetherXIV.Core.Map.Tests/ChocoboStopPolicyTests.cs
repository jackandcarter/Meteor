namespace AetherXIV.Core.Map.Tests;

public sealed class ChocoboStopPolicyTests
{
    [Theory]
    [InlineData(1090464u, "Anything", "pushDefault")]
    [InlineData(0u, "ChocoboStop", "pushDefault")]
    [InlineData(1090464u, "ChocoboStop", "_!pushRequest")]
    public void OnlyObservedBoundaryTriggersMayStartWhileMounted(
        uint actorClassId,
        string className,
        string eventName)
    {
        Assert.True(ChocoboStopPolicy.CanStartWhileMounted(actorClassId, className, eventName));
    }

    [Theory]
    [InlineData(1090464u, "ChocoboStop", "talkDefault")]
    [InlineData(1090464u, "ChocoboStop", "pushCommandIn")]
    [InlineData(1000840u, "PopulaceChocoboLender", "pushDefault")]
    public void UnrelatedMountedEventsRemainBlocked(
        uint actorClassId,
        string className,
        string eventName)
    {
        Assert.False(ChocoboStopPolicy.CanStartWhileMounted(actorClassId, className, eventName));
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, true, false)]
    [InlineData(1, false, true)]
    public void MountedRideIsRefusedOnlyAtNonRideDestination(
        ushort mountState,
        bool destinationCanRide,
        bool expected)
    {
        Assert.Equal(expected, ChocoboStopPolicy.ShouldRefuseTransition(mountState, destinationCanRide));
    }
}
