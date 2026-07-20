using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors.group;
using System;
using System.IO;
using System.Text;

namespace AetherXIV.Core.Map.packets.send.group
{
    class GroupHeaderPacket
    {
        public const uint TYPEID_RETAINER = 0x13881;
        public const uint TYPEID_PARTY = 0x2711;
        public const uint TYPEID_LINKSHELL = 0x4E22;

        public const ushort OPCODE = 0x017C;
        public const uint PACKET_SIZE = 0x98;

        public static SubPacket buildPacket(uint playerActorID, uint locationCode, ulong sequenceId, Group group)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    //Write list header
                    binWriter.Write((UInt64)locationCode);
                    binWriter.Write((UInt64)sequenceId);

                    uint typeId = group.GetTypeId();
                    bool isPlayerParty = typeId == Group.PlayerPartyGroup && group.GetMemberCount() > 1;
                    bool isContentGroup = typeId >= Group.ContentGroup_GuildleveGroup
                        && typeId <= Group.ContentGroup_SimpleContentGroup24C;
                    ulong clientGroupIndex = group.GetClientGroupIndex();

                    // Retail uses distinct registration envelopes for player
                    // parties and content groups. Other legacy group families
                    // retain the inherited Meteor shape.
                    binWriter.Write(isPlayerParty ? (UInt64)0 : (UInt64)3);
                    binWriter.Write(isPlayerParty ? (UInt64)0 : clientGroupIndex);
                    binWriter.Write((UInt64)0);
                    binWriter.Write(clientGroupIndex);

                    //This seems to change depending on what the list is for
                    binWriter.Write(typeId);
                    binWriter.Seek(0x40, SeekOrigin.Begin);

                    //This is for Linkshell
                    binWriter.Write((UInt32)group.GetGroupLocalizedName());
                    binWriter.Write(Encoding.ASCII.GetBytes(group.GetGroupName()), 0, Encoding.ASCII.GetByteCount(group.GetGroupName()) >= 0x20 ? 0x20 : Encoding.ASCII.GetByteCount(group.GetGroupName()));

                    binWriter.Seek(0x64, SeekOrigin.Begin);

                    uint marker = isPlayerParty ? 0x3F3Eu : isContentGroup ? 0u : 0x6Du;
                    binWriter.Write(marker);
                    binWriter.Write(marker);
                    binWriter.Write(marker);
                    binWriter.Write(marker);

                    binWriter.Write((UInt32)group.GetMemberCount());
                }
            }

            return new SubPacket(OPCODE, playerActorID, data);
        }
    }
}
