using System;

namespace AetherXIV.Core.Map
{
    static class ChocoboStopPolicy
    {
        public const uint ActorClassId = 1090464;
        public const string ClassName = "ChocoboStop";
        public const string DefaultTrigger = "pushDefault";
        public const string RequestTrigger = "_!pushRequest";

        public static bool IsStopActor(uint actorClassId, string className)
        {
            return actorClassId == ActorClassId
                || String.Equals(className, ClassName, StringComparison.Ordinal);
        }

        public static bool IsBoundaryTrigger(string eventName)
        {
            return String.Equals(eventName, DefaultTrigger, StringComparison.Ordinal)
                || String.Equals(eventName, RequestTrigger, StringComparison.Ordinal);
        }

        public static bool CanStartWhileMounted(uint actorClassId, string className, string eventName)
        {
            return IsStopActor(actorClassId, className) && IsBoundaryTrigger(eventName);
        }

        public static bool ShouldRefuseTransition(ushort mountState, bool destinationCanRide)
        {
            return mountState != 0 && !destinationCanRide;
        }
    }
}
