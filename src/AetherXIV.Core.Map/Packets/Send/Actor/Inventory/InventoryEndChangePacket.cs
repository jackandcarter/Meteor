using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor.inventory
{
    class InventoryEndChangePacket
    {
        public const ushort OPCODE = 0x016E;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId)
        {
            return new SubPacket(OPCODE, sourceActorId, new byte[8]);
        }
        
    }
}
