using System;

namespace AetherXIV.Core.Common
{
    public enum PlayableCharacterSex : byte
    {
        Male = 0,
        Female = 1
    }

    public enum PlayableCharacterRace : byte
    {
        Hyur = 1,
        Elezen = 2,
        Lalafell = 3,
        Miqote = 4,
        Roegadyn = 5
    }

    /// <summary>
    /// Canonical 1.23b player identity mapping. In the retail client the
    /// tribe id carries both clan and sex, while the small model id drives
    /// PlayerBaseClass:isFemale/isMale and other client-side presentation.
    /// Those values must describe the same identity.
    /// </summary>
    public static class PlayableCharacterIdentity
    {
        public const byte FirstTribeId = 1;
        public const byte LastTribeId = 15;
        public const uint UseTribeDefaultModel = 0xFFFFFFFF;

        public static bool IsValidTribe(byte tribe)
        {
            return tribe >= FirstTribeId && tribe <= LastTribeId;
        }

        public static bool TryGetModelId(byte tribe, out uint modelId)
        {
            modelId = tribe switch
            {
                1 => 1,           // Hyur Midlander male
                2 => 2,           // Hyur Midlander female
                3 => 9,           // Hyur Highlander male
                4 or 6 => 3,      // Elezen male
                5 or 7 => 4,      // Elezen female
                8 or 10 => 5,     // Lalafell male
                9 or 11 => 6,     // Lalafell female
                12 or 13 => 8,    // Miqo'te female
                14 or 15 => 7,    // Roegadyn male
                _ => 0
            };
            return modelId != 0;
        }

        public static uint GetModelId(byte tribe)
        {
            if (TryGetModelId(tribe, out uint modelId))
                return modelId;

            throw new ArgumentOutOfRangeException(
                nameof(tribe),
                tribe,
                $"Tribe must be between {FirstTribeId} and {LastTribeId}.");
        }

        public static bool TryGetSex(byte tribe, out PlayableCharacterSex sex)
        {
            switch (tribe)
            {
                case 2:
                case 5:
                case 7:
                case 9:
                case 11:
                case 12:
                case 13:
                    sex = PlayableCharacterSex.Female;
                    return true;
                case 1:
                case 3:
                case 4:
                case 6:
                case 8:
                case 10:
                case 14:
                case 15:
                    sex = PlayableCharacterSex.Male;
                    return true;
                default:
                    sex = PlayableCharacterSex.Male;
                    return false;
            }
        }

        public static bool TryGetRace(byte tribe, out PlayableCharacterRace race)
        {
            if (tribe >= 1 && tribe <= 3)
                race = PlayableCharacterRace.Hyur;
            else if (tribe >= 4 && tribe <= 7)
                race = PlayableCharacterRace.Elezen;
            else if (tribe >= 8 && tribe <= 11)
                race = PlayableCharacterRace.Lalafell;
            else if (tribe == 12 || tribe == 13)
                race = PlayableCharacterRace.Miqote;
            else if (tribe == 14 || tribe == 15)
                race = PlayableCharacterRace.Roegadyn;
            else
            {
                race = PlayableCharacterRace.Hyur;
                return false;
            }

            return true;
        }

        public static bool IsFemale(byte tribe)
        {
            return TryGetSex(tribe, out PlayableCharacterSex sex)
                && sex == PlayableCharacterSex.Female;
        }

        public static bool IsMale(byte tribe)
        {
            return TryGetSex(tribe, out PlayableCharacterSex sex)
                && sex == PlayableCharacterSex.Male;
        }

        public static bool IsModelConsistent(byte tribe, uint storedModelId)
        {
            return storedModelId == UseTribeDefaultModel
                || (TryGetModelId(tribe, out uint expectedModelId) && storedModelId == expectedModelId);
        }
    }
}
