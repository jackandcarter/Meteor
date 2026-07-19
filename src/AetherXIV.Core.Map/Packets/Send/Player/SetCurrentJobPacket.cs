using System;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.player
{
    class SetCurrentJobPacket
    {
        public const ushort OPCODE = 0x01A4;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId, uint jobId)
        {
            return new SubPacket(OPCODE, sourceActorId, BitConverter.GetBytes((uint)jobId));
        }
    }
}
