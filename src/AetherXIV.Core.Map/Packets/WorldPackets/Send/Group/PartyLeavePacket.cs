using AetherXIV.Core.Common;
using AetherXIV.Core.Map.dataobjects;
using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.WorldPackets.Send.Group
{
    class PartyLeavePacket
    {
        public const ushort OPCODE = 0x1021;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(Session session, bool isDisband)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    binWriter.Write((UInt16)(isDisband ? 1 : 0));
                }
            }
            return new SubPacket(true, OPCODE, session.id, data);
        }

    }
}
