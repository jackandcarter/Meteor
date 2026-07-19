using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.WorldPackets.Receive
{
    class LinkshellResultPacket
    {
        public int resultCode;

        public bool invalidPacket = false;

        public LinkshellResultPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try
                    {
                        resultCode = binReader.ReadInt32();                       
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
