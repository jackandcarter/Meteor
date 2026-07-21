using AetherXIV.Core.Common;
using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.send.actor.battle
{
    /// <summary>
    /// Retail 0x0195 per-enemy enmity/nameplate indicator.
    /// Confirmed against ffxiv_traces/combat_skills.pcapng.
    /// </summary>
    class SetEnmityIndicatorPacket
    {
        public const ushort OPCODE = 0x0195;
        public const uint PACKET_SIZE = 0x28;
        public const uint NO_ENMITY_TARGET = 0xE0000000;

        public static SubPacket BuildPacket(uint battleNpcActorId, uint targetActorId, ushort hateAmount)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];
            using (MemoryStream stream = new MemoryStream(data))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(targetActorId);
                writer.Write(hateAmount);
                writer.Write((ushort)0);
            }

            return new SubPacket(OPCODE, battleNpcActorId, data);
        }
    }
}
