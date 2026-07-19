namespace AetherXIV.Data;

public static class ActorAppearanceConversion
{
    public const int SlotCount = 28;
    public const int Size = 0;
    public const int ColorInfo = 1;
    public const int FaceInfo = 2;
    public const int HighlightHair = 3;
    public const int Voice = 4;
    public const int MainHand = 5;
    public const int OffHand = 6;
    public const int SpMainHand = 7;
    public const int SpOffHand = 8;
    public const int Throwing = 9;
    public const int Pack = 10;
    public const int Pouch = 11;
    public const int HeadGear = 12;
    public const int BodyGear = 13;
    public const int LegsGear = 14;
    public const int HandsGear = 15;
    public const int FeetGear = 16;
    public const int WaistGear = 17;
    public const int NeckGear = 18;
    public const int LeftEar = 19;
    public const int RightEar = 20;
    public const int RightWrist = 21;
    public const int LeftWrist = 22;
    public const int RightRingFinger = 23;
    public const int LeftRingFinger = 24;
    public const int RightIndexFinger = 25;
    public const int LeftIndexFinger = 26;
    public const int Unknown = 27;

    public static uint[] ToLegacyAppearanceIds(ActorAppearanceRecord row)
    {
        uint[] appearanceIds = new uint[SlotCount];
        appearanceIds[Size] = row.Size;
        appearanceIds[ColorInfo] = BuildColorInfo(row.SkinColor, row.HairColor, row.EyeColor);
        appearanceIds[FaceInfo] = BuildFaceInfo(
            row.Characteristics,
            row.CharacteristicsColor,
            row.FaceType,
            row.Ears,
            row.FaceMouth,
            row.FaceFeatures,
            row.FaceNose,
            row.FaceEyeShape,
            row.FaceIrisSize,
            row.FaceEyebrows);
        appearanceIds[HighlightHair] = BuildHighlightHair(row.HairHighlightColor, row.HairVariation, row.HairStyle);
        appearanceIds[Voice] = row.Voice;
        appearanceIds[MainHand] = row.MainHand;
        appearanceIds[OffHand] = row.OffHand;
        appearanceIds[SpMainHand] = row.SpMainHand;
        appearanceIds[SpOffHand] = row.SpOffHand;
        appearanceIds[Throwing] = row.Throwing;
        appearanceIds[Pack] = row.Pack;
        appearanceIds[Pouch] = row.Pouch;
        appearanceIds[HeadGear] = row.Head;
        appearanceIds[BodyGear] = row.Body;
        appearanceIds[LegsGear] = row.Legs;
        appearanceIds[HandsGear] = row.Hands;
        appearanceIds[FeetGear] = row.Feet;
        appearanceIds[WaistGear] = row.Waist;
        appearanceIds[NeckGear] = row.Neck;
        appearanceIds[RightEar] = row.RightEar;
        appearanceIds[LeftEar] = row.LeftEar;
        appearanceIds[RightIndexFinger] = row.LeftIndex;
        appearanceIds[LeftIndexFinger] = row.RightIndex;
        appearanceIds[RightRingFinger] = row.RightFinger;
        appearanceIds[LeftRingFinger] = row.LeftFinger;
        return appearanceIds;
    }

    public static uint[] ToLegacyAppearanceIds(CharacterAppearanceRecord row)
    {
        uint[] appearanceIds = new uint[SlotCount];
        appearanceIds[Size] = row.Size;
        appearanceIds[ColorInfo] = BuildColorInfo(row.SkinColor, row.HairColor, row.EyeColor);
        appearanceIds[FaceInfo] = BuildFaceInfo(
            row.Characteristics,
            row.CharacteristicsColor,
            row.FaceType,
            row.Ears,
            row.FaceMouth,
            row.FaceFeatures,
            row.FaceNose,
            row.FaceEyeShape,
            row.FaceIrisSize,
            row.FaceEyebrows);
        appearanceIds[HighlightHair] = BuildHighlightHair(row.HairHighlightColor, row.HairVariation, row.HairStyle);
        appearanceIds[Voice] = row.Voice;
        appearanceIds[MainHand] = row.MainHand;
        appearanceIds[OffHand] = row.OffHand;
        appearanceIds[SpMainHand] = row.SpMainHand;
        appearanceIds[SpOffHand] = row.SpOffHand;
        appearanceIds[Throwing] = row.Throwing;
        appearanceIds[Pack] = row.Pack;
        appearanceIds[Pouch] = row.Pouch;
        appearanceIds[HeadGear] = row.Head;
        appearanceIds[BodyGear] = row.Body;
        appearanceIds[LegsGear] = row.Legs;
        appearanceIds[HandsGear] = row.Hands;
        appearanceIds[FeetGear] = row.Feet;
        appearanceIds[WaistGear] = row.Waist;
        appearanceIds[NeckGear] = row.Neck;
        appearanceIds[RightEar] = row.RightEar;
        appearanceIds[LeftEar] = row.LeftEar;
        appearanceIds[RightWrist] = row.RightWrist;
        appearanceIds[LeftWrist] = row.LeftWrist;
        appearanceIds[RightIndexFinger] = row.LeftIndex;
        appearanceIds[LeftIndexFinger] = row.RightIndex;
        appearanceIds[RightRingFinger] = row.RightFinger;
        appearanceIds[LeftRingFinger] = row.LeftFinger;
        return appearanceIds;
    }

    public static uint BuildColorInfo(uint skinColor, uint hairColor, uint eyeColor)
    {
        return skinColor | (hairColor << 10) | (eyeColor << 20);
    }

    public static uint BuildHighlightHair(uint hairHighlightColor, uint hairVariation, uint hairStyle)
    {
        return hairHighlightColor | (hairVariation << 5) | (hairStyle << 10);
    }

    public static uint BuildFaceInfo(
        uint characteristics,
        uint characteristicsColor,
        uint faceType,
        uint ears,
        uint faceMouth,
        uint faceFeatures,
        uint faceNose,
        uint faceEyeShape,
        uint faceIrisSize,
        uint faceEyebrows)
    {
        uint result = 0;
        int offset = 0;
        WriteBits(characteristics, 5);
        WriteBits(characteristicsColor, 3);
        WriteBits(faceType, 6);
        WriteBits(ears, 2);
        WriteBits(faceMouth, 2);
        WriteBits(faceFeatures, 2);
        WriteBits(faceNose, 3);
        WriteBits(faceEyeShape, 3);
        WriteBits(faceIrisSize, 1);
        WriteBits(faceEyebrows, 3);
        WriteBits(0, 2);
        return result;

        void WriteBits(uint value, int length)
        {
            uint mask = 0;
            for (int i = 0; i < length; i++)
                mask |= 1u << i;

            result |= (value & mask) << offset;
            offset += length;
        }
    }
}
