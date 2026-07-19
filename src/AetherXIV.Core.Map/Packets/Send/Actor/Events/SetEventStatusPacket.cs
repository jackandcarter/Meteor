using System;
using System.IO;
using System.Text;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor.events
{
    class SetEventStatusPacket
    {
        public const ushort OPCODE = 0x0136;
        public const uint PACKET_SIZE = 0x48;

        public static SubPacket BuildPacket(uint sourceActorId, bool enabled, byte type, string conditionName)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    EventConditionDiagnostics.TraceStatus(sourceActorId, enabled, type, conditionName);
                    binWriter.Write((UInt32)(enabled ? 1 : 0));
                    binWriter.Write((Byte)type);
                    binWriter.Write(Encoding.ASCII.GetBytes(conditionName), 0, Encoding.ASCII.GetByteCount(conditionName) >= 0x24 ? 0x24 : Encoding.ASCII.GetByteCount(conditionName));
                }
            }

            return new SubPacket(OPCODE, sourceActorId, data);
        }
    }
}
