using System.Text;

namespace AetherXIV.Protocol;

public enum WorldPartyModifyCommand : ushort
{
    PromoteByName = 0,
    KickByName = 1,
    PromoteByActorId = 2,
    KickByActorId = 3
}

public readonly record struct WorldPartyModifyRequest(
    WorldPartyModifyCommand Command,
    uint ActorId,
    string Name);

public readonly record struct WorldPartyInviteRequest(
    bool UsesActorId,
    uint ActorId,
    string Name);

public readonly record struct WorldGroupInviteResultRequest(uint GroupType, uint Result);

public readonly record struct WorldPartySync(
    ulong GroupId,
    uint LeaderActorId,
    IReadOnlyList<uint> MemberActorIds);

public readonly record struct WorldLinkshellCreateRequest(string Name, ushort CrestId, uint MasterActorId);

public readonly record struct WorldLinkshellModifyRequest(
    string CurrentName,
    ushort ArgumentCode,
    string Name,
    ushort CrestId,
    uint MasterActorId);

public readonly record struct WorldLinkshellInviteRequest(uint ActorId, string LinkshellName);

public readonly record struct WorldLinkshellLeaveRequest(bool IsKick, string MemberName, string LinkshellName);

public readonly record struct WorldLinkshellRankRequest(string MemberName, string LinkshellName, byte Rank);

public readonly record struct WorldGroupMember(
    uint ActorId,
    int LocalizedName,
    uint Unknown2,
    bool Flag1,
    bool IsOnline,
    string Name);

public static class WorldGroupRoutePackets
{
    public const uint PartyGroupType = 0x2711;
    public const uint LinkshellGroupType = 0x2712;
    public const int PartySyncPayloadSize = 0x40;
    public const int ShortGroupRoutePayloadSize = 0x08;
    public const int StandardGroupRoutePayloadSize = 0x28;
    public const int LinkshellModifyPayloadSize = 0x40;
    public const int ExtendedGroupRoutePayloadSize = 0x48;

    public static bool TryDecodePartyModify(
        ReadOnlyMemory<byte> payload,
        out WorldPartyModifyRequest request)
    {
        request = default;
        if (payload.Length < sizeof(ushort))
            return false;

        ReadOnlySpan<byte> span = payload.Span;
        ushort rawCommand = PacketBinary.ReadUInt16LittleEndian(span);
        if (rawCommand > (ushort)WorldPartyModifyCommand.KickByActorId)
            return false;

        WorldPartyModifyCommand command = (WorldPartyModifyCommand)rawCommand;
        if (command >= WorldPartyModifyCommand.PromoteByActorId)
        {
            if (span.Length < sizeof(ushort) + sizeof(uint))
                return false;
            request = new WorldPartyModifyRequest(
                command,
                PacketBinary.ReadUInt32LittleEndian(span[sizeof(ushort)..]),
                String.Empty);
            return true;
        }

        if (span.Length < sizeof(ushort) + 0x20)
            return false;
        request = new WorldPartyModifyRequest(
            command,
            0,
            ReadFixedAscii(span.Slice(sizeof(ushort), 0x20)));
        return true;
    }

    public static bool TryDecodePartyLeave(ReadOnlyMemory<byte> payload, out bool isDisband)
    {
        isDisband = false;
        if (payload.IsEmpty)
            return false;
        isDisband = payload.Span[0] == 1;
        return true;
    }

    public static bool TryDecodePartyInvite(
        ReadOnlyMemory<byte> payload,
        out WorldPartyInviteRequest request)
    {
        request = default;
        if (payload.Length < sizeof(ushort))
            return false;

        ReadOnlySpan<byte> span = payload.Span;
        ushort command = PacketBinary.ReadUInt16LittleEndian(span);
        if (command == 1)
        {
            if (span.Length < sizeof(ushort) + sizeof(uint))
                return false;
            request = new WorldPartyInviteRequest(
                true,
                PacketBinary.ReadUInt32LittleEndian(span[sizeof(ushort)..]),
                String.Empty);
            return true;
        }

        if (command != 0 || span.Length < sizeof(ushort) + 0x20)
            return false;
        request = new WorldPartyInviteRequest(
            false,
            0,
            ReadFixedAscii(span.Slice(sizeof(ushort), 0x20)));
        return true;
    }

