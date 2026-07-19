using AetherXIV.Core.Map.actors;
using System;
using System.IO;
using System.Text;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor.events
{
    class SetPushEventConditionWithCircle
    {
        public const ushort OPCODE = 0x016F;
        public const uint PACKET_SIZE = 0x58;

        public static SubPacket BuildPacket(uint sourceActorId, EventList.PushCircleEventCondition condition)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    EventConditionDiagnostics.TracePushCircle(sourceActorId, condition);
                    binWriter.Write((Single)condition.radius);
                    binWriter.Write((UInt32)(condition.useSourceActorId ? sourceActorId : condition.unknown1));
                    binWriter.Write((Single)condition.secondaryRadius);
                    binWriter.Seek(4, SeekOrigin.Current);
                    binWriter.Write((Byte)(condition.flags | (condition.outwards ? 0x10 : 0x00))); //0x10 inverts the volume.
                    binWriter.Write((Byte)condition.unknown2);
                    binWriter.Write((Byte)(condition.silent ? 0x1 : 0x0)); //Silent Trigger
                    binWriter.Write(Encoding.ASCII.GetBytes(condition.conditionName), 0, Encoding.ASCII.GetByteCount(condition.conditionName) >= 0x24 ? 0x24 : Encoding.ASCII.GetByteCount(condition.conditionName));
                }
            }

            return new SubPacket(OPCODE, sourceActorId, data);
        }
    }
}
