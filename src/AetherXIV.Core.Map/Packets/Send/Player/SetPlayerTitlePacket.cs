using System;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.player
{
    class SetPlayerTitlePacket
    {
        public const ushort OPCODE = 0x019D;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId, uint titleID)
        {
            return new SubPacket(OPCODE, sourceActorId, BitConverter.GetBytes((UInt64)titleID));
        }
    }
}