    public static bool TryDecodeGroupInviteResult(
        ReadOnlyMemory<byte> payload,
        out WorldGroupInviteResultRequest request)
    {
        request = default;
        if (payload.Length < sizeof(uint) * 2)
            return false;
        request = new WorldGroupInviteResultRequest(
            PacketBinary.ReadUInt32LittleEndian(payload.Span),
            PacketBinary.ReadUInt32LittleEndian(payload.Span[sizeof(uint)..]));
        return true;
    }

    public static byte[] EncodePartySync(WorldPartySync sync)
    {
        ArgumentNullException.ThrowIfNull(sync.MemberActorIds);
        int maximumMembers = (PartySyncPayloadSize - 0x10) / sizeof(uint);
        if (sync.MemberActorIds.Count > maximumMembers)
            throw new ArgumentOutOfRangeException(nameof(sync), $"Party sync supports at most {maximumMembers} members.");

        byte[] payload = new byte[PartySyncPayloadSize];
        PacketBinary.WriteUInt64LittleEndian(payload, sync.GroupId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x08), sync.LeaderActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x0C), checked((uint)sync.MemberActorIds.Count));
        for (int index = 0; index < sync.MemberActorIds.Count; index++)
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x10 + index * sizeof(uint)), sync.MemberActorIds[index]);
        return payload;
    }

    public static bool TryDecodePartySync(ReadOnlyMemory<byte> payload, out WorldPartySync sync)
    {
        sync = default;
        if (payload.Length < PartySyncPayloadSize)
            return false;
        ReadOnlySpan<byte> span = payload.Span;
        uint count = PacketBinary.ReadUInt32LittleEndian(span[0x0C..]);
        int maximumMembers = (PartySyncPayloadSize - 0x10) / sizeof(uint);
        if (count > maximumMembers)
            return false;
        uint[] members = new uint[count];
        for (int index = 0; index < members.Length; index++)
            members[index] = PacketBinary.ReadUInt32LittleEndian(span[(0x10 + index * sizeof(uint))..]);
        sync = new WorldPartySync(
            PacketBinary.ReadUInt64LittleEndian(span),
            PacketBinary.ReadUInt32LittleEndian(span[0x08..]),
            members);
        return true;
    }

    private static string ReadFixedAscii(ReadOnlySpan<byte> bytes)
    {
        int terminator = bytes.IndexOf((byte)0);
        if (terminator >= 0)
            bytes = bytes[..terminator];
        return Encoding.ASCII.GetString(bytes);
    }

    public static bool TryDecodeLinkshellCreate(
        ReadOnlyMemory<byte> payload,
        out WorldLinkshellCreateRequest request)
    {
        request = default;
        if (payload.Length < 0x26)
            return false;
        request = new WorldLinkshellCreateRequest(
            ReadFixedAscii(payload.Span[..0x20]),
            PacketBinary.ReadUInt16LittleEndian(payload.Span[0x20..]),
            PacketBinary.ReadUInt32LittleEndian(payload.Span[0x22..]));
        return true;
    }

    public static bool TryDecodeLinkshellModify(
        ReadOnlyMemory<byte> payload,
        out WorldLinkshellModifyRequest request)
    {
        request = default;
        if (payload.Length < 0x22)
            return false;
        ReadOnlySpan<byte> span = payload.Span;
        string currentName = ReadFixedAscii(span[..0x20]);
        ushort argumentCode = PacketBinary.ReadUInt16LittleEndian(span[0x20..]);
        switch (argumentCode)
        {
            case 0 when span.Length >= 0x42:
                request = new WorldLinkshellModifyRequest(
                    currentName,
                    argumentCode,
                    ReadFixedAscii(span.Slice(0x22, 0x20)),
                    0,
                    0);
                return true;
            case 1 when span.Length >= 0x24:
                request = new WorldLinkshellModifyRequest(
                    currentName,
                    argumentCode,
                    String.Empty,
                    PacketBinary.ReadUInt16LittleEndian(span[0x22..]),
                    0);
                return true;
            case 2 when span.Length >= 0x26:
                request = new WorldLinkshellModifyRequest(
                    currentName,
                    argumentCode,
                    String.Empty,
                    0,
                    PacketBinary.ReadUInt32LittleEndian(span[0x22..]));
                return true;
            default:
                return false;
        }
    }

    public static bool TryDecodeFixedLinkshellName(ReadOnlyMemory<byte> payload, out string name)
    {
        name = String.Empty;
        if (payload.Length < 0x20)
            return false;
        name = ReadFixedAscii(payload.Span[..0x20]);
        return true;
    }

    public static bool TryDecodeLinkshellInvite(
        ReadOnlyMemory<byte> payload,
        out WorldLinkshellInviteRequest request)
    {
        request = default;
        if (payload.Length < 0x24)
            return false;
        request = new WorldLinkshellInviteRequest(
            PacketBinary.ReadUInt32LittleEndian(payload.Span),
            ReadFixedAscii(payload.Span.Slice(0x04, 0x20)));
        return true;
    }

    public static bool TryDecodeLinkshellLeave(
        ReadOnlyMemory<byte> payload,
        out WorldLinkshellLeaveRequest request)
    {
        request = default;
        if (payload.Length < 0x42)
            return false;
        request = new WorldLinkshellLeaveRequest(
            PacketBinary.ReadUInt16LittleEndian(payload.Span) == 1,
            ReadFixedAscii(payload.Span.Slice(0x02, 0x20)),
            ReadFixedAscii(payload.Span.Slice(0x22, 0x20)));
        return true;
    }

    public static bool TryDecodeLinkshellRank(
        ReadOnlyMemory<byte> payload,
        out WorldLinkshellRankRequest request)
    {
        request = default;
        if (payload.Length < 0x41)
            return false;
        request = new WorldLinkshellRankRequest(
            ReadFixedAscii(payload.Span[..0x20]),
            ReadFixedAscii(payload.Span.Slice(0x20, 0x20)),
            payload.Span[0x40]);
        return true;
    }

    public static byte[] EncodeLinkshellResult(int resultCode)
    {
        byte[] payload = new byte[sizeof(int)];
        PacketBinary.WriteUInt32LittleEndian(payload, unchecked((uint)resultCode));
        return payload;
    }

    public static bool TryDecodeLinkshellResult(ReadOnlyMemory<byte> payload, out int resultCode)
    {
        resultCode = 0;
        if (payload.Length < sizeof(int))
            return false;
        resultCode = unchecked((int)PacketBinary.ReadUInt32LittleEndian(payload.Span));
        return true;
    }

    public static byte[] EncodePartyInviteByName(string name)
    {
        byte[] payload = new byte[StandardGroupRoutePayloadSize];
        WriteFixedAscii(payload.AsSpan(0x02, 0x20), name);
        return payload;
    }

    public static byte[] EncodePartyInviteByActorId(uint actorId)
    {
        byte[] payload = new byte[StandardGroupRoutePayloadSize];
        PacketBinary.WriteUInt16LittleEndian(payload, 1);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x02), actorId);
        return payload;
    }

    public static byte[] EncodeGroupInviteResult(uint groupType, uint result)
    {
        byte[] payload = new byte[ShortGroupRoutePayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, groupType);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x04), result);
        return payload;
    }

    public static byte[] EncodeLinkshellCreate(string name, ushort crestId, uint masterActorId)
    {
        byte[] payload = new byte[StandardGroupRoutePayloadSize];
        WriteFixedAscii(payload.AsSpan(0, 0x20), name);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x20), crestId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x22), masterActorId);
        return payload;
    }

    public static byte[] EncodeLinkshellCrestModify(string name, ushort crestId)
    {
        byte[] payload = new byte[LinkshellModifyPayloadSize];
        WriteFixedAscii(payload.AsSpan(0, 0x20), name);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x20), 1);
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(0x22), crestId);
        return payload;
    }

    public static byte[] EncodeFixedLinkshellName(string name)
    {
        byte[] payload = new byte[StandardGroupRoutePayloadSize];
        WriteFixedAscii(payload, name);
        return payload;
    }

    public static byte[] EncodeLinkshellInvite(uint actorId, string name)
    {
        byte[] payload = new byte[StandardGroupRoutePayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, actorId);
        WriteFixedAscii(payload.AsSpan(0x04, 0x20), name);
        return payload;
    }

    public static byte[] EncodeLinkshellLeave(bool isKick, string memberName, string linkshellName)
    {
        byte[] payload = new byte[ExtendedGroupRoutePayloadSize];
        PacketBinary.WriteUInt16LittleEndian(payload, isKick ? (ushort)1 : (ushort)0);
        WriteFixedAscii(payload.AsSpan(0x02, 0x20), memberName);
        WriteFixedAscii(payload.AsSpan(0x22, 0x20), linkshellName);
        return payload;
    }

    public static byte[] EncodeLinkshellRank(string memberName, string linkshellName, byte rank)
    {
        byte[] payload = new byte[ExtendedGroupRoutePayloadSize];
        WriteFixedAscii(payload.AsSpan(0, 0x20), memberName);
        WriteFixedAscii(payload.AsSpan(0x20, 0x20), linkshellName);
        payload[0x40] = rank;
        return payload;
    }

    public static byte[] EncodeLinkshellInviteCancel() => new byte[ShortGroupRoutePayloadSize];

    private static void WriteFixedAscii(Span<byte> destination, string value)
    {
        destination.Clear();
        int length = Math.Min(destination.Length, value.Length);
        Encoding.ASCII.GetBytes(value.AsSpan(0, length), destination);
    }
}

