using AetherXIV.Core.Map.Actors;

namespace AetherXIV.Core.Map.actors.chara.ai.state
{
    class ItemState : State
    {
        public ItemState(Player owner, Character target, ushort slot, uint itemId) :
            base(owner, target)
        {
        }
    }
}
