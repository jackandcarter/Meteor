using AetherXIV.Core.Map.actors.director;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Common;
using System;
using System.Linq;
using AetherXIV.Core.Map.actors.chara.npc;
using AetherXIV.Core.Map.actors.group;

namespace AetherXIV.Core.Map.actors.area
{

    class PrivateAreaContent : PrivateArea
    {
        private Director currentDirector;
        private bool isContentFinished = false;
        private bool battleCompletionSignaled = false;

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
            RemoveTutorialAlliesFromParty();
            isContentFinished = true;
            DevDiagnostics.Trace(
                "content.area.finished",
                "zone", zoneName,
                "privateArea", GetPrivateAreaName(),
                "privateAreaType", GetPrivateAreaType());
        }

        private void RemoveTutorialAlliesFromParty()
        {
            if (currentDirector == null || !GridaniaOpeningTutorialPolicy.IsContentArea(GetPrivateAreaName()))
                return;

            uint[] allyIds = currentDirector.GetMembers()
                .OfType<Ally>()
                .Select(ally => ally.actorId)
                .ToArray();
            if (allyIds.Length == 0)
                return;

            foreach (Player player in currentDirector.GetPlayerMembers().OfType<Player>())
            {
                if (player.currentParty is Party party)
                    party.RemoveTransientMembers(allyIds);
            }
        }

        /// <summary>
        /// Completes the Gridania opening battle only after the content area's
        /// three hostile wolves are dead. The signal is scoped to the owning
        /// player so an unrelated death elsewhere cannot advance this director.
        /// </summary>
        public void NotifyBattleNpcDefeated(BattleNpc defeated)
        {
            if (battleCompletionSignaled ||
                defeated == null ||
                !GridaniaOpeningTutorialPolicy.IsContentArea(GetPrivateAreaName()) ||
                defeated is Ally)
            {
                return;
            }

            bool hasLivingHostiles = GetMonsters().Any(monster => !(monster is Ally) && monster.IsAlive());
            if (hasLivingHostiles)
                return;

            battleCompletionSignaled = true;
            foreach (Player player in currentDirector.GetPlayerMembers().OfType<Player>())
            {
                string signal = GridaniaOpeningTutorialPolicy.BuildBattleCompleteSignal(player.actorId);
                DevDiagnostics.Trace(
                    "tutorial.gridania.battleComplete",
                    "player", player.customDisplayName,
                    "actor", String.Format("0x{0:X}", player.actorId),
                    "privateArea", GetPrivateAreaName(),
                    "signal", signal);
                LuaEngine.GetInstance().OnSignal(signal);
            }
        }

        public bool IsContentBattleComplete()
        {
            return battleCompletionSignaled;
        }

        public string GetBattleCompleteSignal(Player player)
        {
            return GridaniaOpeningTutorialPolicy.BuildBattleCompleteSignal(player.actorId);
        }

        public string GetPlayerSignal(Player player, string signal)
        {
            return GridaniaOpeningTutorialPolicy.BuildPlayerSignal(signal, player.actorId);
        }

        /// <summary>
        /// Resolves the player who owns a Gridania tutorial wolf kill. Yda and
        /// Papalymo are transient NPC party members, so their final blows still
        /// need to credit the sole player in this director.
        /// </summary>
        public Player GetTutorialRewardPlayer(BattleNpc defeated, Character killer)
        {
            if (defeated == null ||
                killer == null ||
                currentDirector == null ||
                !GridaniaOpeningTutorialPolicy.IsTutorialWolf(GetPrivateAreaName(), defeated.GetBattleNpcId()))
            {
                return null;
            }

            var members = currentDirector.GetMembers();
            if (!members.Contains(defeated) || !members.Contains(killer))
                return null;

            Player[] players = currentDirector.GetPlayerMembers().OfType<Player>().ToArray();
            return players.Length == 1 ? players[0] : null;
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
