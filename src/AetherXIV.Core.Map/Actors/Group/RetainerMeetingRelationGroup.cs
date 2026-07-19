using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors.chara.npc;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.dataobjects;
using AetherXIV.Core.Map.packets.send.group;
using AetherXIV.Core.Map.packets.send.groups;
using System.Collections.Generic;

namespace AetherXIV.Core.Map.actors.group
{
    class RetainerMeetingRelationGroup : Group
    {
        Player player;
        Retainer retainer;

        public RetainerMeetingRelationGroup(ulong groupIndex, Player player, Retainer retainer)
            : base(groupIndex)
        {
            this.player = player;
            this.retainer = retainer;
        }

        public override int GetMemberCount()
        {
            return 2;
        }

        public override List<GroupMember> BuildMemberList(uint id)
        {
            List<GroupMember> groupMembers = new List<GroupMember>();

            groupMembers.Add(new GroupMember(player.actorId, -1, 0x83, false, true, player.customDisplayName));
            groupMembers.Add(new GroupMember(retainer.actorId, -1, 0x83, false, true, retainer.customDisplayName));
            
            return groupMembers;
        }

        public override uint GetTypeId()
        {
            return 50003;
        }

        public override void SendInitWorkValues(Session session)
        {
            SynchGroupWorkValuesPacket groupWork = new SynchGroupWorkValuesPacket(groupIndex);
            groupWork.setTarget("/_init");

            SubPacket test = groupWork.buildPacket(session.id);
            test.DebugPrintSubPacket();
            session.QueuePacket(test);
        }

    }
}
