using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.receive.supportdesk
{
    class GMTicketIssuesRequestPacket
    {
        public bool invalidPacket = false;
        public uint langCode;

        public GMTicketIssuesRequestPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try{
                        langCode = binReader.ReadUInt32();
                    }
                    catch (Exception){
                        invalidPacket = true;
                    }
                }
            }
        }
    }
}
