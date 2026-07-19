using AetherXIV.Core.Map.actors.director;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Common;
using System;

namespace AetherXIV.Core.Map.actors.area
{

    class PrivateAreaContent : PrivateArea
    {
        private Director currentDirector;
        private bool isContentFinished = false;

        public static PrivateAreaContent CreateContentArea(String scriptPath)
        {
            return null;
        }

        public PrivateAreaContent(Zone parent, string classPath, string privateAreaName, uint privateAreaType, Director director, Player contentStarter) //TODO: Make it a list
            : base(parent, parent.actorId, classPath, privateAreaName, privateAreaType, 0, 0, 0)
        {
            currentDirector = director;
            DevDiagnostics.Trace(
                "content.area.create",
                "player", contentStarter == null ? "(none)" : contentStarter.customDisplayName,
                "actor", contentStarter == null ? "0x0" : String.Format("0x{0:X}", contentStarter.actorId),
                "zone", zoneName,
                "privateArea", privateAreaName,
                "privateAreaType", privateAreaType,
                "director", director == null ? "" : director.GetName());
            LuaEngine.GetInstance().CallLuaFunction(contentStarter, this, "onCreate", false, currentDirector);
        }
        
        public Director GetContentDirector()
        {
            return currentDirector;
        }

        public void ContentFinished()
        {
            isContentFinished = true;
            DevDiagnostics.Trace(
                "content.area.finished",
                "zone", zoneName,
                "privateArea", GetPrivateAreaName(),
                "privateAreaType", GetPrivateAreaType());
        }

        public void CheckDestroy()
        {
            lock (mActorList)
            {
                if (isContentFinished)
                {
                    bool noPlayersLeft = true;
                    foreach (Actor a in mActorList.Values)
                    {
                        if (a is Player)
                            noPlayersLeft = false;
                    }
                    if (noPlayersLeft)
                    {
                        DevDiagnostics.Trace(
                            "content.area.destroy",
                            "zone", zoneName,
                            "privateArea", GetPrivateAreaName(),
                            "privateAreaType", GetPrivateAreaType());
                        GetParentZone().DeleteContentArea(this);
                    }
                }
            }
        }

    }
}
