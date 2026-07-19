using AetherXIV.Core.Map.actors.chara.ai;

namespace AetherXIV.Core.Map.Tests;

public sealed class BattleCastPresentationPolicyTests
{
    [Theory]
    [InlineData(3, 0x60)]
    public void PlayerChantMatchesOfficial123bCastTrace(byte castType, byte expectedChantId)
    {
        Assert.Equal(expectedChantId, BattleCastPresentationPolicy.GetChantId(true, castType));
    }

    [Theory]
    [InlineData(11, 0xF0)]
    [InlineData(12, 0xE0)]
    public void BattleNpcChantMatchesOfficial123bCastTraces(byte castType, byte expectedChantId)
    {
        Assert.Equal(expectedChantId, BattleCastPresentationPolicy.GetChantId(false, castType));
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(true, 8)]
    [InlineData(false, 3)]
    public void UnconfirmedCastTypesRetainLegacyFallback(bool isPlayer, byte castType)
    {
        Assert.Equal(0xF0, BattleCastPresentationPolicy.GetChantId(isPlayer, castType));
    }
}
