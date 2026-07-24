using AetherXIV.Core;
using AetherXIV.Core.Common;
using AetherXIV.Data;

namespace AetherXIV.Data.Tests;

public sealed class PlayableCharacterIdentityTests
{
    public static TheoryData<byte, uint, PlayableCharacterRace, PlayableCharacterSex> RetailTribes => new()
    {
        { 1, 1, PlayableCharacterRace.Hyur, PlayableCharacterSex.Male },
        { 2, 2, PlayableCharacterRace.Hyur, PlayableCharacterSex.Female },
        { 3, 9, PlayableCharacterRace.Hyur, PlayableCharacterSex.Male },
        { 4, 3, PlayableCharacterRace.Elezen, PlayableCharacterSex.Male },
        { 5, 4, PlayableCharacterRace.Elezen, PlayableCharacterSex.Female },
        { 6, 3, PlayableCharacterRace.Elezen, PlayableCharacterSex.Male },
        { 7, 4, PlayableCharacterRace.Elezen, PlayableCharacterSex.Female },
        { 8, 5, PlayableCharacterRace.Lalafell, PlayableCharacterSex.Male },
        { 9, 6, PlayableCharacterRace.Lalafell, PlayableCharacterSex.Female },
        { 10, 5, PlayableCharacterRace.Lalafell, PlayableCharacterSex.Male },
        { 11, 6, PlayableCharacterRace.Lalafell, PlayableCharacterSex.Female },
        { 12, 8, PlayableCharacterRace.Miqote, PlayableCharacterSex.Female },
        { 13, 8, PlayableCharacterRace.Miqote, PlayableCharacterSex.Female },
        { 14, 7, PlayableCharacterRace.Roegadyn, PlayableCharacterSex.Male },
        { 15, 7, PlayableCharacterRace.Roegadyn, PlayableCharacterSex.Male }
    };

    [Theory]
    [MemberData(nameof(RetailTribes))]
    public void RetailTribeCarriesCanonicalRaceSexAndModel(
        byte tribe,
        uint modelId,
        PlayableCharacterRace race,
        PlayableCharacterSex sex)
    {
        Assert.True(PlayableCharacterIdentity.IsValidTribe(tribe));
        Assert.True(PlayableCharacterIdentity.TryGetModelId(tribe, out uint actualModelId));
        Assert.True(PlayableCharacterIdentity.TryGetRace(tribe, out PlayableCharacterRace actualRace));
        Assert.True(PlayableCharacterIdentity.TryGetSex(tribe, out PlayableCharacterSex actualSex));
        Assert.Equal(modelId, actualModelId);
        Assert.Equal(race, actualRace);
        Assert.Equal(sex, actualSex);
        Assert.Equal(sex == PlayableCharacterSex.Female, PlayableCharacterIdentity.IsFemale(tribe));
        Assert.Equal(sex == PlayableCharacterSex.Male, PlayableCharacterIdentity.IsMale(tribe));
        Assert.True(PlayableCharacterIdentity.IsModelConsistent(tribe, modelId));
        Assert.True(PlayableCharacterIdentity.IsModelConsistent(
            tribe,
            PlayableCharacterIdentity.UseTribeDefaultModel));
        Assert.Equal(modelId, CharacterModelIds.FromTribe(tribe));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    [InlineData(255)]
    public void InvalidTribeCannotSilentlyBecomeAPlayerIdentity(byte tribe)
    {
        Assert.False(PlayableCharacterIdentity.IsValidTribe(tribe));
        Assert.False(PlayableCharacterIdentity.TryGetModelId(tribe, out _));
        Assert.False(PlayableCharacterIdentity.TryGetRace(tribe, out _));
        Assert.False(PlayableCharacterIdentity.TryGetSex(tribe, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => CharacterModelIds.FromTribe(tribe));

        byte[] payload = new byte[0x49];
        payload[0x08] = tribe;
        Assert.False(CharacterCreationPayloadParser.TryParse(payload, out _));
        Assert.False(CharacterAppearancePayloadParser.TryParseCreationPayload(
            new CharacterId(1),
            payload,
            out CharacterAppearanceRecord? appearance));
        Assert.Null(appearance);
    }

    [Fact]
    public void ExplicitModelFromAnotherSexIsDetectedAsDrift()
    {
        Assert.False(PlayableCharacterIdentity.IsModelConsistent(2, 1));
        Assert.False(PlayableCharacterIdentity.IsModelConsistent(5, 3));
        Assert.False(PlayableCharacterIdentity.IsModelConsistent(9, 5));
    }
}
