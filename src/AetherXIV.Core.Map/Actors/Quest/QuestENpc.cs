namespace AetherXIV.Core.Map.Actors
{
    class QuestENpc
    {
        public uint ActorClassId { get; }
        public byte QuestFlagType { get; }
        public bool IsTalkEnabled { get; }
        public bool IsPushEnabled { get; }
        public bool IsEmoteEnabled { get; }
        public bool IsSpawned { get; }

        public QuestENpc(uint actorClassId, byte questFlagType, bool isTalkEnabled,
            bool isPushEnabled, bool isEmoteEnabled, bool isSpawned)
        {
            ActorClassId = actorClassId;
            QuestFlagType = questFlagType;
            IsTalkEnabled = isTalkEnabled;
            IsPushEnabled = isPushEnabled;
            IsEmoteEnabled = isEmoteEnabled;
            IsSpawned = isSpawned;
        }

        public bool HasSamePresentation(QuestENpc other)
        {
            return other != null
                && QuestFlagType == other.QuestFlagType
                && IsTalkEnabled == other.IsTalkEnabled
                && IsPushEnabled == other.IsPushEnabled
                && IsEmoteEnabled == other.IsEmoteEnabled
                && IsSpawned == other.IsSpawned;
        }

        internal static bool ShouldBroadcast(
            QuestENpc previous,
            QuestENpc current,
            bool forceForAreaChange)
        {
            return forceForAreaChange
                || previous == null
                || !previous.HasSamePresentation(current);
        }
    }
}
