using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.receive
{
    class UpdatePlayerPositionPacket
    {
        bool invalidPacket = false;

        public ulong timestamp;
        public float x, y, z, rot;
        public ushort moveState; //0: Standing, 1: Walking, 2: Running

        public UpdatePlayerPositionPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try{
                        timestamp = binReader.ReadUInt64();
                        x = binReader.ReadSingle();
                        y = binReader.ReadSingle();
                        z = binReader.ReadSingle();
                        rot = binReader.ReadSingle();
                        moveState = binReader.ReadUInt16();
                    }
                    catch (Exception){
                        invalidPacket = true;
                    }
                }
            }
        }

    }
}
