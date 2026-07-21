using AetherXIV.Core.Common;
using System;
using System.Collections.Generic;
using System.IO;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class ServerZoneInstanceBeginPacket
    {
        public const ushort OPCODE = 0x0006;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId) =>
            new SubPacket(OPCODE, sourceActorId, new byte[PACKET_SIZE - 0x20]);
    }

    class ServerZoneInstanceActorsPacket
    {
        public const ushort OPCODE = 0x0008;
        public const uint PACKET_SIZE = 0x50;
        public const int MAXIMUM_ACTORS = 8;

        public static SubPacket BuildPacket(uint sourceActorId, IReadOnlyList<uint> actorIds)
        {
            if (actorIds == null)
                throw new ArgumentNullException(nameof(actorIds));
            if (actorIds.Count < 1 || actorIds.Count > MAXIMUM_ACTORS)
                throw new ArgumentOutOfRangeException(nameof(actorIds), actorIds.Count,
                    "A zone-instance actor packet must contain between one and eight actor IDs.");

            byte[] data = new byte[PACKET_SIZE - 0x20];
            using (MemoryStream stream = new MemoryStream(data))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write((uint)actorIds.Count);
                for (int index = 0; index < actorIds.Count; index++)
                    writer.Write(actorIds[index]);
            }

            return new SubPacket(OPCODE, sourceActorId, data);
        }
    }

    class ServerZoneInstanceEndPacket
    {
        public const ushort OPCODE = 0x0007;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId) =>
            new SubPacket(OPCODE, sourceActorId, new byte[PACKET_SIZE - 0x20]);
    }
}
