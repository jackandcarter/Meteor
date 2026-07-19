using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.WorldPackets.Receive
{
    class ErrorPacket
    {
        public uint errorCode;
       
        public bool invalidPacket = false;

        public ErrorPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try
                    {
                        errorCode = binReader.ReadUInt32();                        
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
