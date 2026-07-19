using System;
using System.IO;

using AetherXIV.Core.Common;
using AetherXIV.Core.World.DataObjects;

namespace AetherXIV.Core.World.Packets.WorldPackets.Send
{
    class ErrorPacket
    {
        public const ushort OPCODE = 0x100A;
        public const uint PACKET_SIZE = 0x24;

        public static SubPacket BuildPacket(Session session, uint errorCode)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    try
                    {
                        binWriter.Write((UInt32)errorCode);
                    }
                    catch (Exception)
                    { }
                }
            }

            return new SubPacket(true, OPCODE, session.sessionId, data);
        }
    }
}
