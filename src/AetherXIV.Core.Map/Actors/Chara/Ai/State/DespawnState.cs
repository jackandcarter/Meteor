using System;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.packets.send.actor;

namespace AetherXIV.Core.Map.actors.chara.ai.state
{
    class DespawnState : State
    {
        private DateTime respawnTime;
        public DespawnState(Character owner, uint respawnTimeSeconds) :
            base(owner, null)
        {
            startTime = Program.Tick;
            respawnTime = startTime.AddSeconds(respawnTimeSeconds);
            owner.ChangeState(SetActorStatePacket.MAIN_STATE_DEAD2);
            owner.OnDespawn();
        }

        public override bool Update(DateTime tick)
        {
            if (tick >= respawnTime)
            {
                owner.ResetTempVars();
                owner.Spawn(tick);
                return true;
            }
            return false;
        }
    }
}
