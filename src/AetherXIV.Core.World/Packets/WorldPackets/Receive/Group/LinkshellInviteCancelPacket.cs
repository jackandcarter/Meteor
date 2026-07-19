using System;
using System.IO;

namespace AetherXIV.Core.World.Packets.WorldPackets.Receive.Group
{
    class LinkshellInviteCancelPacket
    {
        public bool invalidPacket = false;

        public string lsName;
        public uint actorId;

        public LinkshellInviteCancelPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try
                    {                        
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
