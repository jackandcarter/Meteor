using AetherXIV.Core.Common;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Map.packets.send.actor;
using System.Collections.Generic;

namespace AetherXIV.Core.Map.actors.area
{
    class PrivateArea : Area    
    {
        private Zone parentZone;
        private string privateAreaName;
        private new uint privateAreaType;

        public PrivateArea(Zone parent, uint id, string classPath, string privateAreaName, uint privateAreaType, ushort bgmDay, ushort bgmNight, ushort bgmBattle)
            : base(id, parent.zoneName, parent.regionId, classPath, bgmDay, bgmNight, bgmBattle, parent.isIsolated, parent.isInn, parent.canRideChocobo, parent.canStealth, true)
        {
            this.parentZone = parent;
            this.zoneName = parent.zoneName;
            this.privateAreaName = privateAreaName;
            this.privateAreaType = privateAreaType;
        }

        public string GetPrivateAreaName()
        {
            return privateAreaName;
        }

        public uint GetPrivateAreaType()
        {
            return privateAreaType;
        }

        public Zone GetParentZone()
        {
            return parentZone;
        }

        public override SubPacket CreateScriptBindPacket()
        {
            List<LuaParam> lParams;

            string path = className;

            string realClassName = className.Substring(className.LastIndexOf("/") + 1);

            lParams = LuaUtils.CreateLuaParamList(classPath, false, true, zoneName, privateAreaName, privateAreaType, canRideChocobo ? (byte)1 : (byte)0, canStealth, isInn, false, false, false, false, false, false);
            ActorInstantiatePacket.BuildPacket(actorId, actorName, realClassName, lParams).DebugPrintSubPacket();
            return ActorInstantiatePacket.BuildPacket(actorId, actorName, realClassName, lParams);
        }


        public void AddSpawnLocation(SpawnLocation spawn)
        {
            mSpawnLocations.Add(spawn);
        }

        public void SpawnAllActors()
        {
            foreach (SpawnLocation spawn in mSpawnLocations)
                SpawnActor(spawn);
        }
    }
}