public static class WorldChatPackets
{
    public const int PartyChatPayloadSize = 0x204;
    public const int ServerChatPayloadSize = ServerChatMessagePacketCodec.PayloadSize;
    public const uint PartyMessageType = ChatMessageType.Party;

    public static bool TryDecodePartyChat(ReadOnlyMemory<byte> payload, out uint actorId, out string message)
    {
        actorId = 0;
        message = String.Empty;
        if (payload.Length < PartyChatPayloadSize)
            return false;
        actorId = PacketBinary.ReadUInt32LittleEndian(payload.Span);
        ReadOnlySpan<byte> messageBytes = payload.Span.Slice(sizeof(uint), 0x200);
        int terminator = messageBytes.IndexOf((byte)0);
        if (terminator >= 0)
            messageBytes = messageBytes[..terminator];
        message = Encoding.ASCII.GetString(messageBytes);
        return true;
    }

    public static WireLegacySubPacket BuildChat(
        uint sourceActorId,
        uint recipientActorId,
        uint messageType,
        string sender,
        string message)
    {
        SubPacket packet = new ServerChatMessagePacketCodec().Encode(
            sourceActorId,
            new ServerChatMessagePacket(sender, messageType, message));
        return WireLegacySubPacket.FromGame(
            packet,
            targetActorId: recipientActorId);
    }
}

