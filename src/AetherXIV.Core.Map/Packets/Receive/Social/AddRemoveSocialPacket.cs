using AetherXIV.Core.Common;
using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.receive.social
{
    class AddRemoveSocialPacket
    {
        public bool invalidPacket = false;

        public string name;

        public AddRemoveSocialPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try{
                        name = Utils.ReadNullTermString(binReader);
                    }
                    catch (Exception){
                        invalidPacket = true;
                    }
                }
            }
        }
    }
}
