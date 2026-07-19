using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.receive
{
    class AchievementProgressRequestPacket
    {
        public bool invalidPacket = false;

        public uint achievementId;
        public uint responseType;

        public AchievementProgressRequestPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try
                    {
                        achievementId = binReader.ReadUInt32();
                        responseType = binReader.ReadUInt32();
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