public static class WorldGroupClientPackets
{
    public const uint PartyGroupType = 10001;
    public const uint InvitationRelationGroupType = 50001;
    public const uint RetainerMeetingRelationGroupType = 50003;
    public const uint RetainerGroupType = 80001;
    public const uint LinkshellGroupType = 20002;
    public const uint SimpleContentGroupType = 30006;
    public const uint GuildleveContentGroupType = 30001;
    private const int MemberWireSize = 0x30;

    public static IReadOnlyList<WireLegacySubPacket> BuildGroupList(
        uint recipientActorId,
        uint locationCode,
        ulong sequenceId,
        ulong groupId,
        uint groupType,
        IReadOnlyList<WorldGroupMember> members,
        int localizedName = -1,
        string name = "")
    {
        ArgumentNullException.ThrowIfNull(members);
        List<WireLegacySubPacket> packets =
        [
            BuildHeader(recipientActorId, locationCode, sequenceId, groupId, groupType, members.Count, localizedName, name),
            BuildMembersBegin(recipientActorId, locationCode, sequenceId, groupId, members.Count)
        ];

        int offset = 0;
        while (offset < members.Count)
        {
            int remaining = members.Count - offset;
            int capacity = remaining >= 64 ? 64 : remaining >= 32 ? 32 : remaining >= 16 ? 16 : 8;
            packets.Add(BuildMemberChunk(recipientActorId, locationCode, sequenceId, members, offset, capacity));
            offset += Math.Min(capacity, remaining);
        }

        packets.Add(BuildMembersEnd(recipientActorId, locationCode, sequenceId, groupId));
        return packets;
    }

