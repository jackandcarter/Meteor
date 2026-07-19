using AetherXIV.Core.Common;
using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.send.player
{
    class SetPlayerDreamPacket
    {
        public const ushort OPCODE = 0x01A7;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId, byte dreamID, byte innID)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    binWriter.Write((Byte)dreamID);
                    binWriter.Write((Byte)innID);
                }
            }

            return new SubPacket(OPCODE, sourceActorId, data);
        }
    }
}
