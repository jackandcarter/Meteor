using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors.director;
using AetherXIV.Core.Map.actors.group.Work;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.dataobjects;
using AetherXIV.Core.Map.packets.send.group;
using AetherXIV.Core.Map.packets.send.groups;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AetherXIV.Core.Map.actors.group
{
    class ContentGroup : Group
    {
        public ContentGroupWork contentGroupWork = new ContentGroupWork();
        private Director director;
        private List<uint> members = new List<uint>();
        private bool isStarted = false;
        private bool isDeleting = false;
        private bool isDeleted = false;

        public ContentGroup(ulong groupIndex, Director director, uint[] initialMembers) : base(groupIndex)
        {
            if (director != null)
                members.Add(director.actorId);

            if (initialMembers != null)
            {
                for (int i = 0; i < initialMembers.Length; i++)
                {
                    Session s = Server.GetServer().GetSession(initialMembers[i]);
                    if (s != null)
                        s.GetActor().SetCurrentContentGroup(this);

                    if (!members.Contains(initialMembers[i]))
                        members.Add(initialMembers[i]);
                }
            }

            this.director = director;
            contentGroupWork._globalTemp.director = (ulong)director.actorId << 32;
        }

        public void Start()
        {
            isStarted = true;
            
            SendGroupPacketsAll(members);
        }

        /// <summary>
        /// Marks a content group live after its initial roster has already
        /// been included in the player's zone-in packet stream.
        /// </summary>
        public void StartAfterZoneIn()
        {
            isStarted = true;
        }

        public void AddMember(Actor actor)
        {
            if (actor == null)
                return;
            
            if(!members.Contains(actor.actorId))
                members.Add(actor.actorId);

            if (actor is Character)            
                ((Character)actor).SetCurrentContentGroup(this);

            if (isStarted)
                SendGroupPacketsAll(members);
        }
        
        public void RemoveMember(uint memberId)
        {
            if (isDeleting || isDeleted || !members.Remove(memberId))
                return;

            Actor actor = director == null ? null : director.GetZone().FindActorInArea(memberId);
            if (actor is Character character)
                ClearMemberContentGroup(character);

            if (isStarted)
                SendGroupPacketsAll(members);
            CheckDestroy();
        }

        private void ClearMemberContentGroup(Character character)
        {
            if (character == null || character.currentContentGroup != this)
                return;

            // Players need the current-content work update. Removed NPCs must
            // be cleared silently so teardown does not reference them after
            // their 0x00CB RemoveActor packet.
            if (character is Player)
                character.SetCurrentContentGroup(null);
            else
            {
                character.currentContentGroup = null;
                character.charaWork.currentContentGroup = 0;
            }
        }

        public override List<GroupMember> BuildMemberList(uint id)
        {
            List<GroupMember> groupMembers = new List<GroupMember>();
            groupMembers.Add(new GroupMember(id, -1, 0, false, true, ""));
            foreach (uint charaId in members)
            {
                if (charaId != id)
                    groupMembers.Add(new GroupMember(charaId, -1, 0, false, true, ""));
            }
            return groupMembers;
        }

        public override int GetMemberCount()
        {
            return members.Count;
        }

        public override void SendInitWorkValues(Session session)
        {
            SynchGroupWorkValuesPacket groupWork = new SynchGroupWorkValuesPacket(groupIndex);
            groupWork.addProperty(this, "contentGroupWork._globalTemp.director");
            groupWork.addByte(Utils.MurmurHash2("contentGroupWork.property[0]", 0), 1);
            groupWork.setTarget("/_init");

            SubPacket test = groupWork.buildPacket(session.id);
            test.DebugPrintSubPacket();
            session.QueuePacket(test);
        }

        public override void SendGroupPackets(Session session)
        {
            ulong time = Utils.MilisUnixTimeStampUTC();
            List<GroupMember> members = BuildMemberList(session.id);

            session.QueuePacket(GroupHeaderPacket.buildPacket(session.id, session.GetActor().zoneId, time, this));
            session.QueuePacket(GroupMembersBeginPacket.buildPacket(session.id, session.GetActor().zoneId, time, this));

            int currentIndex = 0;

            while (true)
            {
                if (GetMemberCount() - currentIndex >= 64)
                    session.QueuePacket(ContentMembersX64Packet.buildPacket(session.id, session.GetActor().zoneId, time, members, ref currentIndex));
                else if (GetMemberCount() - currentIndex >= 32)
                    session.QueuePacket(ContentMembersX32Packet.buildPacket(session.id, session.GetActor().zoneId, time, members, ref currentIndex));
                else if (GetMemberCount() - currentIndex >= 16)
                    session.QueuePacket(ContentMembersX16Packet.buildPacket(session.id, session.GetActor().zoneId, time, members, ref currentIndex));
                else if (GetMemberCount() - currentIndex > 0)
                    session.QueuePacket(ContentMembersX08Packet.buildPacket(session.id, session.GetActor().zoneId, time, members, ref currentIndex));
                else
                    break;
            }

            session.QueuePacket(GroupMembersEndPacket.buildPacket(session.id, session.GetActor().zoneId, time, this));
        }

        public override uint GetTypeId()
        {
            return Group.ContentGroup_SimpleContentGroup24B;
        }


        public void SendAll()
        {
            SendGroupPacketsAll(members);            
        }

        public void DeleteGroup()
        {
            if (isDeleting || isDeleted)
                return;

            isDeleting = true;
            DevDiagnostics.Trace(
                "content.group.delete",
                "groupIndex", groupIndex,
                "typeId", GetTypeId(),
                "memberCount", members.Count,
                "director", director == null ? "0x0" : String.Format("0x{0:X}", director.actorId),
                "directorName", director == null ? "" : director.actorName);

            uint[] deletingMembers = members.ToArray();
            SendDeletePackets(deletingMembers);
            foreach (uint memberId in deletingMembers)
            {
                Session session = Server.GetServer().GetSession(memberId);
                if (session != null)
                    ClearMemberContentGroup(session.GetActor());

                Actor actor = director == null ? null : director.GetZone().FindActorInArea(memberId);
                if (actor is Character character)
                    ClearMemberContentGroup(character);
                if (actor is Npc npc)
                {
                    npc.OnDespawn();
                    npc.Despawn();
                }
            }

            members.Clear();
            Server.GetWorldManager().DeleteContentGroup(groupIndex);
            isDeleted = true;
            isDeleting = false;
        }

        public void CheckDestroy()
        {
            if (isDeleting || isDeleted)
                return;

            bool foundSession = false;
            foreach (uint memberId in members)
            {
                Session session = Server.GetServer().GetSession(memberId);
                if (session != null)
                {
                    foundSession = true;
                    break;
                }
            }

            if (!foundSession)
                DeleteGroup();
        }

        public List<uint> GetMembers()
        {
            return members;
        }
    }
}
