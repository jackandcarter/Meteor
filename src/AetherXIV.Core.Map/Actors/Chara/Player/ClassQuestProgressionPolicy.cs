using System;

namespace AetherXIV.Core.Map.actors.chara.player
{
    public readonly record struct ClassQuestRequirement(
        uint QuestId,
        string QuestName,
        byte ClassId,
        short RequiredLevel,
        uint PrerequisiteQuestId);

    public static class ClassQuestProgressionPolicy
    {
        private static readonly ClassQuestRequirement[] Requirements =
        {
            // Carpenter (Woodworking) — the complete retail 1.x guild-story chain.
            new(110300, "Wdk200", 29, 20, 0),
            new(110301, "Wdk300", 29, 30, 110300),
            new(110302, "Wdk306", 29, 36, 110301)
        };

        public static bool TryGet(uint questId, out ClassQuestRequirement requirement)
        {
            foreach (ClassQuestRequirement candidate in Requirements)
            {
                if (candidate.QuestId == questId)
                {
                    requirement = candidate;
                    return true;
                }
            }

            requirement = default;
            return false;
        }

        public static bool MeetsRequirements(
            ClassQuestRequirement requirement,
            byte currentClassOrJob,
            Func<byte, short> getClassLevel,
            Func<uint, bool> isQuestCompleted)
        {
            if (getClassLevel == null)
                throw new ArgumentNullException(nameof(getClassLevel));
            if (isQuestCompleted == null)
                throw new ArgumentNullException(nameof(isQuestCompleted));

            return currentClassOrJob == requirement.ClassId
                && getClassLevel(requirement.ClassId) >= requirement.RequiredLevel
                && (requirement.PrerequisiteQuestId == 0
                    || isQuestCompleted(requirement.PrerequisiteQuestId));
        }
    }
}
