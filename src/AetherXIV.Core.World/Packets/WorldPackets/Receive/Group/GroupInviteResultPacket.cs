using System;
using System.IO;

namespace AetherXIV.Core.World.Packets.WorldPackets.Receive.Group
{
    class GroupInviteResultPacket
    {
        public bool invalidPacket = false;

        public uint groupType;
        public uint result;

        public GroupInviteResultPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try
                    {
                        groupType = binReader.ReadUInt32();
                        result = binReader.ReadUInt32();
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
