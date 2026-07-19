using System;

namespace AetherXIV.Core.Map
{
    // A local zone change is initiated before the client finishes unloading the
    // previous scene. Position packets from that previous scene can therefore
    // arrive after the server has installed the destination zone. Only a
    // position near the server-issued spawn may acknowledge the transition.
    static class ZoneTransitionPositionPolicy
    {
        public const float ArrivalTolerance = 8.0f;

        public static bool IsDestinationConsistent(
            bool hasExpectedPosition,
            uint currentZoneId,
            uint expectedZoneId,
            float expectedX,
            float expectedY,
            float expectedZ,
            float receivedX,
            float receivedY,
            float receivedZ)
        {
            if (!hasExpectedPosition)
                return true;

            if (currentZoneId != expectedZoneId)
                return false;

            if (!IsFinite(expectedX) || !IsFinite(expectedY) || !IsFinite(expectedZ)
                || !IsFinite(receivedX) || !IsFinite(receivedY) || !IsFinite(receivedZ))
            {
                return false;
            }

            double deltaX = receivedX - expectedX;
            double deltaY = receivedY - expectedY;
            double deltaZ = receivedZ - expectedZ;
            double toleranceSquared = ArrivalTolerance * ArrivalTolerance;

            return deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ <= toleranceSquared;
        }

        private static bool IsFinite(float value)
        {
            return !Single.IsNaN(value) && !Single.IsInfinity(value);
        }
    }
}