    public static IReadOnlyList<WireLegacySubPacket> BuildContentGroupList(
        uint recipientActorId,
        uint locationCode,
        ulong sequenceId,
        ulong groupId,
        IReadOnlyList<uint> memberActorIds,
        uint groupType = SimpleContentGroupType)
    {
        ArgumentNullException.ThrowIfNull(memberActorIds);
        uint[] orderedMembers = memberActorIds
            .Where(actorId => actorId != recipientActorId)
            .Prepend(recipientActorId)
            .Distinct()
            .ToArray();
        List<WireLegacySubPacket> packets =
        [
            BuildHeader(
                recipientActorId,
                locationCode,
                sequenceId,
                groupId,
                groupType,
                orderedMembers.Length,
                localizedName: -1,
                name: String.Empty),
            BuildMembersBegin(recipientActorId, locationCode, sequenceId, groupId, orderedMembers.Length)
        ];

        int offset = 0;
        while (offset < orderedMembers.Length)
        {
            int remaining = orderedMembers.Length - offset;
            int capacity = remaining >= 64 ? 64 : remaining >= 32 ? 32 : remaining >= 16 ? 16 : 8;
            packets.Add(BuildContentMemberChunk(
                recipientActorId,
                locationCode,
                sequenceId,
                orderedMembers,
                offset,
                capacity));
            offset += Math.Min(capacity, remaining);
        }

        packets.Add(BuildMembersEnd(recipientActorId, locationCode, sequenceId, groupId));
        return packets;
    }

