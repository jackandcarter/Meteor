using AetherXIV.Core.Map.actors.chara.player;

namespace AetherXIV.Core.Map.Tests;

public sealed class ClassQuestProgressionPolicyTests
{
    [Theory]
    [InlineData(110300, "Wdk200", 20, 0)]
    [InlineData(110301, "Wdk300", 30, 110300)]
    [InlineData(110302, "Wdk306", 36, 110301)]
    public void CarpenterChainUsesRetailQuestIdsLevelsAndPrerequisites(
        uint questId,
        string questName,
        short requiredLevel,
        uint prerequisiteQuestId)
    {
        Assert.True(ClassQuestProgressionPolicy.TryGet(questId, out ClassQuestRequirement requirement));
        Assert.Equal(questName, requirement.QuestName);
        Assert.Equal((byte)29, requirement.ClassId);
        Assert.Equal(requiredLevel, requirement.RequiredLevel);
        Assert.Equal(prerequisiteQuestId, requirement.PrerequisiteQuestId);
    }

    [Fact]
    public void FirstCarpenterQuestRequiresActiveLevelTwentyCarpenter()
    {
        Assert.True(ClassQuestProgressionPolicy.TryGet(110300, out ClassQuestRequirement requirement));

        Assert.False(ClassQuestProgressionPolicy.MeetsRequirements(
            requirement,
            currentClassOrJob: 7,
            classId => classId == 29 ? (short)50 : (short)1,
            _ => false));
        Assert.False(ClassQuestProgressionPolicy.MeetsRequirements(
            requirement,
            currentClassOrJob: 29,
            _ => 19,
            _ => false));
        Assert.True(ClassQuestProgressionPolicy.MeetsRequirements(
            requirement,
            currentClassOrJob: 29,
            _ => 20,
            _ => false));
    }

    [Fact]
    public void LaterCarpenterQuestsRequireThePreviousQuest()
    {
        Assert.True(ClassQuestProgressionPolicy.TryGet(110301, out ClassQuestRequirement requirement));

        Assert.False(ClassQuestProgressionPolicy.MeetsRequirements(
            requirement,
            currentClassOrJob: 29,
            _ => 30,
            _ => false));
        Assert.True(ClassQuestProgressionPolicy.MeetsRequirements(
            requirement,
            currentClassOrJob: 29,
            _ => 30,
            questId => questId == 110300));
    }

    [Fact]
    public void UnknownAndArrEraQuestIdsAreNotTreatedAsLegacyCarpenterQuests()
    {
        Assert.False(ClassQuestProgressionPolicy.TryGet(110299, out _));
        Assert.False(ClassQuestProgressionPolicy.TryGet(110303, out _));
    }
}
