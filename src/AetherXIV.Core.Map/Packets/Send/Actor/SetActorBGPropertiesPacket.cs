using System.IO;

using AetherXIV.Core.Common;
using System;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class SetActorBGPropertiesPacket
    {
        public const ushort OPCODE = 0x00D8;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId, uint val1, uint val2)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    binWriter.Write((UInt32)val1);
                    binWriter.Write((UInt32)val2);
                }
            }

            return new SubPacket(OPCODE, sourceActorId, data);
        }
    }
}
