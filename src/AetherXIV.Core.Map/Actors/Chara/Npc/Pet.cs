using AetherXIV.Core.Map.actors.chara.ai;
using AetherXIV.Core.Map.actors.chara.ai.controllers;
using AetherXIV.Core.Map.actors.chara.npc;

namespace AetherXIV.Core.Map.Actors
{
    class Pet : BattleNpc
    {
        public Pet(int actorNumber, ActorClass actorClass, string uniqueId, Area spawnedArea, float posX, float posY, float posZ, float rot,
                    ushort actorState, uint animationId, string customDisplayName)
            : base(actorNumber, actorClass, uniqueId, spawnedArea, posX, posY, posZ, rot, actorState, animationId, customDisplayName)  
        {
            this.aiContainer = new AIContainer(this, new PetController(this), new PathFind(this), new TargetFind(this));            
            this.hateContainer = new HateContainer(this);
        }
    }
}
