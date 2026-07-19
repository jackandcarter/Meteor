using AetherXIV.Core.Map.actors;
using System;
using System.IO;
using System.Text;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor.events
{
    class SetPushEventConditionWithTriggerBox
    {
        public const ushort OPCODE = 0x0175;
        public const uint PACKET_SIZE = 0x60;

        public static SubPacket BuildPacket(uint sourceActorId, EventList.PushBoxEventCondition condition)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    EventConditionDiagnostics.TracePushBox(sourceActorId, condition);
                    binWriter.Write((UInt32)condition.bgObj);  // bgObj
                    binWriter.Write((UInt32)condition.layout);   // Layout
                    binWriter.Write((UInt32)4);       // Actor?  Always 4 in 1.23
                    binWriter.Seek(8, SeekOrigin.Current); // Unknowns
                    binWriter.Write((Byte)(condition.outwards ? 0x11 : 0x0)); //If == 0x10, Inverted Bounding Box
                    binWriter.Write((Byte)3);
                    binWriter.Write((Byte)(condition.silent ? 0x1 : 0x0)); //Silent Trigger;
                    binWriter.Write(Encoding.ASCII.GetBytes(condition.conditionName), 0, Encoding.ASCII.GetByteCount(condition.conditionName) >= 0x20 ? 0x20 : Encoding.ASCII.GetByteCount(condition.conditionName));
                    binWriter.Seek(55, SeekOrigin.Begin);
                    binWriter.Write((Byte)0);       // Unknown
                    binWriter.Write(Encoding.ASCII.GetBytes(condition.reactName), 0, Encoding.ASCII.GetByteCount(condition.reactName) >= 0x04 ? 0x04 : Encoding.ASCII.GetByteCount(condition.reactName));
                }
            }
            return new SubPacket(OPCODE, sourceActorId, data);
        }
    }
}
