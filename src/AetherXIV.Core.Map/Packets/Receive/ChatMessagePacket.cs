using AetherXIV.Core.Common;

using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.receive
{
    class ChatMessagePacket
    {
        public float posX;
        public float posY;
        public float posZ;
        public float posRot;

        public uint logType;

        public string message;

        public bool invalidPacket = false;

        public ChatMessagePacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try{
                        binReader.ReadUInt64();
                        posX = binReader.ReadSingle();
                        posY = binReader.ReadSingle();
                        posZ = binReader.ReadSingle();
                        posRot = binReader.ReadSingle();
                        logType = binReader.ReadUInt32();
                        message = Utils.ReadNullTermString(binReader, 0x200);
                    }
                    catch (Exception){
                        invalidPacket = true;
                    }
                }
            }
        }
    }
}
