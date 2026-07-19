namespace AetherXIV.Core.Map.Tests;

public sealed class ZoneServicePolicyTests
{
    [Theory]
    [InlineData(1, 100)]
    [InlineData(10, 100)]
    [InlineData(11, 500)]
    [InlineData(20, 500)]
    [InlineData(21, 1000)]
    [InlineData(30, 1000)]
    [InlineData(31, 2100)]
    [InlineData(40, 2100)]
    [InlineData(41, 5000)]
    [InlineData(50, 5000)]
    public void RepairFeesMatchSeptember123bBrackets(int level, int expected) =>
        Assert.Equal(expected, RepairPolicy.FeeForLevel(level));

    [Theory]
    [InlineData(100, 99u)]
    [InlineData(10000, 9900u)]
    [InlineData(0, 0u)]
    public void NpcRepairTargetsExactlyNinetyNinePercent(int maximum, uint expected) =>
        Assert.Equal(expected, RepairPolicy.TargetDurability(maximum));

    [Theory]
    [InlineData(4040010u, true)] // weapon
    [InlineData(9040018u, true)] // accessory
    [InlineData(3020202u, false)] // potion
    public void NpcRepairAcceptsEquipmentAndAccessoriesOnly(uint catalogId, bool expected) =>
        Assert.Equal(expected, RepairPolicy.IsRepairableCatalogId(catalogId));

    [Fact]
    public void RentalPolicyUsesLevelTenEightHundredGilAndTenMinutes()
    {
        Assert.False(ChocoboPolicy.IsRentalLevelEligible(9));
        Assert.True(ChocoboPolicy.IsRentalLevelEligible(10));
        Assert.Equal(800, ChocoboPolicy.RentalPrice);
        Assert.Equal(10, ChocoboPolicy.RentalMinutes);
        Assert.Equal(0, ChocoboPolicy.RentalAppearance);
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(11, true)]
    [InlineData(12, true)]
    [InlineData(127, false)]
    public void PersonalIssuanceRequiresARealPrivateThirdClassRank(byte rank, bool expected) =>
        Assert.Equal(expected, ChocoboPolicy.IsPrivateThirdClassOrHigher(rank));

    [Theory]
    [InlineData(1500202u, 1, 1000201u, 2001004u)]
    [InlineData(1500203u, 2, 1000202u, 2001005u)]
    [InlineData(1500201u, 3, 1000203u, 2001006u)]
    public void CompanyShopsPinCompanySealAndIssuance(
        uint actorClassId,
        byte company,
        uint sealItemId,
        uint issuanceItemId)
    {
        Assert.True(GrandCompanyShopPolicy.TryGetShop(actorClassId, out GrandCompanyShopPolicyEntry policy));
        Assert.Equal(company, policy.grandCompany);
        Assert.Equal(sealItemId, policy.sealItemId);
        Assert.Equal(issuanceItemId, policy.chocoboIssuanceItemId);
        Assert.True(GrandCompanyShopPolicy.IsExactIssuanceSelection(policy, issuanceItemId, 3000));
        Assert.False(GrandCompanyShopPolicy.IsExactIssuanceSelection(policy, issuanceItemId, 2999));
        Assert.False(GrandCompanyShopPolicy.IsExactIssuanceSelection(policy, issuanceItemId + 1, 3000));
    }

    [Theory]
    [InlineData(1500006u, 1, 2001004u)]
    [InlineData(1500061u, 2, 2001005u)]
    [InlineData(1000840u, 3, 2001006u)]
    public void StablemastersRequireTheirMatchingCompanyIssuance(uint actorClassId, byte company, uint issuance)
    {
        Assert.True(ChocoboPolicy.TryGetStablemaster(actorClassId, out StablemasterPolicy policy));
        Assert.Equal(company, policy.grandCompany);
        Assert.Equal(issuance, policy.issuanceItemId);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Chocobo")]
    [InlineData("Wind-Rider")]
    public void ClientApprovedChocoboNamesAreAccepted(string name) =>
        Assert.True(ChocoboPolicy.IsClientApprovedName(name));

    [Theory]
    [InlineData("")]
    [InlineData("ElevenChars!")]
    [InlineData("Bad Name")]
    [InlineData("123")]
    public void InvalidChocoboNamesAreRejected(string name) =>
        Assert.False(ChocoboPolicy.IsClientApprovedName(name));

    [Fact]
    public void UndocumentedRideCombatModifiersRemainEvidenceGated()
    {
        Assert.False(ChocoboCombatParameters.EvidenceBacked);
        Assert.Equal(1.0f, ChocoboCombatParameters.DamageMultiplier);
        Assert.Equal(0.0f, ChocoboCombatParameters.RearHitSpeedLossChance);
        Assert.Equal(0, ChocoboCombatParameters.ForcedDismountDamageThreshold);
    }
}
