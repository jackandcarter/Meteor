using System;

namespace AetherXIV.Core.Map
{
    static class SeamlessBoundaryPolicy
    {
        public static bool ContainsPoint(float x, float z, float x1, float z1, float x2, float z2)
        {
            if (!IsFinite(x) || !IsFinite(z)
                || !IsFinite(x1) || !IsFinite(z1)
                || !IsFinite(x2) || !IsFinite(z2))
                return false;

            float minX = Math.Min(x1, x2);
            float maxX = Math.Max(x1, x2);
            float minZ = Math.Min(z1, z2);
            float maxZ = Math.Max(z1, z2);
            return minX <= x && x <= maxX && minZ <= z && z <= maxZ;
        }

        // Position packets are samples. Test the complete movement segment so a
        // narrow transition volume cannot be skipped between two valid samples.
        public static bool MovementIntersectsBounds(
            float previousX,
            float previousZ,
            float currentX,
            float currentZ,
            float x1,
            float z1,
            float x2,
            float z2)
        {
            if (!IsFinite(previousX) || !IsFinite(previousZ)
                || !IsFinite(currentX) || !IsFinite(currentZ)
                || !IsFinite(x1) || !IsFinite(z1)
                || !IsFinite(x2) || !IsFinite(z2))
                return false;

            float minX = Math.Min(x1, x2);
            float maxX = Math.Max(x1, x2);
            float minZ = Math.Min(z1, z2);
            float maxZ = Math.Max(z1, z2);
            float minimumTime = 0.0f;
            float maximumTime = 1.0f;

            if (!IntersectsAxis(previousX, currentX - previousX, minX, maxX, ref minimumTime, ref maximumTime))
                return false;

            return IntersectsAxis(previousZ, currentZ - previousZ, minZ, maxZ, ref minimumTime, ref maximumTime);
        }

        public static bool ReachedDestination(
            bool destinationIsMerged,
            float previousX,
            float previousZ,
            float currentX,
            float currentZ,
            float destinationX1,
            float destinationZ1,
            float destinationX2,
            float destinationZ2,
            float mergeX1,
            float mergeZ1,
            float mergeX2,
            float mergeZ2)
        {
            if (ContainsPoint(currentX, currentZ, destinationX1, destinationZ1, destinationX2, destinationZ2))
                return true;

            if (!MovementIntersectsBounds(
                    previousX,
                    previousZ,
                    currentX,
                    currentZ,
                    destinationX1,
                    destinationZ1,
                    destinationX2,
                    destinationZ2))
                return false;

            return destinationIsMerged || MovementIntersectsBounds(
                previousX,
                previousZ,
                currentX,
                currentZ,
                mergeX1,
                mergeZ1,
                mergeX2,
                mergeZ2);
        }

        // The merge and destination boxes describe opposite ends of the same
        // transition corridor. Retain the merged actor set while the player is
        // between them instead of dropping it in the unsampled gap.
        public static bool IsWithinTransitionCorridor(
            float x,
            float z,
            float destinationX1,
            float destinationZ1,
            float destinationX2,
            float destinationZ2,
            float mergeX1,
            float mergeZ1,
            float mergeX2,
            float mergeZ2)
        {
            float corridorX1 = Math.Min(Math.Min(destinationX1, destinationX2), Math.Min(mergeX1, mergeX2));
            float corridorX2 = Math.Max(Math.Max(destinationX1, destinationX2), Math.Max(mergeX1, mergeX2));
            float corridorZ1 = Math.Min(Math.Min(destinationZ1, destinationZ2), Math.Min(mergeZ1, mergeZ2));
            float corridorZ2 = Math.Max(Math.Max(destinationZ1, destinationZ2), Math.Max(mergeZ1, mergeZ2));
            return ContainsPoint(x, z, corridorX1, corridorZ1, corridorX2, corridorZ2);
        }

        private static bool IntersectsAxis(
            float start,
            float delta,
            float minimum,
            float maximum,
            ref float minimumTime,
            ref float maximumTime)
        {
            if (delta == 0.0f)
                return minimum <= start && start <= maximum;

            float enter = (minimum - start) / delta;
            float exit = (maximum - start) / delta;
            if (enter > exit)
            {
                float swap = enter;
                enter = exit;
                exit = swap;
            }

            minimumTime = Math.Max(minimumTime, enter);
            maximumTime = Math.Min(maximumTime, exit);
            return minimumTime <= maximumTime;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
