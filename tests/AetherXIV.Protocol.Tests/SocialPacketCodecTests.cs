using System.Text.Json;

namespace AetherXIV.Protocol.Tests;

public sealed class SocialPacketCodecTests
{
    [Fact]
    public void FriendStatusCodecMatchesOfficialOfflineAndOnlineFrames()
    {
        using JsonDocument fixture = LoadFixture("world-friend-status-observed.json");
        JsonElement root = fixture.RootElement;
        Assert.Equal("aetherxiv.trace.friend-status.v1", root.GetProperty("schema").GetString());
        Assert.Equal("TraceConfirmed", root.GetProperty("evidenceStatus").GetString());

        JsonElement requestEvidence = root.GetProperty("request");
        ClientSocialStateRequestPacket request = new ClientSocialStateRequestPacketCodec(PacketOpcode.FriendStatus)
            .Decode(EvidenceSubPacket(requestEvidence, PacketOpcode.FriendStatus));
        Assert.Equal(0u, request.PageIndex);
        Assert.Equal(0x0018EA7Eu, request.RequestToken);

        foreach (string propertyName in new[] { "offlineResponse", "onlineResponse" })
        {
            JsonElement evidence = root.GetProperty(propertyName);
            FriendStatusEntry entry = new(
                evidence.GetProperty("firstEntryCharacterId").GetUInt64(),
                evidence.GetProperty("firstEntryOnline").GetBoolean());
            SubPacket encoded = new FriendStatusPacketCodec().Encode(
                0x029B2941,
                new FriendStatusPacket(evidence.GetProperty("pageIndex").GetUInt32(), [entry]));
            byte[] expectedPrefix = Convert.FromHexString(evidence.GetProperty("payloadPrefixHex").GetString()!);

            Assert.Equal(evidence.GetProperty("payloadLength").GetInt32(), encoded.Payload.Length);
            Assert.Equal(expectedPrefix, encoded.Payload.Span[..expectedPrefix.Length].ToArray());
            Assert.Equal(entry, Assert.Single(new FriendStatusPacketCodec().Decode(encoded).Entries));
            AssertWireLength(encoded, evidence);
        }
    }

    [Fact]
    public void SocialStateCodecsMatchOfficialWorldStreamFixture()
    {
        using JsonDocument fixture = LoadFixture("world-login-social-state-observed.json");
        JsonElement root = fixture.RootElement;
        Assert.Equal("aetherxiv.trace.social-state.v1", root.GetProperty("schema").GetString());
        Assert.Equal("World", root.GetProperty("service").GetString());
        Assert.Equal("TraceConfirmed", root.GetProperty("evidenceStatus").GetString());

        JsonElement blacklistRequestEvidence = Message(root, "client-to-server", "0x01CB");
        ClientSocialStateRequestPacket blacklistRequest = new ClientSocialStateRequestPacketCodec(PacketOpcode.BlacklistState)
            .Decode(EvidenceSubPacket(blacklistRequestEvidence, PacketOpcode.BlacklistState));
        Assert.Equal(0u, blacklistRequest.PageIndex);
        Assert.Equal(0x6E616D6Du, blacklistRequest.RequestToken);

        JsonElement blacklistResponseEvidence = Message(root, "server-to-client", "0x01CB");
        SubPacket blacklist = new BlacklistStatePacketCodec().Encode(
            0x029B2941,
            new BlacklistStatePacket(0, []));
        Assert.Equal(blacklistResponseEvidence.GetProperty("payloadLength").GetInt32(), blacklist.Payload.Length);
        Assert.All(blacklist.Payload.ToArray(), value => Assert.Equal((byte)0, value));
        AssertWireLength(blacklist, blacklistResponseEvidence);

        JsonElement friendResponseEvidence = Message(root, "server-to-client", "0x01CE");
        FriendListEntry officialFriend = new(
            friendResponseEvidence.GetProperty("firstEntryCharacterId").GetUInt64(),
            friendResponseEvidence.GetProperty("firstEntryName").GetString()!);
        SubPacket friendList = new FriendListStatePacketCodec().Encode(
            0x029B2941,
            new FriendListStatePacket(0, [officialFriend]));
        byte[] expectedPrefix = Convert.FromHexString(friendResponseEvidence.GetProperty("payloadPrefixHex").GetString()!);
        Assert.Equal(friendResponseEvidence.GetProperty("payloadLength").GetInt32(), friendList.Payload.Length);
        Assert.Equal(expectedPrefix, friendList.Payload.Span[..expectedPrefix.Length].ToArray());
        Assert.Equal(officialFriend, Assert.Single(new FriendListStatePacketCodec().Decode(friendList).Entries));
        AssertWireLength(friendList, friendResponseEvidence);

        JsonElement ticketRequestEvidence = Message(root, "client-to-server", "0x01D3");
        ClientSocialStateRequestPacket ticketRequest = new ClientSocialStateRequestPacketCodec(PacketOpcode.GmTicketState)
            .Decode(EvidenceSubPacket(ticketRequestEvidence, PacketOpcode.GmTicketState));
        Assert.Equal(0x009D9FF0u, ticketRequest.RequestToken);

        JsonElement ticketResponseEvidence = Message(root, "server-to-client", "0x01D3");
        SubPacket ticket = new GmTicketStatePacketCodec().Encode(0x029B2941, new GmTicketStatePacket(false));
        Assert.Equal(Convert.FromHexString(ticketResponseEvidence.GetProperty("payloadHex").GetString()!), ticket.Payload.ToArray());
        Assert.False(new GmTicketStatePacketCodec().Decode(ticket).HasOpenTicket);
        AssertWireLength(ticket, ticketResponseEvidence);
    }

    private static void AssertWireLength(SubPacket packet, JsonElement evidence)
    {
        WireLegacySubPacket wire = WireLegacySubPacket.FromGame(packet, packet.Header.SourceActorId, gameTimestamp: 1);
        Assert.Equal(evidence.GetProperty("subPacketLength").GetInt32(), new RawLegacySubPacketCodec().Encode(wire).Length);
    }

    private static SubPacket EvidenceSubPacket(JsonElement evidence, PacketOpcode opcode)
    {
        return SubPacket.Create(
            opcode,
            0x029B2941,
            Convert.FromHexString(evidence.GetProperty("payloadHex").GetString()!));
    }

    private static JsonElement Message(JsonElement root, string direction, string opcode)
    {
        return root.GetProperty("messages")
            .EnumerateArray()
            .Single(message =>
                message.GetProperty("direction").GetString() == direction &&
                message.GetProperty("opcodeKey").GetString() == opcode);
    }

    private static JsonDocument LoadFixture(string fixtureFileName)
    {
        string relativePath = Path.Combine(
            "tests",
            "fixtures",
            "trace-evidence",
            fixtureFileName);
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
                return JsonDocument.Parse(File.ReadAllBytes(candidate));

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find fixture {relativePath}.");
    }
}
