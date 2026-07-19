using System.Buffers.Binary;
using AetherXIV.Core;
using AetherXIV.Data;

namespace AetherXIV.Data.Tests;

public sealed class CharacterAppearancePayloadParserTests
{
    [Fact]
    public void CreationPayloadParsesLegacyAppearanceOffsets()
    {
        byte[] payload = new byte[0x49];
        payload[0x08] = 3;
        payload[0x09] = 2;
        WriteUInt16(payload, 0x0A, 0x123);
        payload[0x0C] = 4;
        payload[0x0D] = 5;
        payload[0x0E] = 6;
        payload[0x0F] = 7;
        payload[0x10] = 1;
        payload[0x15] = 2;
        payload[0x16] = 1;
        payload[0x17] = 3;
        payload[0x18] = 4;
        payload[0x19] = 2;
        payload[0x1A] = 1;
        payload[0x1B] = 2;
        WriteUInt16(payload, 0x1C, 42);
        WriteUInt16(payload, 0x22, 24);
        WriteUInt16(payload, 0x24, 12);
        payload[0x26] = 8;

        bool parsed = CharacterAppearancePayloadParser.TryParseCreationPayload(
            new CharacterId(99),
            payload,
            out CharacterAppearanceRecord? appearance);

        Assert.True(parsed);
        Assert.NotNull(appearance);
        Assert.Equal(new CharacterId(99), appearance.CharacterId);
        Assert.Equal(9u, appearance.ModelId);
        Assert.Equal(3, appearance.Tribe);
        Assert.Equal(2u, appearance.Size);
        Assert.Equal(0x123u, appearance.HairStyle);
        Assert.Equal(4u, appearance.HairHighlightColor);
        Assert.Equal(5u, appearance.HairVariation);
        Assert.Equal(6, appearance.FaceType);
        Assert.Equal(7, appearance.Characteristics);
        Assert.Equal(1, appearance.CharacteristicsColor);
        Assert.Equal(42u, appearance.HairColor);
        Assert.Equal(24u, appearance.SkinColor);
        Assert.Equal(12u, appearance.EyeColor);
        Assert.Equal(8u, appearance.Voice);

        uint[] appearanceIds = ActorAppearanceConversion.ToLegacyAppearanceIds(appearance);
        Assert.Equal(2u, appearanceIds[ActorAppearanceConversion.Size]);
        Assert.Equal(ActorAppearanceConversion.BuildColorInfo(24, 42, 12), appearanceIds[ActorAppearanceConversion.ColorInfo]);
        Assert.Equal(ActorAppearanceConversion.BuildHighlightHair(4, 5, 0x123), appearanceIds[ActorAppearanceConversion.HighlightHair]);
    }

    [Fact]
    public void CreationPayloadRejectsShortBuffers()
    {
        bool parsed = CharacterAppearancePayloadParser.TryParseCreationPayload(
            new CharacterId(1),
            new byte[0x20],
            out CharacterAppearanceRecord? appearance);

        Assert.False(parsed);
        Assert.Null(appearance);
    }

    [Fact]
    public void CreationPayloadInfoParsesStartingClassAndTownOffsets()
    {
        byte[] payload = new byte[0x49];
        payload[0x08] = 3;
        payload[0x27] = 4;
        payload[0x28] = 5;
        payload[0x29] = 6;
        WriteUInt16(payload, 0x2A, 22);
        payload[0x48] = 2;

        bool parsed = CharacterCreationPayloadParser.TryParse(payload, out CharacterCreationPayloadInfo info);

        Assert.True(parsed);
        Assert.Equal((byte)3, info.Tribe);
        Assert.Equal((byte)4, info.Guardian);
        Assert.Equal((byte)5, info.BirthMonth);
        Assert.Equal((byte)6, info.BirthDay);
        Assert.Equal((byte)22, info.StartingClass);
        Assert.Equal((byte)2, info.InitialTown);
        Assert.True(CharacterStartingLocations.TryFromInitialTown(info.InitialTown, out CharacterStartingLocation location));
        Assert.Equal(new ZoneId(166), location.ZoneId);
        Assert.Equal(369.5434f, location.X, 3);
    }

    [Fact]
    public void CreationStartingLocationRejectsUnknownTown()
    {
        Assert.False(CharacterStartingLocations.TryFromInitialTown(9, out _));
    }

