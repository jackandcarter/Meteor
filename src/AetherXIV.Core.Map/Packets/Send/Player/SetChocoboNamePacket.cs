using System.Text;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.player
{
    class SetChocoboNamePacket
    {
        public const ushort OPCODE = 0x0198;
        public const uint PACKET_SIZE = 0x40;

        public static SubPacket BuildPacket(uint sourceActorId, string name)
        {
            if (Encoding.Unicode.GetByteCount(name) >= 0x20)
                name = "ERR: Too Long";
            return new SubPacket(OPCODE, sourceActorId, Encoding.ASCII.GetBytes(name));
        }
    }
}
