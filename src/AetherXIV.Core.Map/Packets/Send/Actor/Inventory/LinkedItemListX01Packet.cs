using System;
using System.IO;

using AetherXIV.Core.Common;
using AetherXIV.Core.Map.dataobjects;

namespace AetherXIV.Core.Map.packets.send.actor.inventory
{
    class LinkedItemListX01Packet
    {
        public const ushort OPCODE = 0x014D;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint playerActorID, ushort position, InventoryItem linkedItem)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    binWriter.Write((UInt16)position);
                    binWriter.Write((UInt16)linkedItem.slot);
                    binWriter.Write((UInt16)linkedItem.itemPackage);
                }
            }

            return new SubPacket(OPCODE, playerActorID, data);
        }        
    }
}
