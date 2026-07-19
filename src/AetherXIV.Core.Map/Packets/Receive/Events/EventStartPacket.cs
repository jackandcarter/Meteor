using AetherXIV.Core.Common;
using AetherXIV.Core.Map.lua;
using System;
using System.Collections.Generic;
using System.IO;

namespace AetherXIV.Core.Map.packets.receive.events
{
    class EventStartPacket
    {
        public const ushort OPCODE = 0x012D;
        public const uint PACKET_SIZE = 0xD8;

        public bool invalidPacket = false;

        public uint triggerActorID;
        public uint ownerActorID;
        public uint serverCodes;
        public uint unknown;
        public byte eventType;
        public string eventName;
        public List<LuaParam> luaParams;

        public uint errorIndex;
        public uint errorNum;
        public string error = null;
        
        public EventStartPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try{
                        triggerActorID = binReader.ReadUInt32();
                        ownerActorID = binReader.ReadUInt32();
                        serverCodes = binReader.ReadUInt32();
                        unknown = binReader.ReadUInt32();
                        eventType = binReader.ReadByte();
                        /*
                        //Lua Error Dump
                        if (val1 == 0x39800010)
                        {
                            errorIndex = actorID;
                            errorNum = scriptOwnerActorID;
                            error = ASCIIEncoding.ASCII.GetString(binReader.ReadBytes(0x80)).Replace("\0", "");

                            if (errorIndex == 0)
                                Program.Log.Error("LUA ERROR:");                            

                            return;
                        }
                        */
                        eventName = Utils.ReadNullTermString(binReader);

                        if (binReader.PeekChar() == 0x1)
                            luaParams = new List<LuaParam>();
                        else
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
