using AetherXIV.Core.Common;
using AetherXIV.Core.World.Packets.Send.Subpackets.Groups;

namespace AetherXIV.Core.World.DataObjects.Group
{
    class RetainerMeetingRelationGroup : Relation
    {
        public RetainerMeetingRelationGroup(ulong groupIndex, uint host, uint other, uint command, ulong topicGroup)
            : base(groupIndex, host, other, command, topicGroup)
        {

        }

        public override uint GetTypeId()
        {
            return Group.RetainerMeetingRelationGroup;
        }

        public override void SendInitWorkValues(Session session)
        {
            SynchGroupWorkValuesPacket groupWork = new SynchGroupWorkValuesPacket(groupIndex);
            groupWork.setTarget("/_init");

            SubPacket test = groupWork.buildPacket(session.sessionId);
            test.DebugPrintSubPacket();
            session.clientConnection.QueuePacket(test);
        }

    }
}
