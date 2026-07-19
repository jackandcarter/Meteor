using System;

namespace AetherXIV.Core.World.DataObjects
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
