using AetherXIV.Core.Map.actors.chara.player;
using Xunit;

namespace AetherXIV.Core.Map.Tests;

public sealed class JobProgressionPolicyTests
{
    [Theory]
    [InlineData(2, 15, 8, 2000202)]
    [InlineData(3, 16, 23, 2000201)]
    [InlineData(4, 17, 3, 2000203)]
    [InlineData(7, 18, 23, 2000205)]
    [InlineData(8, 19, 2, 2000204)]
    [InlineData(22, 26, 2, 2000207)]
    [InlineData(23, 27, 3, 2000206)]
    public void RetailJobsMapToTheirBaseClassSecondaryClassAndSoul(
        byte classId,
        byte expectedJobId,
        byte expectedSecondaryClassId,
        uint expectedSoulCrystalItemId)
    {
        Assert.True(JobProgressionPolicy.TryGetForBaseClass(classId, out JobProgressionRequirement requirement));
        Assert.Equal(expectedJobId, requirement.JobId);
        Assert.Equal(classId, requirement.BaseClassId);
        Assert.Equal(expectedSecondaryClassId, requirement.SecondaryClassId);
        Assert.Equal(expectedSoulCrystalItemId, requirement.SoulCrystalItemId);
    }

    [Fact]
    public void JobsRequireLevelThirtyBaseAndLevelFifteenSecondary()
    {
        Assert.True(JobProgressionPolicy.TryGetForBaseClass(7, out JobProgressionRequirement bard));
        Assert.False(JobProgressionPolicy.MeetsLevelRequirements(
            bard,
            classId => classId == 7 ? (short)29 : (short)15));
        Assert.False(JobProgressionPolicy.MeetsLevelRequirements(
            bard,
            classId => classId == 7 ? (short)30 : (short)14));
        Assert.True(JobProgressionPolicy.MeetsLevelRequirements(
            bard,
            classId => classId == 7 ? (short)30 : (short)15));
    }

    [Theory]
    [InlineData(29)]
    [InlineData(39)]
    [InlineData(41)]
    public void CraftingAndGatheringClassesDoNotHaveJobs(byte classId)
    {
        Assert.False(JobProgressionPolicy.TryGetForBaseClass(classId, out _));
    }
}
