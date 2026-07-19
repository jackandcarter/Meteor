using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send
{
    class LogoutPacket
    {
        public const ushort OPCODE = 0x000E;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint playerActorID)
        {
            return new SubPacket(OPCODE, playerActorID, new byte[8]);
        }
    }
}