    public static WireLegacySubPacket BuildContentGroupInit(
        uint recipientActorId,
        ulong groupId,
        uint directorActorId)
    {
        byte[] payload = new byte[0x90];
        PacketBinary.WriteUInt64LittleEndian(payload, groupId);
        int offset = 0x09;
        payload[offset++] = 8;
        PacketBinary.WriteUInt32LittleEndian(
            payload.AsSpan(offset),
            ActorPropertyHash.LegacyMurmurHash2("contentGroupWork._globalTemp.director"));
        offset += sizeof(uint);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(offset), (ulong)directorActorId << 32);
        offset += sizeof(ulong);
        payload[offset++] = 1;
        PacketBinary.WriteUInt32LittleEndian(
            payload.AsSpan(offset),
            ActorPropertyHash.LegacyMurmurHash2("contentGroupWork.property[0]"));
        offset += sizeof(uint);
        payload[offset++] = 1;
        const string target = "/_init";
        payload[offset++] = checked((byte)(0x82 + target.Length));
        Encoding.ASCII.GetBytes(target).CopyTo(payload.AsSpan(offset));
        payload[0x08] = checked((byte)(offset + target.Length - 0x09));
        return Game(PacketOpcode.GroupWorkValues, recipientActorId, payload);
    }

    public static WireLegacySubPacket BuildPartyInit(uint recipientActorId, ulong groupId, uint leaderActorId)
    {
        ulong owner = ((ulong)leaderActorId << 32) | 0x00B36F92u;
        return BuildGroupWork(
            recipientActorId,
            groupId,
            [(ActorPropertyHash.LegacyMurmurHash2("partyGroupWork._globalTemp.owner"), owner)],
            "/_init");
    }

    public static WireLegacySubPacket BuildPartyLeaderUpdate(uint recipientActorId, ulong groupId, uint leaderActorId)
    {
        ulong owner = ((ulong)leaderActorId << 32) | 0x00B36F92u;
        return BuildGroupWork(
            recipientActorId,
            groupId,
            [(ActorPropertyHash.LegacyMurmurHash2("partyGroupWork._globalTemp.owner"), owner)],
            "partyGroupWork/leader");
    }

    public static WireLegacySubPacket BuildInvitationInit(
        uint recipientActorId,
        ulong groupId,
        uint hostActorId,
        uint variableCommand)
    {
        ulong host = ((ulong)hostActorId << 32) | 0x00C17909u;
        byte[] payload = new byte[0x90];
        PacketBinary.WriteUInt64LittleEndian(payload, groupId);
        int offset = 0x09;
        payload[offset++] = 8;
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(offset), ActorPropertyHash.LegacyMurmurHash2("work._globalTemp.host"));
        offset += sizeof(uint);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(offset), host);
        offset += sizeof(ulong);
        payload[offset++] = 4;
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(offset), ActorPropertyHash.LegacyMurmurHash2("work._globalTemp.variableCommand"));
        offset += sizeof(uint);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(offset), variableCommand);
        offset += sizeof(uint);
        string target = "/_init";
        payload[offset++] = checked((byte)(0x82 + target.Length));
        Encoding.ASCII.GetBytes(target).CopyTo(payload.AsSpan(offset));
        payload[0x08] = checked((byte)(offset + target.Length - 0x09));
        return Game(PacketOpcode.GroupWorkValues, recipientActorId, payload);
    }

    public static WireLegacySubPacket BuildEmptyGroupInit(uint recipientActorId, ulong groupId)
    {
        byte[] payload = new byte[0x90];
        PacketBinary.WriteUInt64LittleEndian(payload, groupId);
        const string target = "/_init";
        int offset = 0x09;
        payload[offset++] = checked((byte)(0x82 + target.Length));
        Encoding.ASCII.GetBytes(target).CopyTo(payload.AsSpan(offset));
        payload[0x08] = checked((byte)(1 + target.Length));
        return Game(PacketOpcode.GroupWorkValues, recipientActorId, payload);
    }

    public static WireLegacySubPacket BuildRetainerInit(
        uint recipientActorId,
        ulong groupId,
        IReadOnlyList<(byte ContentIdOffset, ushort PlaceNameId, byte ConditionCode, byte Level)> retainers)
    {
        ArgumentNullException.ThrowIfNull(retainers);
        byte[] payload = new byte[0x90];
        PacketBinary.WriteUInt64LittleEndian(payload, groupId);
        int offset = 0x09;
        const string target = "/_init";
        for (int index = 0; index < retainers.Count; index++)
        {
            const int retainerPropertyBytes = 6 + 7 + 6 + 6;
            if (offset + retainerPropertyBytes + 1 + target.Length > payload.Length)
                break;

            (byte contentIdOffset, ushort placeNameId, byte conditionCode, byte level) = retainers[index];
            WriteByte($"work._memberSave[{index}].cdIDOffset", contentIdOffset);
            WriteUInt16($"work._memberSave[{index}].placeName", placeNameId);
            WriteByte($"work._memberSave[{index}].conditions", conditionCode);
            WriteByte($"work._memberSave[{index}].level", level);
        }

        if (offset + 1 + target.Length > payload.Length)
            throw new InvalidDataException("Retainer group work values exceed the fixed client payload.");
        payload[offset++] = checked((byte)(0x82 + target.Length));
        Encoding.ASCII.GetBytes(target).CopyTo(payload.AsSpan(offset));
        payload[0x08] = checked((byte)(offset + target.Length - 0x09));
        return Game(PacketOpcode.GroupWorkValues, recipientActorId, payload);

        void WriteByte(string propertyName, byte value)
        {
            EnsureCapacity(6);
            payload[offset++] = 1;
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(offset), ActorPropertyHash.LegacyMurmurHash2(propertyName));
            offset += sizeof(uint);
            payload[offset++] = value;
        }

        void WriteUInt16(string propertyName, ushort value)
        {
            EnsureCapacity(7);
            payload[offset++] = 2;
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(offset), ActorPropertyHash.LegacyMurmurHash2(propertyName));
            offset += sizeof(uint);
            PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(offset), value);
            offset += sizeof(ushort);
        }

        void EnsureCapacity(int bytes)
        {
            if (offset + bytes + 1 + target.Length > payload.Length)
                throw new InvalidDataException("Retainer group work values exceed the fixed client payload.");
        }
    }

    public static WireLegacySubPacket BuildLinkshellInit(
        uint recipientActorId,
        ulong groupId,
        uint masterActorId,
        ushort crestId,
        byte recipientRank,
        IReadOnlyList<byte> memberRanks)
    {
        ArgumentNullException.ThrowIfNull(memberRanks);
        byte[] payload = new byte[0x90];
        PacketBinary.WriteUInt64LittleEndian(payload, groupId);
        int offset = 0x09;
        const string target = "/_init";
        WriteUInt64("work._globalSave.master", masterActorId);
        WriteUInt16("work._globalSave.crestIcon[0]", crestId);
        WriteByte("work._globalSave.rank", recipientRank);
        for (int index = 0; index < memberRanks.Count; index++)
        {
            if (offset + 6 + 1 + target.Length > payload.Length)
                break;
            WriteByte($"work._memberSave[{index}].rank", memberRanks[index]);
        }
        payload[offset++] = checked((byte)(0x82 + target.Length));
        Encoding.ASCII.GetBytes(target).CopyTo(payload.AsSpan(offset));
        payload[0x08] = checked((byte)(offset + target.Length - 0x09));
        return Game(PacketOpcode.GroupWorkValues, recipientActorId, payload);

        void WriteByte(string propertyName, byte value)
        {
            EnsureCapacity(6);
            payload[offset++] = 1;
            WritePropertyId(propertyName);
            payload[offset++] = value;
        }

        void WriteUInt16(string propertyName, ushort value)
        {
            EnsureCapacity(7);
            payload[offset++] = 2;
            WritePropertyId(propertyName);
            PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(offset), value);
            offset += sizeof(ushort);
        }

        void WriteUInt64(string propertyName, ulong value)
        {
            EnsureCapacity(13);
            payload[offset++] = 8;
            WritePropertyId(propertyName);
            PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(offset), value);
            offset += sizeof(ulong);
        }

        void WritePropertyId(string propertyName)
        {
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(offset), ActorPropertyHash.LegacyMurmurHash2(propertyName));
            offset += sizeof(uint);
        }

        void EnsureCapacity(int bytes)
        {
            if (offset + bytes + 1 + target.Length > payload.Length)
                throw new InvalidDataException("Linkshell group work values exceed the fixed client payload.");
        }
    }

    public static WireLegacySubPacket BuildSetActiveLinkshell(uint recipientActorId, ulong groupId)
    {
        byte[] payload = new byte[0x78];
        PacketBinary.WriteUInt64LittleEndian(payload, groupId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x40), LinkshellGroupType);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x60), groupId == 0 ? 0u : 1u);
        return Game(PacketOpcode.SetActiveLinkshell, recipientActorId, payload);
    }

    public static WireLegacySubPacket BuildDelete(uint recipientActorId, ulong groupId)
    {
        byte[] payload = new byte[0x20];
        PacketBinary.WriteUInt64LittleEndian(payload, 3);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x08), groupId);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x18), groupId);
        return Game(PacketOpcode.DeleteGroup, recipientActorId, payload);
    }

    private static WireLegacySubPacket BuildHeader(
        uint recipientActorId,
        uint locationCode,
        ulong sequenceId,
        ulong groupId,
        uint groupType,
        int memberCount,
        int localizedName,
        string name)
    {
        byte[] payload = new byte[0x78];
        PacketBinary.WriteUInt64LittleEndian(payload, locationCode);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x08), sequenceId);
        bool isParty = groupType == PartyGroupType && memberCount > 1;
        bool isContent = groupType is >= 30001 and <= 30018;
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x10), isParty ? 0ul : 3ul);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x18), isParty ? 0ul : groupId);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x28), groupId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x30), groupType);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x40), unchecked((uint)localizedName));
        WriteFixedAscii(payload.AsSpan(0x44, 0x20), name);
        uint marker = isParty ? 0x3F3Eu : isContent ? 0u : 0x6Du;
        for (int offset = 0x64; offset <= 0x70; offset += sizeof(uint))
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(offset), marker);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x74), checked((uint)memberCount));
        return Game(PacketOpcode.GroupHeader, recipientActorId, payload);
    }

    private static WireLegacySubPacket BuildMembersBegin(
        uint recipientActorId,
        uint locationCode,
        ulong sequenceId,
        ulong groupId,
        int memberCount)
    {
        byte[] payload = new byte[0x20];
        PacketBinary.WriteUInt64LittleEndian(payload, locationCode);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x08), sequenceId);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x10), groupId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x18), checked((uint)memberCount));
        return Game(PacketOpcode.GroupMembersBegin, recipientActorId, payload);
    }

    private static WireLegacySubPacket BuildMembersEnd(
        uint recipientActorId,
        uint locationCode,
        ulong sequenceId,
        ulong groupId)
    {
        byte[] payload = new byte[0x18];
        PacketBinary.WriteUInt64LittleEndian(payload, locationCode);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x08), sequenceId);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x10), groupId);
        return Game(PacketOpcode.GroupMembersEnd, recipientActorId, payload);
    }

    private static WireLegacySubPacket BuildMemberChunk(
        uint recipientActorId,
        uint locationCode,
        ulong sequenceId,
        IReadOnlyList<WorldGroupMember> members,
        int offset,
        int capacity)
    {
        int payloadLength = 0x10 + MemberWireSize * capacity + (capacity == 8 ? sizeof(uint) : 0);
        byte[] payload = new byte[payloadLength];
        PacketBinary.WriteUInt64LittleEndian(payload, locationCode);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x08), sequenceId);
        int count = Math.Min(capacity, members.Count - offset);
        for (int index = 0; index < count; index++)
        {
            WorldGroupMember member = members[offset + index];
            Span<byte> entry = payload.AsSpan(0x10 + index * MemberWireSize, MemberWireSize);
            PacketBinary.WriteUInt32LittleEndian(entry, member.ActorId);
            PacketBinary.WriteUInt32LittleEndian(entry[0x04..], unchecked((uint)member.LocalizedName));
            PacketBinary.WriteUInt32LittleEndian(entry[0x08..], member.Unknown2);
            entry[0x0C] = member.Flag1 ? (byte)1 : (byte)0;
            entry[0x0D] = member.IsOnline ? (byte)1 : (byte)0;
            WriteFixedAscii(entry.Slice(0x0E, 0x20), member.Name);
        }

        if (capacity == 8)
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x10 + MemberWireSize * capacity), checked((uint)count));
        PacketOpcode opcode = capacity switch
        {
            64 => PacketOpcode.GroupMembersX64,
            32 => PacketOpcode.GroupMembersX32,
            16 => PacketOpcode.GroupMembersX16,
            _ => PacketOpcode.GroupMembersX08
        };
        return Game(opcode, recipientActorId, payload);
    }

    private static WireLegacySubPacket BuildContentMemberChunk(
        uint recipientActorId,
        uint locationCode,
        ulong sequenceId,
        IReadOnlyList<uint> members,
        int offset,
        int capacity)
    {
        int payloadLength = capacity switch
        {
            64 => 0x310,
            32 => 0x190,
            16 => 0xD0,
            // The legacy emulator declares the X08 packet as 0x1B8 bytes total,
            // but the captured retail packet is 0x98 bytes total: a 0x78-byte
            // payload containing the list header, eight 0x0C-byte entries, the
            // entry count, and alignment padding. Keeping the legacy allocation
            // here produces a malformed 440-byte game subpacket.
            _ => 0x78
        };
        byte[] payload = new byte[payloadLength];
        PacketBinary.WriteUInt64LittleEndian(payload, locationCode);
        PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(0x08), sequenceId);
        int count = Math.Min(capacity, members.Count - offset);
        for (int index = 0; index < count; index++)
        {
            Span<byte> entry = payload.AsSpan(0x10 + index * 0x0C, 0x0C);
            PacketBinary.WriteUInt32LittleEndian(entry, members[offset + index]);
            PacketBinary.WriteUInt32LittleEndian(entry[0x04..], 1001);
            PacketBinary.WriteUInt32LittleEndian(entry[0x08..], 1);
        }

        if (capacity == 8)
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(0x70), checked((uint)count));
        PacketOpcode opcode = capacity switch
        {
            64 => PacketOpcode.ContentMembersX64,
            32 => PacketOpcode.ContentMembersX32,
            16 => PacketOpcode.ContentMembersX16,
            _ => PacketOpcode.ContentMembersX08
        };
        return Game(opcode, recipientActorId, payload);
    }

    private static WireLegacySubPacket BuildGroupWork(
        uint recipientActorId,
        ulong groupId,
        IReadOnlyList<(uint PropertyId, ulong Value)> values,
        string target)
    {
        byte[] payload = new byte[0x90];
        PacketBinary.WriteUInt64LittleEndian(payload, groupId);
        int offset = 0x09;
        foreach ((uint propertyId, ulong value) in values)
        {
            payload[offset++] = 8;
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(offset), propertyId);
            offset += sizeof(uint);
            PacketBinary.WriteUInt64LittleEndian(payload.AsSpan(offset), value);
            offset += sizeof(ulong);
        }
        payload[offset++] = checked((byte)(0x82 + target.Length));
        Encoding.ASCII.GetBytes(target).CopyTo(payload.AsSpan(offset));
        payload[0x08] = checked((byte)(offset + target.Length - 0x09));
        return Game(PacketOpcode.GroupWorkValues, recipientActorId, payload);
    }

    private static WireLegacySubPacket Game(PacketOpcode opcode, uint recipientActorId, byte[] payload)
    {
        return WireLegacySubPacket.FromGame(
            SubPacket.Create(opcode, recipientActorId, payload),
            targetActorId: recipientActorId);
    }

    private static void WriteFixedAscii(Span<byte> destination, string value)
    {
        destination.Clear();
        int length = Math.Min(value.Length, destination.Length);
        Encoding.ASCII.GetBytes(value.AsSpan(0, length), destination);
    }
}
