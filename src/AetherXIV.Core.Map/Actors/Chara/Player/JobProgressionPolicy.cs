using System;

namespace AetherXIV.Core.Map.actors.chara.player
{
    public readonly record struct JobProgressionRequirement(
        byte JobId,
        byte BaseClassId,
        byte SecondaryClassId,
        uint SoulCrystalItemId);

    public static class JobProgressionPolicy
    {
        public static bool TryGetForBaseClass(byte classId, out JobProgressionRequirement requirement)
        {
            requirement = classId switch
            {
                2 => new JobProgressionRequirement(15, 2, 8, 2000202),
                3 => new JobProgressionRequirement(16, 3, 23, 2000201),
                4 => new JobProgressionRequirement(17, 4, 3, 2000203),
                7 => new JobProgressionRequirement(18, 7, 23, 2000205),
                8 => new JobProgressionRequirement(19, 8, 2, 2000204),
                22 => new JobProgressionRequirement(26, 22, 2, 2000207),
                23 => new JobProgressionRequirement(27, 23, 3, 2000206),
                _ => default
            };

            return requirement.JobId != 0;
        }

        public static bool MeetsLevelRequirements(
            JobProgressionRequirement requirement,
            Func<byte, short> getClassLevel)
        {
            if (getClassLevel == null)
                throw new ArgumentNullException(nameof(getClassLevel));

            return getClassLevel(requirement.BaseClassId) >= 30
                && getClassLevel(requirement.SecondaryClassId) >= 15;
        }
    }
}
