using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.packets.send.actor;

namespace AetherXIV.Core.Map.Tests;

public sealed class ActorMovementInitializationTests
{
    [Fact]
    public void ShortConstructorPublishesUsableLegacyMovementSpeeds()
    {
        Actor actor = new(0x44D00001);

        Assert.Equal(SetActorSpeedPacket.DEFAULT_STOP, actor.moveSpeeds[0]);
        Assert.Equal(SetActorSpeedPacket.DEFAULT_WALK, actor.moveSpeeds[1]);
        Assert.Equal(SetActorSpeedPacket.DEFAULT_RUN, actor.moveSpeeds[2]);
        Assert.Equal(SetActorSpeedPacket.DEFAULT_ACTIVE, actor.moveSpeeds[3]);
    }
}
