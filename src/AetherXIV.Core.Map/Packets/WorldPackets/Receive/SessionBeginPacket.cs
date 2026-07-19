using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.WorldPackets.Receive
{
    class SessionBeginPacket
    {
        public bool isLogin;
        public bool invalidPacket = false;

        public SessionBeginPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try
                    {
                        isLogin = binReader.ReadByte() != 0;                      
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
