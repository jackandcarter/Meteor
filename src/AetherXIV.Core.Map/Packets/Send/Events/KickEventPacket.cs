using AetherXIV.Core.Map.lua;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.events
{
    class KickEventPacket
    {
        public const ushort OPCODE = 0x012F;
        public const uint PACKET_SIZE = 0x90;

        public static SubPacket BuildPacket(uint triggerActorId, uint ownerActorId, string eventName, byte eventType, List<LuaParam> luaParams)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    binWriter.Write((UInt32)triggerActorId);
                    binWriter.Write((UInt32)ownerActorId);
                    binWriter.Write((Byte)eventType);
                    binWriter.Write((Byte)0x17); //?
                    binWriter.Write((UInt16)0x75DC); //?
                    binWriter.Write((UInt32)0x30400000); //ServerCodes
                    Utils.WriteNullTermString(binWriter, eventName);

                    binWriter.Seek(0x30, SeekOrigin.Begin);

                    LuaUtils.WriteLuaParams(binWriter, luaParams);
                }
            }

            return new SubPacket(OPCODE, triggerActorId, data);
        }
    }

}
