using System;
using System.IO;

namespace AetherXIV.Core.World.Packets.WorldPackets.Receive.Group
{
    class GetGroupPacket
    {
        public bool invalidPacket = false;    
        public ulong groupId;
        
        public GetGroupPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try
                    {
                        groupId = binReader.ReadUInt64();                        
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
