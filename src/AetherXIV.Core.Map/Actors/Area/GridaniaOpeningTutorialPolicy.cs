using System;
using System.Globalization;

namespace AetherXIV.Core.Map.actors.area
{
    /// <summary>
    /// Evidence-backed constants for the Gridania Man0g0 opening battle.
    /// Keeping these checks here prevents tutorial-only rewards and signals
    /// from leaking into ordinary battle content.
    /// </summary>
    static class GridaniaOpeningTutorialPolicy
    {
        public const string ContentAreaName = "SimpleContent30010";
        public const ushort WolfExperience = 1000;

        public static bool IsContentArea(string privateAreaName)
        {
            return String.Equals(privateAreaName, ContentAreaName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTutorialWolf(string privateAreaName, uint battleNpcId)
        {
            return IsContentArea(privateAreaName) && battleNpcId >= 3 && battleNpcId <= 5;
        }

        public static string BuildBattleCompleteSignal(uint playerActorId)
        {
            return "battleComplete:" + playerActorId.ToString(CultureInfo.InvariantCulture);
        }
    }
}
