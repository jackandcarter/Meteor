using System.Collections.Generic;

namespace AetherXIV.Core.Map.actors
{
    class EventList
    {
        public List<TalkEventCondition> talkEventConditions;
        public List<NoticeEventCondition> noticeEventConditions;
        public List<EmoteEventCondition> emoteEventConditions;
        public List<PushCircleEventCondition> pushWithCircleEventConditions;
        public List<PushFanEventCondition> pushWithFanEventConditions;
        public List<PushBoxEventCondition> pushWithBoxEventConditions;

        public class TalkEventCondition
        {
            public byte unknown1;
            public bool isDisabled = false;
            public string conditionName;
        }

        public class NoticeEventCondition
        {
            public byte unknown1;
            public byte unknown2;
            public bool sendStatus = true;
            public string conditionName;

            public NoticeEventCondition(string name, byte unk1, byte unk2)
            {
                conditionName = name;
                unknown1 = unk1;
                unknown2 = unk2;
            }
        }

        public class EmoteEventCondition
        {
            public byte unknown1;
            public byte unknown2;
            public byte emoteId;
            public string conditionName;
        }

        public class PushCircleEventCondition
        {
            public string conditionName = "";
            public float radius = 30.0f;
            public bool outwards = false;
            public bool silent = true;
            public bool isDisabled = false;

            // The first word varies between otherwise identical official
            // ChocoboStop observations and appears to be capture-local data.
            // Keep the legacy fallback unless a reviewed observation supplies
            // it, while preserving every stable byte exposed by the traces.
            public uint unknown1 = 0x44533088;
            public bool useSourceActorId = false;
            public float secondaryRadius = 100.0f;
            public byte flags = 0x01;
            public byte unknown2 = 0;
        }

        public class PushFanEventCondition
        {
            public string conditionName;
            public float radius = 30.0f;
            public bool outwards = false;
            public bool silent = true;
        }

        public class PushBoxEventCondition
        {
            public uint bgObj;
            public uint layout;
            public string conditionName = "";
            public string reactName = "";
            public bool outwards = false;
            public bool silent = true;
        }
    }
}
