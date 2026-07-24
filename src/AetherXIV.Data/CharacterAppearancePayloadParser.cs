using System.Buffers.Binary;
using AetherXIV.Core;
using AetherXIV.Core.Common;

namespace AetherXIV.Data;

public static class CharacterAppearancePayloadParser
{
    private const int MinimumCharacterCreationPayloadSize = 0x49;

    public static bool TryParseCreationPayload(
        CharacterId characterId,
        ReadOnlySpan<byte> payload,
        out CharacterAppearanceRecord? appearance)
    {
        appearance = null;
        if (payload.Length < MinimumCharacterCreationPayloadSize)
            return false;

        byte tribe = payload[0x08];
        if (!PlayableCharacterIdentity.IsValidTribe(tribe))
            return false;

        byte size = payload[0x09];
        ushort hairStyle = ReadUInt16(payload, 0x0A);
        byte hairHighlightColor = payload[0x0C];
        byte hairVariation = payload[0x0D];
        byte faceType = payload[0x0E];
        byte characteristics = payload[0x0F];
        byte characteristicsColor = payload[0x10];
        byte faceEyebrows = payload[0x15];
        byte faceIrisSize = payload[0x16];
        byte faceEyeShape = payload[0x17];
        byte faceNose = payload[0x18];
        byte faceFeatures = payload[0x19];
        byte faceMouth = payload[0x1A];
        byte ears = payload[0x1B];
        ushort hairColor = ReadUInt16(payload, 0x1C);
        ushort skinColor = ReadUInt16(payload, 0x22);
        ushort eyeColor = ReadUInt16(payload, 0x24);
        byte voice = payload[0x26];

        appearance = new CharacterAppearanceRecord(
            characterId,
            CharacterModelIds.FromTribe(tribe),
            tribe,
            size,
            hairStyle,
            hairHighlightColor,
            hairVariation,
            faceType,
            characteristics,
            characteristicsColor,
            faceEyebrows,
            faceIrisSize,
            faceEyeShape,
            faceNose,
            faceFeatures,
            faceMouth,
            ears,
            hairColor,
            skinColor,
            eyeColor,
            voice,
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
        return true;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> payload, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(payload[offset..]);
    }
}

public static class CharacterModelIds
{
    public static uint FromTribe(byte tribe)
    {
        return PlayableCharacterIdentity.GetModelId(tribe);
    }
}
