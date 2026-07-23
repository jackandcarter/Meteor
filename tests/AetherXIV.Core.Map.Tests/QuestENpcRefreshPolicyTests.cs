using AetherXIV.Core.Map.Actors;

namespace AetherXIV.Core.Map.Tests;

public sealed class QuestENpcRefreshPolicyTests
{
    private static QuestENpc PushTrigger() =>
        new QuestENpc(1090201, 2, false, true, false, false);

    [Fact]
    public void UnchangedPresentationIsDeduplicatedWithinTheSameArea()
    {
        Assert.False(QuestENpc.ShouldBroadcast(PushTrigger(), PushTrigger(), false));
    }

    [Fact]
    public void UnchangedPresentationIsRebroadcastAfterAnAreaChange()
    {
        Assert.True(QuestENpc.ShouldBroadcast(PushTrigger(), PushTrigger(), true));
    }

    [Fact]
    public void ChangedPresentationBroadcastsWithoutAnAreaChange()
    {
        QuestENpc disabled = new QuestENpc(1090201, 0, false, false, false, false);

        Assert.True(QuestENpc.ShouldBroadcast(disabled, PushTrigger(), false));
    }
}
