using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.receive
{
    class PingPacket
    {
        bool invalidPacket = false;

        public uint time;

        public PingPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try{
                        time = binReader.ReadUInt32();
                    }
                    catch (Exception){
                        invalidPacket = true;
                    }
                }
            }
        }
    }
}
