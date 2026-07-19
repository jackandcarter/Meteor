using AetherXIV.Core.Map.lua;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.events
{
    class RunEventFunctionPacket
    {
        public const ushort OPCODE = 0x0130;
        public const uint PACKET_SIZE = 0x2B8;

        public static SubPacket BuildPacket(uint triggerActorID, uint ownerActorID, string eventName, byte eventType, string functionName, List<LuaParam> luaParams)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];
            int maxBodySize = data.Length - 0x80;

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    binWriter.Write((UInt32)triggerActorID);
                    binWriter.Write((UInt32)ownerActorID);
                    binWriter.Write((Byte)eventType);
                    Utils.WriteNullTermString(binWriter, eventName);
                    binWriter.Seek(0x29, SeekOrigin.Begin);                
                    Utils.WriteNullTermString(binWriter, functionName);
                    binWriter.Seek(0x49, SeekOrigin.Begin);

                    LuaUtils.WriteLuaParams(binWriter, luaParams);
                }
            }

            return new SubPacket(OPCODE, triggerActorID, data);
        }
    }
}
