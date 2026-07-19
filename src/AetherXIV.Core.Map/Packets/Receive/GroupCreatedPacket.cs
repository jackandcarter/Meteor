using AetherXIV.Core.Common;
using System;
using System.IO;
using System.Text;

namespace AetherXIV.Core.Map.packets.receive
{
    class GroupCreatedPacket
    {    
        public ulong groupId;
        public string workString;

        public bool invalidPacket = false;

        public GroupCreatedPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try{
                        groupId = binReader.ReadUInt64();
                        workString = Utils.ReadNullTermString(binReader);
                    }
                    catch (Exception){
                        invalidPacket = true;
                    }
                }
            }
        }

    }
}
