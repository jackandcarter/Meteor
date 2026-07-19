using System;

namespace AetherXIV.Core.Map.lua
{
    class LuaParam
    {
        public int typeID;
        public Object value;

        public LuaParam(int type, Object value)
        {
            this.typeID = type;
            this.value = value;
        }
    }
}
