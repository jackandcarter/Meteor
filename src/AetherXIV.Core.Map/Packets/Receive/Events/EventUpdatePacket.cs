using System;
using System.Collections.Generic;
using System.IO;

using AetherXIV.Core.Map.lua;

namespace AetherXIV.Core.Map.packets.receive.events
{
    class EventUpdatePacket
    {
        public const ushort OPCODE = 0x012E;
        public const uint PACKET_SIZE = 0x78;

        public bool invalidPacket = false;

        public uint triggerActorID;
        public uint serverCodes;
        public uint unknown1;
        public uint unknown2;
        public byte eventType;
        public List<LuaParam> luaParams;

        public EventUpdatePacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try{
                        triggerActorID = binReader.ReadUInt32();
                        serverCodes = binReader.ReadUInt32();
                        unknown1 = binReader.ReadUInt32();
                        unknown2 = binReader.ReadUInt32();
                        eventType = binReader.ReadByte();
                        luaParams = LuaUtils.ReadLuaParams(binReader);
                    }
                    catch (Exception){
                        invalidPacket = true;
                    }
                }
            }
        }
    }
}
