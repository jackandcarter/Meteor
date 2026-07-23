using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors;
using System;
using System.IO;
using System.Text;

namespace AetherXIV.Core.Map.packets.send.actor.events
{
    class SetEmoteEventCondition
    {
        public const ushort OPCODE = 0x016C;
        public const uint PACKET_SIZE = 0x48;

        public static SubPacket BuildPacket(uint sourceActorId, EventList.EmoteEventCondition condition)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    EventConditionDiagnostics.TraceEmote(sourceActorId, condition);
                    // Retail 0x016C body: u8 kind, u8 opaque, u16 emote id,
                    // then the fixed condition name. Omitting unknown2 shifts
                    // the id/name and makes the client treat a matching quest
                    // emote as an ordinary EmoteStandardCommand.
                    binWriter.Write((Byte)4);
                    binWriter.Write((Byte)condition.unknown2);
                    binWriter.Write((UInt16)condition.emoteId); //82, 76, 6E
                    binWriter.Write(Encoding.ASCII.GetBytes(condition.conditionName), 0, Encoding.ASCII.GetByteCount(condition.conditionName) >= 0x24 ? 0x24 : Encoding.ASCII.GetByteCount(condition.conditionName));
                }
            }

            return new SubPacket(OPCODE, sourceActorId, data);
        }

    }
}
