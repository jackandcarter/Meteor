using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class WorldGroupPacketTests
{
    [Fact]
    public void PartyRouteRequestsDecodeProvenLayouts()
    {
        byte[] byId = new byte[6];
        PacketBinary.WriteUInt16LittleEndian(byId, 3);
        PacketBinary.WriteUInt32LittleEndian(byId.AsSpan(2), 0x029B2941);
        Assert.True(WorldGroupRoutePackets.TryDecodePartyModify(byId, out WorldPartyModifyRequest modify));
        Assert.Equal(WorldPartyModifyCommand.KickByActorId, modify.Command);
        Assert.Equal(0x029B2941u, modify.ActorId);

        byte[] byName = new byte[0x22];
        PacketBinary.WriteUInt16LittleEndian(byName, 0);
        "Ian Five"u8.CopyTo(byName.AsSpan(2));
        Assert.True(WorldGroupRoutePackets.TryDecodePartyInvite(byName, out WorldPartyInviteRequest invite));
        Assert.False(invite.UsesActorId);
        Assert.Equal("Ian Five", invite.Name);

        byte[] result = new byte[8];
        PacketBinary.WriteUInt32LittleEndian(result, WorldGroupRoutePackets.PartyGroupType);
        PacketBinary.WriteUInt32LittleEndian(result.AsSpan(4), 1);
        Assert.True(WorldGroupRoutePackets.TryDecodeGroupInviteResult(result, out WorldGroupInviteResultRequest accepted));
        Assert.Equal(1u, accepted.Result);
    }

    [Fact]
    public void PartySyncRoundTripsFixedServerRoutePayload()
    {
        WorldPartySync expected = new(17, 42, [42, 84, 126]);

        byte[] payload = WorldGroupRoutePackets.EncodePartySync(expected);

        Assert.Equal(0x40, payload.Length);
        Assert.True(WorldGroupRoutePackets.TryDecodePartySync(payload, out WorldPartySync actual));
        Assert.Equal(expected.GroupId, actual.GroupId);
        Assert.Equal(expected.LeaderActorId, actual.LeaderActorId);
        Assert.Equal(expected.MemberActorIds, actual.MemberActorIds);
    }

    [Fact]
    public void GroupListUsesRunningOffsetAcrossMemberChunks()
    {
        WorldGroupMember[] members = Enumerable.Range(1, 17)
            .Select(index => new WorldGroupMember((uint)index, -1, 0, false, true, $"Member {index}"))
            .ToArray();

        IReadOnlyList<WireLegacySubPacket> packets = WorldGroupClientPackets.BuildGroupList(
            recipientActorId: 1,
            locationCode: 209,
            sequenceId: 123456,
            groupId: 9,
            groupType: WorldGroupClientPackets.PartyGroupType,
            members);

        Assert.Equal(
            [PacketOpcode.GroupHeader, PacketOpcode.GroupMembersBegin, PacketOpcode.GroupMembersX16, PacketOpcode.GroupMembersX08, PacketOpcode.GroupMembersEnd],
            packets.Select(packet => packet.Opcode!.Value).ToArray());
        Assert.Equal(1u, packets[0].SourceActorId);
        Assert.Equal(1u, packets[0].TargetActorId);
        Assert.Equal(0ul, PacketBinary.ReadUInt64LittleEndian(packets[0].Payload.Span[0x10..]));
        Assert.Equal(0ul, PacketBinary.ReadUInt64LittleEndian(packets[0].Payload.Span[0x18..]));
        Assert.Equal(9ul, PacketBinary.ReadUInt64LittleEndian(packets[0].Payload.Span[0x28..]));
        Assert.Equal(0x3F3Eu, PacketBinary.ReadUInt32LittleEndian(packets[0].Payload.Span[0x64..]));
        Assert.Equal(17u, PacketBinary.ReadUInt32LittleEndian(packets[3].Payload.Span[0x10..]));
        Assert.Equal(1u, PacketBinary.ReadUInt32LittleEndian(packets[3].Payload.Span[0x190..]));
    }

    [Fact]
    public void ContentGroupUsesCompactMemberPacketsWithRunningOffset()
    {
        uint[] members = Enumerable.Range(1, 17).Select(index => (uint)index).ToArray();

        IReadOnlyList<WireLegacySubPacket> packets = WorldGroupClientPackets.BuildContentGroupList(
            recipientActorId: 1,
            locationCode: 166,
            sequenceId: 123456,
            groupId: 0x3000000000000001,
            members);
        WireLegacySubPacket init = WorldGroupClientPackets.BuildContentGroupInit(
            recipientActorId: 1,
            groupId: 0x3000000000000001,
            directorActorId: 0x65200001);

        Assert.Equal(
            [PacketOpcode.GroupHeader, PacketOpcode.GroupMembersBegin, PacketOpcode.ContentMembersX16, PacketOpcode.ContentMembersX08, PacketOpcode.GroupMembersEnd],
            packets.Select(packet => packet.Opcode!.Value).ToArray());
        Assert.Equal(WorldGroupClientPackets.SimpleContentGroupType,
            PacketBinary.ReadUInt32LittleEndian(packets[0].Payload.Span[0x30..]));
        Assert.Equal(0u, PacketBinary.ReadUInt32LittleEndian(packets[0].Payload.Span[0x64..]));
        Assert.Equal(0u, PacketBinary.ReadUInt32LittleEndian(packets[0].Payload.Span[0x70..]));
        Assert.Equal(1u, PacketBinary.ReadUInt32LittleEndian(packets[2].Payload.Span[0x10..]));
        Assert.Equal(17u, PacketBinary.ReadUInt32LittleEndian(packets[3].Payload.Span[0x10..]));
        Assert.Equal(1u, PacketBinary.ReadUInt32LittleEndian(packets[3].Payload.Span[0x70..]));
        Assert.Equal(0x78, packets[3].Payload.Length);

        WireLegacySubPacket guildleveHeader = WorldGroupClientPackets.BuildContentGroupList(
            1,
            166,
            123456,
            0x3000000000000002,
            members,
            WorldGroupClientPackets.GuildleveContentGroupType)[0];
        Assert.Equal(
            WorldGroupClientPackets.GuildleveContentGroupType,
            PacketBinary.ReadUInt32LittleEndian(guildleveHeader.Payload.Span[0x30..]));

        Assert.Equal(PacketOpcode.GroupWorkValues, init.Opcode);
        Assert.Equal(0x3000000000000001ul, PacketBinary.ReadUInt64LittleEndian(init.Payload.Span));
        Assert.Equal((ulong)0x65200001 << 32, PacketBinary.ReadUInt64LittleEndian(init.Payload.Span[0x0E..]));
    }

    [Theory]
    [InlineData(8, PacketOpcode.ContentMembersX08, 0x78)]
    [InlineData(16, PacketOpcode.ContentMembersX16, 0xD0)]
    [InlineData(32, PacketOpcode.ContentMembersX32, 0x190)]
    [InlineData(64, PacketOpcode.ContentMembersX64, 0x310)]
    public void ContentGroupMemberPacketSizesMatchWireCapacity(
        int memberCount,
        PacketOpcode expectedOpcode,
        int expectedPayloadLength)
    {
        uint[] members = Enumerable.Range(1, memberCount).Select(index => (uint)index).ToArray();

        WireLegacySubPacket memberPacket = Assert.Single(
            WorldGroupClientPackets.BuildContentGroupList(
                recipientActorId: 1,
                locationCode: 166,
                sequenceId: 123456,
                groupId: 0x3000000000000001,
                members),
            packet => packet.Opcode is PacketOpcode.ContentMembersX08
                or PacketOpcode.ContentMembersX16
                or PacketOpcode.ContentMembersX32
                or PacketOpcode.ContentMembersX64);

        Assert.Equal(expectedOpcode, memberPacket.Opcode);
        Assert.Equal(expectedPayloadLength, memberPacket.Payload.Length);
        Assert.Equal(checked((uint)memberCount), PacketBinary.ReadUInt32LittleEndian(
            memberPacket.Payload.Span[(0x10 + (memberCount - 1) * 0x0C)..]));
        if (memberCount == 8)
        {
            Assert.Equal(8u, PacketBinary.ReadUInt32LittleEndian(
                memberPacket.Payload.Span[0x70..]));
        }
    }

    [Fact]
    public void PartyInitEncodesLeaderOwnerAndTraceConfirmedRecipientHeaders()
    {
        WireLegacySubPacket packet = WorldGroupClientPackets.BuildPartyInit(42, 7, 84);

        Assert.Equal(PacketOpcode.GroupWorkValues, packet.Opcode);
        Assert.Equal(42u, packet.SourceActorId);
        Assert.Equal(42u, packet.TargetActorId);
        Assert.Equal(7ul, PacketBinary.ReadUInt64LittleEndian(packet.Payload.Span));
        Assert.Equal(8, packet.Payload.Span[0x09]);
        Assert.Equal(
            ActorPropertyHash.LegacyMurmurHash2("partyGroupWork._globalTemp.owner"),
            PacketBinary.ReadUInt32LittleEndian(packet.Payload.Span[0x0A..]));
        Assert.Equal(
            ((ulong)84 << 32) | 0x00B36F92u,
            PacketBinary.ReadUInt64LittleEndian(packet.Payload.Span[0x0E..]));
    }

    [Fact]
    public void PartyChatDecodesClientPayloadAndBuildsRecipientAddressedServerMessage()
    {
        byte[] request = new byte[WorldChatPackets.PartyChatPayloadSize];
        PacketBinary.WriteUInt32LittleEndian(request, 42);
        "Hello party"u8.CopyTo(request.AsSpan(4));

        Assert.True(WorldChatPackets.TryDecodePartyChat(request, out uint actorId, out string message));
        Assert.Equal(42u, actorId);
        Assert.Equal("Hello party", message);

        WireLegacySubPacket response = WorldChatPackets.BuildChat(
            sourceActorId: 42,
            recipientActorId: 84,
            WorldChatPackets.PartyMessageType,
            "Ian Five",
            message);
        Assert.Equal(PacketOpcode.ClientChatMessage, response.Opcode);
        Assert.Equal(42u, response.SourceActorId);
        Assert.Equal(84u, response.TargetActorId);
        Assert.Equal(WorldChatPackets.ServerChatPayloadSize, response.Payload.Length);
        Assert.Equal(WorldChatPackets.PartyMessageType, PacketBinary.ReadUInt32LittleEndian(response.Payload.Span[0x20..]));
        Assert.Equal("Ian Five"u8.ToArray(), response.Payload.Span[..8].ToArray());
        Assert.Equal("Hello party"u8.ToArray(), response.Payload.Span.Slice(0x24, 11).ToArray());
    }

    [Fact]
    public void LinkshellBootstrapPacketsCarryGroupIdentityRankAndActiveState()
    {
        WireLegacySubPacket init = WorldGroupClientPackets.BuildLinkshellInit(
            recipientActorId: 42,
            groupId: 0x8000000000000003,
            masterActorId: 42,
            crestId: 17,
            recipientRank: 10,
            memberRanks: [10, 4]);
        WireLegacySubPacket active = WorldGroupClientPackets.BuildSetActiveLinkshell(
            recipientActorId: 42,
            groupId: 0x8000000000000003);
        WireLegacySubPacket inactive = WorldGroupClientPackets.BuildSetActiveLinkshell(42, 0);

        Assert.Equal(PacketOpcode.GroupWorkValues, init.Opcode);
        Assert.Equal(0x90, init.Payload.Length);
        Assert.Equal(PacketOpcode.SetActiveLinkshell, active.Opcode);
        Assert.Equal(0x78, active.Payload.Length);
        Assert.Equal(0x8000000000000003ul, PacketBinary.ReadUInt64LittleEndian(active.Payload.Span));
        Assert.Equal(WorldGroupClientPackets.LinkshellGroupType, PacketBinary.ReadUInt32LittleEndian(active.Payload.Span[0x40..]));
        Assert.Equal(1u, PacketBinary.ReadUInt32LittleEndian(active.Payload.Span[0x60..]));
        Assert.Equal(0u, PacketBinary.ReadUInt32LittleEndian(inactive.Payload.Span[0x60..]));
    }

    [Fact]
    public void RetainerBootstrapTruncatesAtFixedWorkPacketCapacity()
    {
        (byte ContentIdOffset, ushort PlaceNameId, byte ConditionCode, byte Level)[] retainers =
            Enumerable.Range(0, 12)
                .Select(index => ((byte)index, (ushort)(100 + index), (byte)1, (byte)(10 + index)))
                .ToArray();

        WireLegacySubPacket packet = WorldGroupClientPackets.BuildRetainerInit(42, 7, retainers);

        Assert.Equal(PacketOpcode.GroupWorkValues, packet.Opcode);
        Assert.Equal(0x90, packet.Payload.Length);
        Assert.True(packet.Payload.Span[0x08] <= 0x87);
        uint fifthLevel = ActorPropertyHash.LegacyMurmurHash2("work._memberSave[4].level");
        uint sixthLevel = ActorPropertyHash.LegacyMurmurHash2("work._memberSave[5].level");
        Assert.Contains(Convert.ToHexString(BitConverter.GetBytes(fifthLevel)), Convert.ToHexString(packet.Payload.Span));
        Assert.DoesNotContain(Convert.ToHexString(BitConverter.GetBytes(sixthLevel)), Convert.ToHexString(packet.Payload.Span));
    }

    [Fact]
    public void EmptyRelationGroupInitPreservesGroupAndInitTarget()
    {
        WireLegacySubPacket packet = WorldGroupClientPackets.BuildEmptyGroupInit(42, 5555);

        Assert.Equal(PacketOpcode.GroupWorkValues, packet.Opcode);
        Assert.Equal(0x90, packet.Payload.Length);
        Assert.Equal(5555ul, PacketBinary.ReadUInt64LittleEndian(packet.Payload.Span));
        Assert.Contains("/_init"u8.ToArray(), packet.Payload.ToArray());
    }

    [Fact]
    public void LinkshellRouteRequestsDecodeWorkingServerLayouts()
    {
        byte[] createPayload = new byte[0x26];
        "Aether"u8.CopyTo(createPayload);
        PacketBinary.WriteUInt16LittleEndian(createPayload.AsSpan(0x20), 17);
        PacketBinary.WriteUInt32LittleEndian(createPayload.AsSpan(0x22), 42);
        Assert.True(WorldGroupRoutePackets.TryDecodeLinkshellCreate(createPayload, out WorldLinkshellCreateRequest create));
        Assert.Equal(new WorldLinkshellCreateRequest("Aether", 17, 42), create);

        byte[] leavePayload = new byte[0x42];
        PacketBinary.WriteUInt16LittleEndian(leavePayload, 1);
        "Member"u8.CopyTo(leavePayload.AsSpan(0x02));
        "Aether"u8.CopyTo(leavePayload.AsSpan(0x22));
        Assert.True(WorldGroupRoutePackets.TryDecodeLinkshellLeave(leavePayload, out WorldLinkshellLeaveRequest leave));
        Assert.True(leave.IsKick);
        Assert.Equal("Member", leave.MemberName);
        Assert.Equal("Aether", leave.LinkshellName);

        byte[] resultPayload = WorldGroupRoutePackets.EncodeLinkshellResult(3);
        Assert.True(WorldGroupRoutePackets.TryDecodeLinkshellResult(resultPayload, out int resultCode));
        Assert.Equal(3, resultCode);
    }

    [Fact]
    public void GroupRouteEncodersPreserveWorkingFixedPayloadSizesAndFields()
    {
        byte[] inviteByName = WorldGroupRoutePackets.EncodePartyInviteByName("Ian Five");
        byte[] inviteByActor = WorldGroupRoutePackets.EncodePartyInviteByActorId(0x029B2941);
        byte[] inviteResult = WorldGroupRoutePackets.EncodeGroupInviteResult(
            WorldGroupRoutePackets.PartyGroupType,
            1);
        byte[] create = WorldGroupRoutePackets.EncodeLinkshellCreate("Aether", 17, 42);
        byte[] crest = WorldGroupRoutePackets.EncodeLinkshellCrestModify("Aether", 18);
        byte[] delete = WorldGroupRoutePackets.EncodeFixedLinkshellName("Aether");
        byte[] invite = WorldGroupRoutePackets.EncodeLinkshellInvite(84, "Aether");
        byte[] leave = WorldGroupRoutePackets.EncodeLinkshellLeave(false, String.Empty, "Aether");
        byte[] kick = WorldGroupRoutePackets.EncodeLinkshellLeave(true, "Member", "Aether");
        byte[] rank = WorldGroupRoutePackets.EncodeLinkshellRank("Member", "Aether", 7);
        byte[] cancel = WorldGroupRoutePackets.EncodeLinkshellInviteCancel();

        Assert.Equal(0x28, inviteByName.Length);
        Assert.Equal(0x28, inviteByActor.Length);
        Assert.Equal(0x08, inviteResult.Length);
        Assert.Equal(0x28, create.Length);
        Assert.Equal(0x40, crest.Length);
        Assert.Equal(0x28, delete.Length);
        Assert.Equal(0x28, invite.Length);
        Assert.Equal(0x48, leave.Length);
        Assert.Equal(0x48, kick.Length);
        Assert.Equal(0x48, rank.Length);
        Assert.Equal(0x08, cancel.Length);

        Assert.True(WorldGroupRoutePackets.TryDecodePartyInvite(inviteByName, out WorldPartyInviteRequest nameRequest));
        Assert.Equal("Ian Five", nameRequest.Name);
        Assert.True(WorldGroupRoutePackets.TryDecodePartyInvite(inviteByActor, out WorldPartyInviteRequest actorRequest));
        Assert.Equal(0x029B2941u, actorRequest.ActorId);
        Assert.True(WorldGroupRoutePackets.TryDecodeLinkshellCreate(create, out WorldLinkshellCreateRequest createRequest));
        Assert.Equal(new WorldLinkshellCreateRequest("Aether", 17, 42), createRequest);
        Assert.True(WorldGroupRoutePackets.TryDecodeLinkshellModify(crest, out WorldLinkshellModifyRequest crestRequest));
        Assert.Equal((ushort)18, crestRequest.CrestId);
        Assert.True(WorldGroupRoutePackets.TryDecodeLinkshellInvite(invite, out WorldLinkshellInviteRequest inviteRequest));
        Assert.Equal(new WorldLinkshellInviteRequest(84, "Aether"), inviteRequest);
        Assert.True(WorldGroupRoutePackets.TryDecodeLinkshellLeave(kick, out WorldLinkshellLeaveRequest kickRequest));
        Assert.Equal(new WorldLinkshellLeaveRequest(true, "Member", "Aether"), kickRequest);
        Assert.True(WorldGroupRoutePackets.TryDecodeLinkshellRank(rank, out WorldLinkshellRankRequest rankRequest));
        Assert.Equal(new WorldLinkshellRankRequest("Member", "Aether", 7), rankRequest);
    }
}
