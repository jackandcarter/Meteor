using AetherXIV.Core.Common;

using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.packets.send.actor;
using System.Collections.Generic;
using AetherXIV.Core.Map.actors.chara.npc;

namespace AetherXIV.Core.Map.dataobjects
{
    class Session
    {
        public uint id = 0;
        Player playerActor;
        public List<Actor> actorInstanceList = new List<Actor>();
        public uint languageCode = 1;        
        private uint lastPingPacket = Utils.UnixTimeStampUTC();
        private uint sessionEndMarkedAt = 0;

        public bool isUpdatesLocked = true;
        public bool isEnding = false;

        public string errorMessage = "";

        public Session(uint sessionId)
        {
            this.id = sessionId;
            playerActor = new Player(this, sessionId);
        }

        public void QueuePacket(List<SubPacket> packets)
        {
            foreach (SubPacket s in packets)
                QueuePacket(s);
        }

        public void QueuePacket(SubPacket subPacket)
        {
            subPacket.SetTargetId(id);
            Server.GetWorldConnection().QueuePacket(subPacket);
        }

        public Player GetActor()
        {
            return playerActor;
        }

        public void Ping()
        {
            lastPingPacket = Utils.UnixTimeStampUTC();
        }

        public void BeginEnding()
        {
            if (isEnding)
                return;

            isEnding = true;
            isUpdatesLocked = true;
            sessionEndMarkedAt = Utils.UnixTimeStampUTC();
        }

        public bool IsEndingExpired(uint now, uint graceSeconds)
        {
            return isEnding && now - sessionEndMarkedAt >= graceSeconds;
        }

        public bool CheckIfDCing()
        {
            uint currentTime = Utils.UnixTimeStampUTC();
            if (currentTime - lastPingPacket >= 5000) //Show D/C flag
                playerActor.SetDCFlag(true);
            else if (currentTime - lastPingPacket >= 30000) //DCed
                return true;
            else
                playerActor.SetDCFlag(false);
            return false;
        }

        public void UpdatePlayerActorPosition(float x, float y, float z, float rot, ushort moveState)
        {
            if (isUpdatesLocked)
                return;

            if (playerActor.positionX == x && playerActor.positionY == y && playerActor.positionZ == z && playerActor.rotation == rot)
                return;

            playerActor.oldPositionX = playerActor.positionX;
            playerActor.oldPositionY = playerActor.positionY;
            playerActor.oldPositionZ = playerActor.positionZ;
            playerActor.oldRotation = playerActor.rotation;

            playerActor.positionX = x;
            playerActor.positionY = y;
            playerActor.positionZ = z;
            playerActor.rotation = rot;
            playerActor.moveState = moveState;

            if (playerActor.GetZone() != null)
                playerActor.GetZone().UpdateActorPosition(playerActor);

            if (playerActor.zone2 != null && playerActor.zone2 != playerActor.GetZone())
                playerActor.zone2.UpdateActorPosition(playerActor);

            playerActor.QueuePositionUpdate(new Vector3(x,y,z));
        }

        public void UpdateInstance(List<Actor> list, bool force = false)
        {
            if (isUpdatesLocked && !force)
                return;

            List<BasePacket> basePackets = new List<BasePacket>();
            List<SubPacket> RemoveActorSubpackets = new List<SubPacket>();
            List<SubPacket> posUpdateSubpackets = new List<SubPacket>();

            //Remove missing actors
            for (int i = 0; i < actorInstanceList.Count; i++)
            {
                //Retainer Instance
                if (actorInstanceList[i] is Retainer && playerActor.currentSpawnedRetainer == null)
                {
                    QueuePacket(RemoveActorPacket.BuildPacket(actorInstanceList[i].actorId));
                    actorInstanceList.RemoveAt(i);
                }
                else if (!list.Contains(actorInstanceList[i]) && !(actorInstanceList[i] is Retainer))
                {
                    QueuePacket(RemoveActorPacket.BuildPacket(actorInstanceList[i].actorId));
                    actorInstanceList.RemoveAt(i);
                }
            }

            //Retainer Instance
            if (playerActor.currentSpawnedRetainer != null && !playerActor.sentRetainerSpawn)
            {
                Actor actor = playerActor.currentSpawnedRetainer;
                QueuePacket(actor.GetSpawnPackets(playerActor, 1));
                QueuePacket(actor.GetInitPackets());
                QueuePacket(actor.GetSetEventStatusPackets());
                actorInstanceList.Add(actor);
                ((Npc)actor).DoOnActorSpawn(playerActor);
                playerActor.sentRetainerSpawn = true;
            }

            //Add new actors or move
            for (int i = 0; i < list.Count; i++)
            {
                Actor actor = list[i];

                if (actor.actorId == playerActor.actorId)
                    continue;

                if (actorInstanceList.Contains(actor))
                {

                }
                else
                {   
                    QueuePacket(actor.GetSpawnPackets(playerActor, 1));

                    QueuePacket(actor.GetInitPackets());
                    QueuePacket(actor.GetSetEventStatusPackets());
                    actorInstanceList.Add(actor);

                    if (actor is Npc)
                    {
                        ((Npc)actor).DoOnActorSpawn(playerActor);
                    }
                }
            }

        }


        public void ClearInstance()
        {
            actorInstanceList.Clear();
        }


        public void LockUpdates(bool f)
        {
            isUpdatesLocked = f;
        }
    }
}
