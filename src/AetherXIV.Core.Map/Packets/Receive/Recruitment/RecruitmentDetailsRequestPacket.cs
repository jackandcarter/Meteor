using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.receive.recruitment
{
    class RecruitmentDetailsRequestPacket
    {
        public bool invalidPacket = false;

        public ulong recruitmentId;

        public RecruitmentDetailsRequestPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try{
                        recruitmentId = binReader.ReadUInt64();                        
                    }
                    catch (Exception){
                        invalidPacket = true;
                    }
                }
            }
        }
    }
}
