using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.receive
{
    class UpdateItemPackagePacket
    {
        public bool invalidPacket = false;
        public uint actorID;
        public uint packageId;

        public UpdateItemPackagePacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try{
                        actorID = binReader.ReadUInt32();
                        packageId = binReader.ReadUInt32(); 
                    }
                    catch (Exception){
                        invalidPacket = true;
                    }
                }
            }
        }
    }
}
