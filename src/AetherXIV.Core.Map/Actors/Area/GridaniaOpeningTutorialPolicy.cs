using System;
using System.Globalization;
using AetherXIV.Core.Map.Actors;

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

        /// <summary>
        /// The opening battle is a closed content encounter. Its combatants
        /// must not inherit the world-mob leash while the director group is
        /// live, otherwise a wolf can return home, heal, and become invalid
        /// after the player has already advanced to the weaponskill step.
        /// </summary>
        public static bool IsLiveContentCombat(Character actor, Character target)
        {
            if (actor == null || target == null || actor.IsDead() || target.IsDead())
                return false;

            if (!(actor.zone is PrivateAreaContent contentArea) ||
                !IsContentArea(contentArea.GetPrivateAreaName()))
            {
                return false;
            }

            return actor.currentContentGroup != null &&
                target.currentContentGroup == actor.currentContentGroup;
        }

        public static string BuildBattleCompleteSignal(uint playerActorId)
        {
            return BuildPlayerSignal("battleComplete", playerActorId);
        }

        public static string BuildPlayerSignal(string signal, uint playerActorId) =>
            signal + ":" + playerActorId.ToString(CultureInfo.InvariantCulture);
    }
}
