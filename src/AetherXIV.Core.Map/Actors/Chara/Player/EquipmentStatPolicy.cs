using System;

namespace AetherXIV.Core.Map.actors.chara.player
{
    // Pure item rules recovered from the installed 1.23b client and checked
    // against official equipment-change captures. Keeping the arithmetic here
    // makes the recovered rules independently testable without introducing a
    // second player-stat owner.
    internal static class EquipmentStatPolicy
    {
        public const int ParamNameModifierOffset = 15001;
        public const int ParamNameModifierMax = 15132;

        public static bool TryGetModifierId(int paramType, out uint modifierId)
        {
            if (paramType < ParamNameModifierOffset || paramType > ParamNameModifierMax)
            {
                modifierId = 0;
                return false;
            }

            modifierId = (uint)(paramType - ParamNameModifierOffset);
            return true;
        }

        public static bool IsOrdinaryBonusPair(int pairNumber)
        {
            return pairNumber == 3 || (pairNumber >= 5 && pairNumber <= 10);
        }

        public static bool IsHighQualityBonusPair(int pairNumber)
        {
            return pairNumber == 4;
        }

        public static bool ShouldApplyBonusPair(int pairNumber, bool hasHighQualityBonus)
        {
            return IsOrdinaryBonusPair(pairNumber)
                || (hasHighQualityBonus && IsHighQualityBonusPair(pairNumber));
        }

        public static double CalculateLevelAdjust(int playerLevel, int itemLevel)
        {
            if (itemLevel <= playerLevel)
                return 1.0;

            int difference = Math.Min(7, itemLevel - playerLevel);
            if (itemLevel >= 31)
                return 0.8 - (difference * 0.1);

            if (itemLevel >= 11)
                return 1.0 - (difference * 0.1);

            switch (difference)
            {
                case 1: return 0.90;
                case 2: return 0.85;
                case 3: return 0.80;
                case 4: return 0.75;
                case 5: return 0.70;
                case 6: return 0.60;
                default: return 0.50;
            }
        }

        public static int ApplyHighQualityValue(int value, double multiplier, bool isHighQuality)
        {
            if (!isHighQuality || value <= 0)
                return value;

            return Math.Max(value + 1, (int)Math.Ceiling(value * multiplier));
        }

        public static int CalculateArmorDefense(int defense, int playerLevel, int itemLevel, bool isHighQuality)
        {
            int adjustedDefense = ApplyHighQualityValue(defense, 1.05, isHighQuality);
            return (int)Math.Ceiling(adjustedDefense * CalculateLevelAdjust(playerLevel, itemLevel));
        }

        public static int CalculateToolValue(int value, bool isHighQuality)
        {
            return ApplyHighQualityValue(value, 1.03, isHighQuality);
        }
    }
}
