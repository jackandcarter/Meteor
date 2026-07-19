using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.actors.chara.ai;
using AetherXIV.Core.Map.actors.chara.ai.controllers;

namespace AetherXIV.Core.Map.actors.chara.npc
{
    class Ally : BattleNpc
    {
        // todo: ally class is probably not necessary
        public Ally(int actorNumber, ActorClass actorClass, string uniqueId, Area spawnedArea, float posX, float posY, float posZ, float rot,
            ushort actorState, uint animationId, string customDisplayName)
            : base(actorNumber, actorClass, uniqueId, spawnedArea, posX, posY, posZ, rot, actorState, animationId, customDisplayName)  
        {
            aiContainer = new AIContainer(this, new AllyController(this), new PathFind(this), new TargetFind(this));
            this.allegiance = CharacterTargetingAllegiance.Player;
            this.isAutoAttackEnabled = true;
            this.isMovingToSpawn = false;
        }
    }
}
