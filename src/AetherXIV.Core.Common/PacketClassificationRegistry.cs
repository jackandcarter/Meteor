using System;

namespace AetherXIV.Core.Common
{
    public static class PacketClassificationRegistry
    {
        public static string Classify(string context, SubPacket subpacket)
        {
            if (subpacket == null)
                return null;

            string normalizedContext = context == null ? String.Empty : context.ToLowerInvariant();

            if (normalizedContext.Contains("world") && subpacket.header.type == 0x08 && subpacket.header.subpacketSize == 0x18)
                return "world.session-heartbeat-candidate";

            if (normalizedContext.Contains("map") && subpacket.header.type == 0x03 && subpacket.gameMessage.opcode == 0x00CE)
                return "map.event-tutorial-ui-state-candidate";

            if (normalizedContext.Contains("map") && subpacket.header.type == 0x03 && subpacket.gameMessage.opcode == 0x0002)
                return "map.login-zone-bootstrap-candidate";

            return null;
        }
    }
}
