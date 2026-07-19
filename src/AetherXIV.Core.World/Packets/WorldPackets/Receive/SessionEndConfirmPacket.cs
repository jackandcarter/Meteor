using System;
using System.IO;

namespace AetherXIV.Core.World.Packets.WorldPackets.Receive
{
    class SessionEndConfirmPacket
    {
        public bool invalidPacket = false;
        public uint sessionId;
        public ushort errorCode;
        public uint destinationZone;

        public SessionEndConfirmPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try
                    {
                        sessionId = binReader.ReadUInt32();
                        errorCode = binReader.ReadUInt16();
                        destinationZone = binReader.ReadUInt32();
                    }
                    catch (Exception)
                    {
                        invalidPacket = true;
                    }
                }
            }
        }
    }
}
