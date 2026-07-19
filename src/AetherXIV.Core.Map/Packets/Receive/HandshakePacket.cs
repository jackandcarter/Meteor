using AetherXIV.Core.Common;
using System;
using System.IO;
using System.Text;

namespace AetherXIV.Core.Map.packets.receive
{
    class HandshakePacket
    {
        bool invalidPacket = false;

        public uint actorID;

        public HandshakePacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try{
                        binReader.BaseStream.Seek(4, SeekOrigin.Begin);
                        actorID = UInt32.Parse(Utils.ReadNullTermString(binReader, 10));
                    }
                    catch (Exception){
                        invalidPacket = true;
                    }
                }
            }
        }
    }
}
