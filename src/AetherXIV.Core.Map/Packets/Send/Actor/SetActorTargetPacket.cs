using AetherXIV.Core.Common;
using System;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class SetActorTargetPacket
    {
        public const ushort OPCODE = 0x00DB;
        public const uint PACKET_SIZE = 0x28;
        
        public static SubPacket BuildPacket(uint sourceActorId, uint targetID)
        {            
            return new SubPacket(OPCODE, sourceActorId, BitConverter.GetBytes((ulong)targetID));
        }
    }
}
