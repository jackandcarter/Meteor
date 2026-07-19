using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.dataobjects;

namespace AetherXIV.Core.Map.actors.chara.ai.state
{
    class ItemState : State
    {
        ItemData item;
        new Player owner;
        public ItemState(Player owner, Character target, ushort slot, uint itemId) :
            base(owner, target)
        {
            this.owner = owner;
            this.target = target;
        }
    }
}