    [Fact]
    public void StartingEquipmentAppliesLegacyClassAndUndershirtDefaults()
    {
        CharacterAppearanceRecord appearance = new(
            new CharacterId(7),
            ModelId: 1,
            Tribe: 3,
            Size: 2,
            HairStyle: 0,
            HairHighlightColor: 0,
            HairVariation: 0,
            FaceType: 0,
            Characteristics: 0,
            CharacteristicsColor: 0,
            FaceEyebrows: 0,
            FaceIrisSize: 0,
            FaceEyeShape: 0,
            FaceNose: 0,
            FaceFeatures: 0,
            FaceMouth: 0,
            Ears: 0,
            HairColor: 0,
            SkinColor: 0,
            EyeColor: 0,
            Voice: 0,
            MainHand: 0,
            OffHand: 0,
            SpMainHand: 0,
            SpOffHand: 0,
            Throwing: 0,
            Pack: 0,
            Pouch: 0,
            Head: 0,
            Body: 0,
            Legs: 0,
            Hands: 0,
            Feet: 0,
            Waist: 0,
            Neck: 0,
            LeftEar: 0,
            RightEar: 0,
            LeftWrist: 0,
            RightWrist: 0,
            LeftIndex: 0,
            RightIndex: 0,
            LeftFinger: 0,
            RightFinger: 0);

        CharacterAppearanceRecord equipped = CharacterStartingEquipment.Apply(appearance, startingClass: 4);

        Assert.Equal(147850310u, equipped.MainHand);
        Assert.Equal(23713u, equipped.Head);
        Assert.Equal(1187u, equipped.Body);
        Assert.Equal(10016u, equipped.Legs);
        Assert.Equal(6144u, equipped.Waist);
    }

    [Fact]
    public void LobbyAppearancePayloadBuilderCreatesLegacyEncodedCharacterListBlob()
    {
        byte[] payload = new byte[0x49];
        payload[0x08] = 3;
        payload[0x27] = 4;
        payload[0x28] = 5;
        payload[0x29] = 6;
        WriteUInt16(payload, 0x2A, 22);
        payload[0x48] = 2;
        CharacterCreationPayloadParser.TryParse(payload, out CharacterCreationPayloadInfo info);
        CharacterAppearancePayloadParser.TryParseCreationPayload(new CharacterId(99), payload, out CharacterAppearanceRecord? appearance);

        byte[] lobbyPayload = LobbyAppearancePayloadBuilder.Build(
            "Ian One",
            CharacterStartingEquipment.Apply(appearance!, info.StartingClass),
            info);
        string encoded = System.Text.Encoding.ASCII.GetString(lobbyPayload).TrimEnd('\0');
        byte[] decoded = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/'));

        Assert.Equal(0x190, lobbyPayload.Length);
        Assert.Equal(0xF5, decoded.Length);
        Assert.Equal(0x000004C0u, BitConverter.ToUInt32(decoded.AsSpan(0x00)));
        Assert.Equal(0x232327EAu, BitConverter.ToUInt32(decoded.AsSpan(0x04)));
        Assert.Contains("Ian One", System.Text.Encoding.UTF8.GetString(decoded));
    }

    [Fact]
    public void LobbyAppearancePayloadProfileParserRecoversUpgradedCharacterProfile()
    {
        byte[] creationPayload = new byte[0x49];
        creationPayload[0x08] = 3;
        creationPayload[0x27] = 4;
        creationPayload[0x28] = 5;
        creationPayload[0x29] = 6;
        WriteUInt16(creationPayload, 0x2A, 7);
        creationPayload[0x48] = 2;
        CharacterCreationPayloadParser.TryParse(creationPayload, out CharacterCreationPayloadInfo expected);
        CharacterAppearancePayloadParser.TryParseCreationPayload(
            new CharacterId(99),
            creationPayload,
            out CharacterAppearanceRecord? appearance);
        byte[] lobbyPayload = LobbyAppearancePayloadBuilder.Build("Ian Two", appearance!, expected);

        bool parsed = LobbyAppearancePayloadProfileParser.TryParse(lobbyPayload, out CharacterCreationPayloadInfo recovered);

        Assert.True(parsed);
        Assert.Equal(expected, recovered);
    }

    private static void WriteUInt16(byte[] payload, int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset), value);
    }
}
