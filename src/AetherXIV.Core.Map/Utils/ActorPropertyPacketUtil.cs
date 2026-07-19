using System.Collections.Generic;
using AetherXIV.Core.Map.packets.send.actor;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.utils
{
    class ActorPropertyPacketUtil
    {
        private Actor forActor;
        private List<SubPacket> subPackets = new List<SubPacket>();
        private SetActorPropetyPacket currentActorPropertyPacket;
        private string currentTarget;

        public ActorPropertyPacketUtil(string firstTarget, Actor forActor)
        {
            currentActorPropertyPacket = new SetActorPropetyPacket(firstTarget);
            this.forActor = forActor;
            this.currentTarget = firstTarget;
        }

        public void AddProperty(string property)
        {
            if (!currentActorPropertyPacket.AddProperty(forActor, property))
            {
                currentActorPropertyPacket.SetIsMore(true);
                currentActorPropertyPacket.AddTarget();
                subPackets.Add(currentActorPropertyPacket.BuildPacket(forActor.actorId));
                currentActorPropertyPacket = new SetActorPropetyPacket(currentTarget);
                currentActorPropertyPacket.AddProperty(forActor, property);
            }
        }

        public void NewTarget(string target)
        {
            currentActorPropertyPacket.AddTarget();
            currentTarget = target;
            currentActorPropertyPacket.SetTarget(target);            
        }

        public List<SubPacket> Done()
        {
            currentActorPropertyPacket.AddTarget();
            currentActorPropertyPacket.SetIsMore(false);
            subPackets.Add(currentActorPropertyPacket.BuildPacket(forActor.actorId));
            return subPackets;
        }

    }
}
