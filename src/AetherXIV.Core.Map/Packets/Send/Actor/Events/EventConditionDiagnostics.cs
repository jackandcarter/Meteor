using System;

using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors;

namespace AetherXIV.Core.Map.packets.send.actor.events
{
    static class EventConditionDiagnostics
    {
        public static void TraceTalk(uint sourceActorId, EventList.TalkEventCondition condition)
        {
            if (!DevDiagnostics.Enabled || condition == null)
                return;

            DevDiagnostics.Trace(
                "event.condition",
                "action", "define",
                "conditionKind", "talk",
                "sourceActor", Hex(sourceActorId),
                "conditionName", condition.conditionName,
                "unknown1", condition.unknown1,
                "isDisabled", condition.isDisabled);
        }

        public static void TraceNotice(uint sourceActorId, EventList.NoticeEventCondition condition)
        {
            if (!DevDiagnostics.Enabled || condition == null)
                return;

            DevDiagnostics.Trace(
                "event.condition",
                "action", "define",
                "conditionKind", "notice",
                "sourceActor", Hex(sourceActorId),
                "conditionName", condition.conditionName,
                "unknown1", condition.unknown1,
                "unknown2", condition.unknown2);
        }

        public static void TraceEmote(uint sourceActorId, EventList.EmoteEventCondition condition)
        {
            if (!DevDiagnostics.Enabled || condition == null)
                return;

            DevDiagnostics.Trace(
                "event.condition",
                "action", "define",
                "conditionKind", "emote",
                "sourceActor", Hex(sourceActorId),
                "conditionName", condition.conditionName,
                "unknown1", condition.unknown1,
                "unknown2", condition.unknown2,
                "emoteId", condition.emoteId);
        }

        public static void TracePushCircle(uint sourceActorId, EventList.PushCircleEventCondition condition)
        {
            if (!DevDiagnostics.Enabled || condition == null)
                return;

            DevDiagnostics.Trace(
                "event.condition",
                "action", "define",
                "conditionKind", "pushCircle",
                "sourceActor", Hex(sourceActorId),
                "conditionName", condition.conditionName,
                "radius", condition.radius,
                "unknown1", Hex(condition.useSourceActorId ? sourceActorId : condition.unknown1),
                "useSourceActorId", condition.useSourceActorId,
                "secondaryRadius", condition.secondaryRadius,
                "flags", condition.flags,
                "unknown2", condition.unknown2,
                "outwards", condition.outwards,
                "silent", condition.silent);
        }

        public static void TracePushFan(uint sourceActorId, EventList.PushFanEventCondition condition)
        {
            if (!DevDiagnostics.Enabled || condition == null)
                return;

            DevDiagnostics.Trace(
                "event.condition",
                "action", "define",
                "conditionKind", "pushFan",
                "sourceActor", Hex(sourceActorId),
                "conditionName", condition.conditionName,
                "radius", condition.radius,
                "outwards", condition.outwards,
                "silent", condition.silent);
        }

        public static void TracePushBox(uint sourceActorId, EventList.PushBoxEventCondition condition)
        {
            if (!DevDiagnostics.Enabled || condition == null)
                return;

            DevDiagnostics.Trace(
                "event.condition",
                "action", "define",
                "conditionKind", "pushBox",
                "sourceActor", Hex(sourceActorId),
                "conditionName", condition.conditionName,
                "reactName", condition.reactName,
                "bgObj", Hex(condition.bgObj),
                "layout", Hex(condition.layout),
                "outwards", condition.outwards,
                "silent", condition.silent);
        }

        public static void TraceStatus(uint sourceActorId, bool enabled, byte type, string conditionName)
        {
            if (!DevDiagnostics.Enabled)
                return;

            DevDiagnostics.Trace(
                "event.condition.status",
                "action", "status",
                "conditionKind", StatusTypeName(type),
                "sourceActor", Hex(sourceActorId),
                "conditionName", conditionName,
                "enabled", enabled,
                "type", type);
        }

        private static string StatusTypeName(byte type)
        {
            switch (type)
            {
                case 1:
                    return "talk";
                case 2:
                    return "push";
                case 3:
                    return "emote";
                case 5:
                    return "notice";
                default:
                    return "unknown";
            }
        }

        private static string Hex(uint value)
        {
            return String.Format("0x{0:X}", value);
        }
    }
}
