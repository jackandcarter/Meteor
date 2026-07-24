using AetherXIV.Core.Common;
using System;

namespace AetherXIV.Core.Map.utils
{
    class CharacterUtils
    {
        public struct FaceInfo
        {
            [BitfieldLength(5)]
            public uint characteristics;
            [BitfieldLength(3)]
            public uint characteristicsColor;
            [BitfieldLength(6)]
            public uint type;
            [BitfieldLength(2)]
            public uint ears;
            [BitfieldLength(2)]
            public uint mouth;
            [BitfieldLength(2)]
            public uint features;
            [BitfieldLength(3)]
            public uint nose;
            [BitfieldLength(3)]
            public uint eyeShape;
            [BitfieldLength(1)]
            public uint irisSize;
            [BitfieldLength(3)]
            public uint eyebrows;
            [BitfieldLength(2)]
            public uint unknown;
        }
        
        public static FaceInfo GetFaceInfo(byte characteristics, byte characteristicsColor, byte faceType, byte ears, byte faceMouth, byte faceFeatures, byte faceNose, byte faceEyeShape, byte faceIrisSize, byte faceEyebrows)
        {
            FaceInfo faceInfo = new FaceInfo();
            faceInfo.characteristics = characteristics;
            faceInfo.characteristicsColor = characteristicsColor;
            faceInfo.type = faceType;
            faceInfo.ears = ears;
            faceInfo.features = faceFeatures;
            faceInfo.eyebrows = faceEyebrows;
            faceInfo.eyeShape = faceEyeShape;
            faceInfo.irisSize = faceIrisSize;
            faceInfo.mouth = faceMouth;
            faceInfo.nose = faceNose;
            return faceInfo;
        }

        public static UInt32 GetTribeModel(byte tribe)
        {
            return PlayableCharacterIdentity.GetModelId(tribe);
        }

        public static string GetClassNameForId(short id)
        {
            switch (id)
            {
                case 2: return "pug";
                case 3: return "gla";
                case 4: return "mrd";
                case 7: return "arc";
                case 8: return "lnc";
                case 22: return "thm";
                case 23: return "cnj";
                case 29: return "crp";
                case 30: return "bsm";
                case 31: return "arm";
                case 32: return "gsm";
                case 33: return "ltw";
                case 34: return "wvr";
                case 35: return "alc";
                case 36: return "cul";
                case 39: return "min";
                case 40: return "btn";
                case 41: return "fsh";
                default: return "undefined";
            }
        }

    }
}
