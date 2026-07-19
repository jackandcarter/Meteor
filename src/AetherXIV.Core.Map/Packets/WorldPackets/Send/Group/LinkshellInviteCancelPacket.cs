using AetherXIV.Core.Common;
using AetherXIV.Core.Map.dataobjects;

namespace AetherXIV.Core.Map.packets.WorldPackets.Send.Group
{
    class LinkshellInviteCancelPacket
    {
        public const ushort OPCODE = 0x1030;
        public const uint PACKET_SIZE = 0x28;       

        public static SubPacket BuildPacket(Session session)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];            
            return new SubPacket(true, OPCODE, session.id, data);
        }      
    }
}
