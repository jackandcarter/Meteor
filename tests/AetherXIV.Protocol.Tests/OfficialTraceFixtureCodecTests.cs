using System.Text.Json;
using System.Text;
using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class OfficialTraceFixtureCodecTests
{
    [Fact]
    public void WorldGridaniaFixtureDecodesClientMovementFramesWithTraceKeys()
    {
        FixtureFrame frame = LoadWorldFixtureFrames().Single(item => item.FrameIndex == 1);
        byte[] payload = Convert.FromHexString(frame.PayloadHex);

        LegacyPacketFrame decoded = new BasePacketFrameCodec().DecodeLegacy(payload);
        IReadOnlyList<ObservedProtocolKey> keys = ObservedProtocolKeySet.FromLegacyFrame(
            ProtocolService.World,
            PacketDirection.ClientToServer,
            decoded);

        Assert.Equal("client-to-server", frame.Direction);
        Assert.Equal("not-compressed", frame.CompressionState);
        Assert.Equal("0x00CA", frame.OpcodeKey);
        Assert.True(decoded.Header.IsAuthenticated);
        Assert.False(decoded.Header.IsCompressed);
        Assert.Equal((ushort)0, decoded.Header.ConnectionType);
        Assert.Equal(frame.PayloadLength, decoded.Header.PacketSize);
        Assert.Equal(2, decoded.Header.SubPacketCount);
        Assert.Equal(2, decoded.SubPackets.Count);
        Assert.All(decoded.SubPackets, subPacket =>
        {
            Assert.True(subPacket.IsGameMessage);
            Assert.Equal(PacketOpcode.ClientUpdatePosition, subPacket.Opcode);
            Assert.Equal(0x50E0F2CBu, subPacket.GameTimestamp);
            Assert.Equal(32, subPacket.Payload.Length);
        });
        Assert.Equal(
            [
                "World:C2S:type=0x0003:cat=0x0014:sub=0x00CA:len=64",
                "World:C2S:type=0x0003:cat=0x0014:sub=0x00CA:len=64"
            ],
            keys.Select(key => key.ToString()).ToArray());
    }

    [Fact]
    public void WorldGridaniaFixtureDecodesCompressedServerFramesWithTraceKeys()
    {
        FixtureFrame frame = LoadWorldFixtureFrames().Single(item => item.CompressionState == "zlib-body");
        byte[] payload = Convert.FromHexString(frame.PayloadHex);

        LegacyPacketFrame decoded = new BasePacketFrameCodec().DecodeLegacy(payload);
        WireLegacySubPacket subPacket = Assert.Single(decoded.SubPackets);
        ObservedProtocolKey key = Assert.Single(ObservedProtocolKeySet.FromLegacyFrame(
            ProtocolService.World,
            PacketDirection.ServerToClient,
            decoded));

        Assert.Equal("server-to-client", frame.Direction);
        Assert.Equal("0x0001", frame.OpcodeKey);
        Assert.True(decoded.Header.IsAuthenticated);
        Assert.True(decoded.Header.IsCompressed);
        Assert.Equal((ushort)0, decoded.Header.ConnectionType);
        Assert.Equal(frame.PayloadLength, decoded.Header.PacketSize);
        Assert.Equal(1, decoded.Header.SubPacketCount);
        Assert.True(subPacket.IsGameMessage);
        Assert.Equal(PacketOpcode.Pong, subPacket.Opcode);
        Assert.Equal(0xFED2E000u, subPacket.HeaderUnknown);
        Assert.Equal(0x50E0F2CBu, subPacket.GameTimestamp);
        Assert.Equal(32, subPacket.Payload.Length);
        Assert.Equal(
            new MapPongPacket(179026025, 0x14D),
            new MapPongPacketCodec().Decode(subPacket.ToSubPacket()));
        Assert.Equal("World:S2C:type=0x0003:cat=0x0014:sub=0x0001:len=64", key.ToString());
    }

    [Fact]
    public void WorldGridaniaFixtureCarriesPortableEvidenceMetadata()
    {
        IReadOnlyList<FixtureFrame> frames = LoadWorldFixtureFrames();

        Assert.Equal([1, 4, 5], frames.Select(frame => frame.FrameIndex).ToArray());
        Assert.All(frames, frame =>
        {
            Assert.Equal("gridania_to_coerthas.pcapng", frame.CaptureName);
            Assert.Equal("World", frame.Service);
            Assert.Equal("TraceConfirmed", frame.EvidenceStatus);
            Assert.Equal(0, frame.ConnectionType);
            Assert.DoesNotContain("/", frame.CaptureName, StringComparison.Ordinal);
            Assert.DoesNotContain("\\", frame.CaptureName, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void WorldMovementFixtureCapturesCompressedServerMovementFanout()
    {
        FixtureFrame frame = LoadFixtureFrames(
            "world-moving-around-gridania-observed.json",
            "moving_around_gridania.pcapng").Single(item => item.FrameIndex == 12);
        byte[] payload = Convert.FromHexString(frame.PayloadHex);

        LegacyPacketFrame decoded = new BasePacketFrameCodec().DecodeLegacy(payload);

        Assert.Equal("server-to-client", frame.Direction);
        Assert.Equal("zlib-body", frame.CompressionState);
        Assert.Equal("0x00CF", frame.OpcodeKey);
        Assert.Equal(10, decoded.SubPackets.Count);
        Assert.All(decoded.SubPackets, subPacket =>
        {
            Assert.True(subPacket.IsGameMessage);
            Assert.Equal(PacketOpcode.MoveActorToPosition, subPacket.Opcode);
            Assert.Equal(48, subPacket.Payload.Length);
        });
        Assert.Equal(
            Enumerable.Repeat("0x00CF", 10).ToArray(),
            frame.MessageKeys.Select(key => key.Subcode).ToArray());
    }

    [Fact]
    public void WorldMovementFixtureCapturesSpawnVisualSequence()
    {
        FixtureFrame frame = LoadFixtureFrames(
            "world-moving-around-gridania-observed.json",
            "moving_around_gridania.pcapng").Single(item => item.FrameIndex == 37);
        byte[] payload = Convert.FromHexString(frame.PayloadHex);

        LegacyPacketFrame decoded = new BasePacketFrameCodec().DecodeLegacy(payload);

        Assert.Equal("server-to-client", frame.Direction);
        Assert.Equal("zlib-body", frame.CompressionState);
        Assert.Contains(decoded.SubPackets, packet => packet.Opcode == PacketOpcode.SetActorAppearance);
        Assert.Contains(decoded.SubPackets, packet => packet.Opcode == PacketOpcode.SetActorSubState);
        Assert.Contains(decoded.SubPackets, packet => packet.Opcode == PacketOpcode.SetActorStatusAll);
        Assert.Contains(decoded.SubPackets, packet => packet.Opcode == PacketOpcode.SetActorIcon);
        Assert.Contains(decoded.SubPackets, packet => packet.Opcode == PacketOpcode.SetActorIsZoning);
        Assert.Contains(decoded.SubPackets, packet => packet.Opcode == PacketOpcode.ActorInstantiate);
        Assert.Equal(
            [
                "0x00CA",
                "0x012E",
                "0x016B",
                "0x00D0",
                "0x00CE",
                "0x00CF",
                "0x00D6",
                "0x013D",
                "0x0134",
                "0x0144",
                "0x0179",
                "0x0145",
                "0x017B",
                "0x00CC",
                "0x0137"
            ],
            frame.MessageKeys.Select(key => key.Subcode).ToArray());
    }

    [Fact]
    public void WorldSmallTalkFixtureCapturesTargetAcknowledgeAndEventStartInOneClientFrame()
    {
        FixtureFrame frame = LoadFixtureFrames(
            "world-small-talk-louisoix-observed.json",
            "small_talk_louisoix.pcapng").Single(item => item.FrameIndex == 20);
        byte[] payload = Convert.FromHexString(frame.PayloadHex);

        LegacyPacketFrame decoded = new BasePacketFrameCodec().DecodeLegacy(payload);
        WireLegacySubPacket actorAcknowledge = decoded.SubPackets.Single(packet => packet.Opcode == PacketOpcode.ActorInstantiate);
        WireLegacySubPacket eventStart = decoded.SubPackets.Single(packet => packet.Opcode == PacketOpcode.EventStart);
        WireLegacySubPacket target = decoded.SubPackets.Single(packet => packet.Opcode == PacketOpcode.ClientSetTarget);

        Assert.Equal("client-to-server", frame.Direction);
        Assert.Equal("not-compressed", frame.CompressionState);
        Assert.Equal(
            ["0x00CA", "0x00CC", "0x012D", "0x00CD", "0x00CA"],
            frame.MessageKeys.Select(key => key.Subcode).ToArray());
        Assert.Equal(5, decoded.SubPackets.Count);
        Assert.Equal(
            new ClientActorInstantiateAcknowledgePacket(0x46700082, 0),
            new ClientActorInstantiateAcknowledgePacketCodec().Decode(actorAcknowledge.ToSubPacket()));
        Assert.Equal(
            new ClientSetTargetPacket(0x46700082, ClientSetTargetPacket.InvalidActorId),
            new ClientSetTargetPacketCodec().Decode(target.ToSubPacket()));

        EventStartPacket decodedEventStart = new EventStartPacketCodec().Decode(eventStart.ToSubPacket());
        Assert.Equal(0x029B2941u, decodedEventStart.TriggerActorId);
        Assert.Equal(0x46700082u, decodedEventStart.OwnerActorId);
        Assert.Equal("talkDefault", decodedEventStart.EventName);
        Assert.Equal(1, decodedEventStart.EventType);
        Assert.Empty(decodedEventStart.Parameters);
        Assert.Equal(155, decodedEventStart.RawParameterPayload.Length);
    }

    [Fact]
    public void WorldSmallTalkFixtureCapturesRunAndEndEventServerReplies()
    {
        IReadOnlyList<FixtureFrame> frames = LoadFixtureFrames(
            "world-small-talk-louisoix-observed.json",
            "small_talk_louisoix.pcapng");
        FixtureFrame runFrame = frames.Single(item => item.FrameIndex == 46);
        FixtureFrame endFrame = frames.Single(item => item.FrameIndex == 62);

        LegacyPacketFrame runDecoded = new BasePacketFrameCodec().DecodeLegacy(Convert.FromHexString(runFrame.PayloadHex));
        LegacyPacketFrame endDecoded = new BasePacketFrameCodec().DecodeLegacy(Convert.FromHexString(endFrame.PayloadHex));

        Assert.Equal("0x0130", runFrame.OpcodeKey);
        Assert.Equal("0x0131", endFrame.OpcodeKey);
        WireLegacySubPacket runSubPacket = Assert.Single(runDecoded.SubPackets);
        WireLegacySubPacket endSubPacket = Assert.Single(endDecoded.SubPackets);
        Assert.Equal(PacketOpcode.RunEventFunction, runSubPacket.Opcode);
        Assert.Equal(PacketOpcode.EndEvent, endSubPacket.Opcode);

        RunEventFunctionPacket run = new RunEventFunctionPacketCodec().Decode(runSubPacket.ToSubPacket());
        Assert.Equal(0x029B2941u, run.TriggerActorId);
        Assert.Equal(0x46700082u, run.OwnerActorId);
        Assert.Equal(1, run.EventType);
        Assert.Equal("talkDefault", run.EventName);
        Assert.Equal("delegateEvent", run.FunctionName);
        Assert.Equal(
            [
                new LuaParameter(LuaParameterType.ActorId, 0x029B2941u),
                new LuaParameter(LuaParameterType.ActorId, 0xA0F1AFCDu),
                new LuaParameter(LuaParameterType.String, "defaultTalkLouisoix_001"),
                new LuaParameter(LuaParameterType.Null, null),
                new LuaParameter(LuaParameterType.Null, null),
                new LuaParameter(LuaParameterType.Null, null),
                new LuaParameter(LuaParameterType.Null, null)
            ],
            run.Parameters);

        EndEventPacket end = new EndEventPacketCodec().Decode(endSubPacket.ToSubPacket());
        Assert.Equal(0x029B2941u, end.SourcePlayerActorId);
        Assert.Equal(1, end.EventType);
        Assert.Equal("talkDefault", end.EventName);
    }

    [Fact]
    public void WorldSmallTalkFixtureCapturesClientEventUpdateRepliesWithRawTails()
    {
        IReadOnlyList<FixtureFrame> frames = LoadFixtureFrames(
            "world-small-talk-louisoix-observed.json",
            "small_talk_louisoix.pcapng");
        foreach (int frameIndex in new[] { 42, 60 })
        {
            FixtureFrame frame = frames.Single(item => item.FrameIndex == frameIndex);
            LegacyPacketFrame decoded = new BasePacketFrameCodec().DecodeLegacy(Convert.FromHexString(frame.PayloadHex));
            WireLegacySubPacket eventUpdate = decoded.SubPackets.Single(packet => packet.Opcode == PacketOpcode.EventUpdate);
            EventUpdatePacket packet = new EventUpdatePacketCodec().Decode(eventUpdate.ToSubPacket());

            Assert.Equal("client-to-server", frame.Direction);
            Assert.Contains(frame.MessageKeys, key => key.Subcode == "0x012E");
            Assert.Equal(0x029B2941u, packet.TriggerActorId);
            Assert.Equal(71, packet.RawParameterPayload.Length);
        }
    }

    [Fact]
    public void LobbyLoginSelectFixtureDecodesCharacterListAndHandoffFields()
    {
        IReadOnlyList<FixtureFrame> frames = LoadFixtureFrames(
            "lobby-login-select-observed.json",
            "login.pcapng",
            expectedService: "Lobby",
            expectedPort: 54994,
            expectedTool: "tshark+legacy-blowfish");
        FixtureFrame listFrame = frames.Single(item => item.FrameIndex == 888);
        FixtureFrame selectFrame = frames.Single(item => item.FrameIndex == 901);
        FixtureFrame confirmFrame = frames.Single(item => item.FrameIndex == 904);

        LegacyPacketFrame listDecoded = new BasePacketFrameCodec().DecodeLegacy(Convert.FromHexString(listFrame.PayloadHex));
        Assert.Equal(
            [
                PacketOpcode.LobbyWorldList,
                PacketOpcode.LobbyWorldList,
                PacketOpcode.LobbyImportList,
                PacketOpcode.LobbyRetainerList,
                PacketOpcode.LobbyCharacterList
            ],
            listDecoded.SubPackets.Select(packet => packet.Opcode!.Value).ToArray());

        WireLegacySubPacket characterSubPacket = listDecoded.SubPackets.Single(packet => packet.Opcode == PacketOpcode.LobbyCharacterList);
        LobbyCharacterListPacket characterList = new LobbyCharacterListPacketCodec().Decode(characterSubPacket.ToSubPacket());
        LobbyCharacterListEntry character = Assert.Single(characterList.Characters);
        string appearance = Encoding.ASCII.GetString(character.Appearance.Span).TrimEnd('\0');
        byte[] decodedAppearance = Convert.FromBase64String(appearance.Replace('-', '+').Replace('_', '/'));

        Assert.Equal("server-to-client", listFrame.Direction);
        Assert.Equal(["0x0015", "0x0015", "0x0016", "0x0017", "0x000D"], listFrame.MessageKeys.Select(key => key.Subcode).ToArray());
        Assert.Equal(0x00C17909u, character.CharacterId);
        Assert.Equal(244u, character.CurrentZoneId);
        Assert.Equal("Wrenix Wrong", character.Name);
        Assert.Equal("Ragnarok", character.WorldName);
        Assert.StartsWith("wAQAA", appearance, StringComparison.Ordinal);
        Assert.Equal(0xF5, decodedAppearance.Length);
        Assert.Equal(0x000004C0u, PacketBinary.ReadUInt32LittleEndian(decodedAppearance.AsSpan(0x00)));
        Assert.Equal(0x232327EAu, PacketBinary.ReadUInt32LittleEndian(decodedAppearance.AsSpan(0x04)));

        LobbySelectCharacterPacket select = new LobbySelectCharacterPacketCodec().Decode(
            new BasePacketFrameCodec().DecodeLegacy(Convert.FromHexString(selectFrame.PayloadHex))
                .SubPackets
                .Single()
                .ToSubPacket());
        LobbySelectCharacterConfirmPacket confirm = new LobbySelectCharacterConfirmPacketCodec().Decode(
            new BasePacketFrameCodec().DecodeLegacy(Convert.FromHexString(confirmFrame.PayloadHex))
                .SubPackets
                .Single()
                .ToSubPacket());

        Assert.Equal("client-to-server", selectFrame.Direction);
        Assert.Equal(0x00C17909u, select.CharacterId);
        Assert.Equal(0u, select.UnknownId);
        Assert.Equal(select.CharacterId, confirm.CharacterId);
        Assert.Equal(0x029B2941u, confirm.ActorId);
        Assert.Equal(select.Ticket, confirm.Ticket);
        Assert.Equal((ushort)54992, confirm.WorldPort);
        Assert.Equal("202.67.51.120", confirm.WorldHost);
    }

    [Fact]
    public void WorldLoginSelectFixtureCapturesDualHelloBootstrapSequence()
    {
        IReadOnlyList<FixtureFrame> frames = LoadFixtureFrames(
            "world-login-select-observed.json",
            "login.pcapng");
        Assert.Equal([913, 915, 917, 923, 925, 927], frames.Select(frame => frame.FrameIndex).ToArray());

        LegacyPacketFrame firstHello = new BasePacketFrameCodec().DecodeLegacy(
            Convert.FromHexString(frames.Single(item => item.FrameIndex == 913).PayloadHex));
        LegacyPacketFrame secondHello = new BasePacketFrameCodec().DecodeLegacy(
            Convert.FromHexString(frames.Single(item => item.FrameIndex == 923).PayloadHex));
        WireLegacySubPacket firstHelloPacket = Assert.Single(firstHello.SubPackets);
        WireLegacySubPacket secondHelloPacket = Assert.Single(secondHello.SubPackets);
        string firstSessionId = Encoding.ASCII.GetString(firstHelloPacket.Payload.Span.Slice(0x04, 0x0C)).Trim('\0', ' ');
        string secondSessionId = Encoding.ASCII.GetString(secondHelloPacket.Payload.Span.Slice(0x04, 0x0C)).Trim('\0', ' ');

        Assert.Equal((ushort)2, firstHello.Header.ConnectionType);
        Assert.Equal((ushort)1, secondHello.Header.ConnectionType);
        Assert.Equal((ushort)0x0001, firstHelloPacket.Type);
        Assert.Equal((ushort)0x0001, secondHelloPacket.Type);
        Assert.Equal("43723073", firstSessionId);
        Assert.Equal(firstSessionId, secondSessionId);
        Assert.Equal(
            ["0x0007", "0x0002", "0x0007", "0x0002"],
            frames.Where(frame => frame.Direction == "server-to-client")
                .Select(frame => frame.OpcodeKey ?? String.Empty)
                .ToArray());

        LegacyPacketFrame compressedActorSync = new BasePacketFrameCodec().DecodeLegacy(
            Convert.FromHexString(frames.Single(item => item.FrameIndex == 925).PayloadHex));
        LegacyPacketFrame compressedActorSeed = new BasePacketFrameCodec().DecodeLegacy(
            Convert.FromHexString(frames.Single(item => item.FrameIndex == 927).PayloadHex));

        Assert.Equal("zlib-body", frames.Single(item => item.FrameIndex == 925).CompressionState);
        Assert.Equal("zlib-body", frames.Single(item => item.FrameIndex == 927).CompressionState);
        Assert.Equal((ushort)0x0007, Assert.Single(compressedActorSync.SubPackets).Type);
        Assert.Equal((ushort)0x0002, Assert.Single(compressedActorSeed.SubPackets).Type);
    }

    [Fact]
    public void WorldLoginReadyFixtureProvesHandshakeBeforeLanguageReady()
    {
        IReadOnlyList<FixtureFrame> frames = LoadFixtureFrames(
            "world-login-ready-observed.json",
            "login.pcapng");
        Assert.Equal([929, 934, 951, 952], frames.Select(frame => frame.FrameIndex).ToArray());

        LegacyPacketFrame requestFrame = new BasePacketFrameCodec().DecodeLegacy(
            Convert.FromHexString(frames.Single(item => item.FrameIndex == 929).PayloadHex));
        LegacyPacketFrame responseFrame = new BasePacketFrameCodec().DecodeLegacy(
            Convert.FromHexString(frames.Single(item => item.FrameIndex == 951).PayloadHex));
        LegacyPacketFrame languageFrame = new BasePacketFrameCodec().DecodeLegacy(
            Convert.FromHexString(frames.Single(item => item.FrameIndex == 952).PayloadHex));

        WireLegacySubPacket request = Assert.Single(requestFrame.SubPackets);
        WireLegacySubPacket response = Assert.Single(responseFrame.SubPackets);
        WireLegacySubPacket language = Assert.Single(languageFrame.SubPackets);
        Assert.Equal(PacketOpcode.MapLoginHandshake, request.Opcode);
        Assert.Equal(0x18, request.Payload.Length);
        Assert.Equal(PacketOpcode.MapLoginHandshake, response.Opcode);
        Assert.Equal(
            new MapLoginHandshakeResponsePacket(0x029B2941),
            new MapLoginHandshakeResponsePacketCodec().Decode(response.ToSubPacket()));
        Assert.Equal(PacketOpcode.ClientLanguageCode, language.Opcode);
        Assert.Equal(new ClientLanguageCodePacket(1), new ClientLanguageCodePacketCodec().Decode(language.ToSubPacket()));
    }

    [Fact]
    public void WorldZoneTransitionFixtureSeparatesRawConnectionSyncFromGameInstanceSnapshot()
    {
        IReadOnlyList<FixtureFrame> frames = LoadFixtureFrames(
            "world-zone-transition-observed.json",
            "gridania_to_coerthas.pcapng");
        LegacyPacketFrame transitionFrame = new BasePacketFrameCodec().DecodeLegacy(
            Convert.FromHexString(frames.Single(frame => frame.FrameIndex == 3962).PayloadHex));
        LegacyPacketFrame resetFrame = new BasePacketFrameCodec().DecodeLegacy(
            Convert.FromHexString(frames.Single(frame => frame.FrameIndex == 3972).PayloadHex));

        WireLegacySubPacket transition = transitionFrame.SubPackets.Single(
            packet => packet.Opcode == PacketOpcode.ZoneTransitionState);
        Assert.Equal(
            new ZoneTransitionStatePacket(0x0F),
            new ZoneTransitionStatePacketCodec().Decode(transition.ToSubPacket()));
        WireLegacySubPacket reset = Assert.Single(resetFrame.SubPackets);
        Assert.Equal((ushort)0x0007, reset.Type);
        Assert.False(reset.IsGameMessage);

        IReadOnlyList<WireLegacySubPacket> bootstrap = DecodeCompleteTcpSegment(
            frames.Single(frame => frame.FrameIndex == 4025).PayloadHex);
        int lastSpawnIndex = bootstrap.ToList().FindLastIndex(packet =>
            packet.Opcode == PacketOpcode.ActorInstantiate);
        int instanceBeginIndex = bootstrap.ToList().FindIndex(packet =>
            packet.Opcode == PacketOpcode.ServerZoneInstanceBegin);
        int instanceEndIndex = bootstrap.ToList().FindIndex(packet =>
            packet.Opcode == PacketOpcode.ServerZoneInstanceEnd);
        ServerZoneInstanceActorsPacket[] instanceChunks = bootstrap
            .Where(packet => packet.Opcode == PacketOpcode.ServerZoneInstanceActors)
            .Select(packet => new ServerZoneInstanceActorsPacketCodec().Decode(packet.ToSubPacket()))
            .ToArray();

        Assert.True(instanceBeginIndex > lastSpawnIndex);
        Assert.True(instanceEndIndex > instanceBeginIndex);
        Assert.Equal(2, instanceChunks.Length);
        Assert.Equal(9, instanceChunks.Sum(chunk => chunk.ActorIds.Count));
        Assert.Equal(0x029B2941u, instanceChunks[0].ActorIds[0]);
        Assert.All(instanceChunks, chunk => Assert.InRange(chunk.ActorIds.Count, 1, 8));
        Assert.All(
            bootstrap.Where(packet => packet.Opcode == PacketOpcode.ContentMembersX08),
            packet => Assert.Equal(0x78, packet.Payload.Length));
        Assert.Equal([3962, 3972, 4025], frames.Select(frame => frame.FrameIndex).ToArray());
    }

    [Fact]
    public void WorldCutsceneFixtureConfirmsDirectionSpecificStatePacketWithoutServerResponseContract()
    {
        string fixturePath = FindFixturePath(Path.Combine(
            "tests",
            "fixtures",
            "trace-evidence",
            "world-cutscene-state-observed.json"));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(fixturePath));
        JsonElement root = document.RootElement;

        Assert.Equal("aetherxiv.trace.cutscene-state.v1", root.GetProperty("schema").GetString());
        Assert.Equal("cutscene_book.pcapng", root.GetProperty("capture").GetString());
        Assert.Equal("client-to-server", root.GetProperty("direction").GetString());
        ClientCutsceneStatePacket[] packets = root.GetProperty("packets")
            .EnumerateArray()
            .Select(item => new ClientCutsceneStatePacketCodec().Decode(SubPacket.Create(
                PacketOpcode.SetActorPosition,
                item.GetProperty("sourceActorId").GetUInt32(),
                Convert.FromHexString(item.GetProperty("payloadHex").GetString()!))))
            .ToArray();

        Assert.Equal(2, packets.Length);
        Assert.Equal(new ClientCutsceneStatePacket(0, "com0g105", 0x04000101), packets[0]);
        Assert.Equal(new ClientCutsceneStatePacket(2, "com0g105", 0x0018EBD0), packets[1]);
    }

    [Fact]
    public void WorldContentExitFixtureProvesScopedTeardownBeforeDestinationBootstrap()
    {
        IReadOnlyList<FixtureFrame> fixtures = LoadFixtureFrames(
            "world-content-exit-observed.json",
            "war_quest_update2.pcapng");
        IReadOnlyList<WireLegacySubPacket> exitPackets = DecodeTcpSegment(
            fixtures.Single(frame => frame.FrameIndex == 837).PayloadHex);

        int firstGroupDeleteIndex = exitPackets.ToList().FindIndex(packet =>
            packet.Opcode == PacketOpcode.DeleteGroup);
        int firstActorRemoveIndex = exitPackets.ToList().FindIndex(packet =>
            packet.Opcode == PacketOpcode.RemoveActor);
        int transitionIndex = exitPackets.ToList().FindIndex(packet =>
            packet.Opcode == PacketOpcode.ZoneTransitionState);
        int destinationBootstrapIndex = exitPackets.ToList().FindIndex(packet =>
            packet.Opcode == PacketOpcode.SetActorIsZoning);
        int destinationPositionIndex = exitPackets.ToList().FindIndex(transitionIndex + 1, packet =>
            packet.Opcode == PacketOpcode.SetActorPosition);
        int instanceBeginIndex = exitPackets.ToList().FindIndex(packet =>
            packet.Opcode == PacketOpcode.ServerZoneInstanceBegin);
        int instanceActorsIndex = exitPackets.ToList().FindIndex(packet =>
            packet.Opcode == PacketOpcode.ServerZoneInstanceActors);
        int instanceEndIndex = exitPackets.ToList().FindIndex(packet =>
            packet.Opcode == PacketOpcode.ServerZoneInstanceEnd);

        Assert.InRange(firstGroupDeleteIndex, 0, transitionIndex - 1);
        Assert.InRange(firstActorRemoveIndex, 0, transitionIndex - 1);
        Assert.True(destinationBootstrapIndex > transitionIndex);
        Assert.True(destinationPositionIndex > destinationBootstrapIndex);
        Assert.True(instanceBeginIndex > destinationPositionIndex);
        Assert.True(instanceActorsIndex > instanceBeginIndex);
        Assert.True(instanceEndIndex > instanceActorsIndex);
        Assert.Equal(
            0x02,
            new ZoneTransitionStatePacketCodec().Decode(exitPackets[transitionIndex].ToSubPacket()).State);
        Assert.Equal(
            [0x029B2941u],
            new ServerZoneInstanceActorsPacketCodec()
                .Decode(exitPackets[instanceActorsIndex].ToSubPacket()).ActorIds);
        Assert.DoesNotContain(exitPackets, packet => packet.Type == 0x0007);

        IReadOnlyList<WireLegacySubPacket> destinationPackets = DecodeTcpSegment(
            fixtures.Single(frame => frame.FrameIndex == 839).PayloadHex);
        Assert.Contains(destinationPackets, packet => packet.Opcode == PacketOpcode.AddActor);
    }

    [Fact]
    public void WorldSameZoneContentExitFixtureAlsoPositionsPlayerAfterScopedTeardown()
    {
        FixtureFrame fixture = Assert.Single(LoadFixtureFrames(
            "world-content-exit-same-zone-observed.json",
            "party_battle_leve.pcapng"));
        IReadOnlyList<WireLegacySubPacket> packets = DecodeCompleteTcpSegment(fixture.PayloadHex);
        int groupDeleteIndex = packets.ToList().FindIndex(packet => packet.Opcode == PacketOpcode.DeleteGroup);
        int actorRemoveIndex = packets.ToList().FindIndex(packet => packet.Opcode == PacketOpcode.RemoveActor);
        int transitionIndex = packets.ToList().FindIndex(packet => packet.Opcode == PacketOpcode.ZoneTransitionState);
        int positionIndex = packets.ToList().FindIndex(transitionIndex + 1, packet =>
            packet.Opcode == PacketOpcode.SetActorPosition);

        Assert.InRange(groupDeleteIndex, 0, transitionIndex - 1);
        Assert.InRange(actorRemoveIndex, 0, transitionIndex - 1);
        Assert.True(positionIndex > transitionIndex);
        Assert.Equal(
            0x02,
            new ZoneTransitionStatePacketCodec().Decode(packets[transitionIndex].ToSubPacket()).State);
        SetActorPositionPacket position = new SetActorPositionPacketCodec().Decode(
            packets[positionIndex].ToSubPacket());
        Assert.Equal(0u, position.ActorId);
        Assert.Equal((ushort)2, position.SpawnType);
        Assert.False(position.IsZoningPlayer);
    }

    [Fact]
    public void WorldPartyBattleFixtureProvesAcceptedCastAndMovementInterruptionSequence()
    {
        IReadOnlyList<FixtureFrame> fixtures = LoadFixtureFrames(
            "world-party-battle-cast-observed.json",
            "party_battle_leve.pcapng");
        IReadOnlyList<WireLegacySubPacket> accepted = DecodeTcpSegment(
            fixtures.Single(frame => frame.FrameIndex == 5123).PayloadHex);

        int castStateIndex = accepted.ToList().FindIndex(packet =>
            packet.Opcode == PacketOpcode.SetActorProperty
            && packet.SourceActorId == 0x029B2941
            && new SetActorPropertyPacketCodec().Decode(packet.ToSubPacket()).Target == "playerWork/castState");
        int chantIndex = accepted.ToList().FindIndex(castStateIndex + 1, packet =>
            packet.Opcode == PacketOpcode.SetActorSubState
            && packet.SourceActorId == 0x029B2941);
        int startResultIndex = accepted.ToList().FindIndex(chantIndex + 1, packet =>
            packet.Opcode == PacketOpcode.CommandResultX01
            && packet.SourceActorId == 0x029B2941);
        int endEventIndex = accepted.ToList().FindIndex(startResultIndex + 1, packet =>
            packet.Opcode == PacketOpcode.EndEvent
            && packet.SourceActorId == 0x029B2941);

        Assert.True(castStateIndex >= 0);
        Assert.True(castStateIndex < chantIndex && chantIndex < startResultIndex && startResultIndex < endEventIndex);
        SetActorPropertyPacket castState = new SetActorPropertyPacketCodec().Decode(accepted[castStateIndex].ToSubPacket());
        Assert.Equal(
            [0x59C40D5Du, 0xF683A451u],
            castState.Values.Select(value => value.PropertyId).ToArray());
        Assert.Equal(0x50E11DC2u, castState.Values[0].Value);
        Assert.Equal(0x00006AD2u, castState.Values[1].Value);
        SetActorSubStatePacket chant = new SetActorSubStatePacketCodec().Decode(accepted[chantIndex].ToSubPacket());
        Assert.Equal((byte)0x60, chant.ChantId);
        Assert.Equal((byte)0x0C, chant.Waste);
        Assert.Equal((ushort)0, chant.MotionPack);
        CommandResultX01Packet start = new CommandResultX01PacketCodec().Decode(accepted[startResultIndex].ToSubPacket());
        Assert.Equal(0x6F000003u, start.AnimationId);
        Assert.Equal((ushort)0x6AD2, start.CommandId);
        Assert.Equal((ushort)30128, start.Action.WorldMasterTextId);
        Assert.Equal(1u, start.Action.EffectId);

        IReadOnlyList<WireLegacySubPacket> interrupted = DecodeTcpSegment(
            fixtures.Single(frame => frame.FrameIndex == 5127).PayloadHex);
        WireLegacySubPacket clearChantPacket = interrupted.Last(packet =>
            packet.Opcode == PacketOpcode.SetActorSubState
            && packet.SourceActorId == 0x029B2941);
        WireLegacySubPacket interruptedResultPacket = interrupted.Last(packet =>
            packet.Opcode == PacketOpcode.CommandResultX01
            && packet.SourceActorId == 0x029B2941);
        SetActorSubStatePacket clearChant = new SetActorSubStatePacketCodec().Decode(clearChantPacket.ToSubPacket());
        CommandResultX01Packet interrupt = new CommandResultX01PacketCodec().Decode(interruptedResultPacket.ToSubPacket());
        Assert.Equal((byte)0, clearChant.ChantId);
        Assert.Equal(0x7F000002u, interrupt.AnimationId);
        Assert.Equal((ushort)30209, interrupt.Action.WorldMasterTextId);
        Assert.Equal(0u, interrupt.Action.EffectId);

        WireLegacySubPacket clearStatePacket = Assert.Single(DecodeTcpSegment(
            fixtures.Single(frame => frame.FrameIndex == 5129).PayloadHex));
        SetActorPropertyPacket clearState = new SetActorPropertyPacketCodec().Decode(clearStatePacket.ToSubPacket());
        ActorPropertyValue clearCommand = Assert.Single(clearState.Values);
        Assert.Equal(0xF683A451u, clearCommand.PropertyId);
        Assert.Equal(0u, clearCommand.Value);
    }

    [Fact]
    public void WorldCombatAutoAttackFixtureProvesResultShapeAndObservedCadence()
    {
        IReadOnlyList<FixtureFrame> fixtures = LoadFixtureFrames(
            "world-combat-autoattack-observed.json",
            "combat_autoattack.pcapng");
        IReadOnlyList<LegacyPacketFrame> firstFrames = DecodeTcpFrames(
            fixtures.Single(frame => frame.FrameIndex == 65).PayloadHex);
        IReadOnlyList<LegacyPacketFrame> fourthFrames = DecodeTcpFrames(
            fixtures.Single(frame => frame.FrameIndex == 155).PayloadHex);

        LegacyPacketFrame firstFrame = firstFrames.Single(frame => frame.SubPackets.Any(IsObservedPlayerAutoAttack));
        LegacyPacketFrame fourthFrame = fourthFrames.Single(frame => frame.SubPackets.Any(IsObservedPlayerAutoAttack));
        CommandResultX01Packet first = DecodeObservedPlayerAutoAttack(firstFrame);
        CommandResultX01Packet fourth = DecodeObservedPlayerAutoAttack(fourthFrame);

        Assert.Equal(0x19001000u, first.AnimationId);
        Assert.Equal((ushort)0x5658, first.CommandId);
        Assert.Equal((ushort)0x0810, first.LayoutFlags);
        Assert.Equal((ushort)0x765D, first.Action.WorldMasterTextId);
        Assert.Equal(0x08000604u, first.Action.EffectId);
        Assert.Equal((byte)1, first.Action.HitNumber);
        Assert.Equal(first.AnimationId, fourth.AnimationId);
        Assert.Equal(first.CommandId, fourth.CommandId);
        Assert.Equal(first.Action.WorldMasterTextId, fourth.Action.WorldMasterTextId);

        double averageIntervalSeconds = (fourthFrame.Header.Timestamp - firstFrame.Header.Timestamp) / 3_000d;
        Assert.InRange(averageIntervalSeconds, 4.0d, 4.5d);
    }

    [Fact]
    public void WorldActionFixtureProvesPagedClassLevelsAndLevelCaps()
    {
        string path = FindFixturePath(Path.Combine(
            "tests",
            "fixtures",
            "trace-evidence",
            "world-action-class-progression-observed.json"));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        Assert.Equal("aetherxiv.trace.actor-property-sequence.v1", root.GetProperty("schema").GetString());
        Assert.Equal("action_and_traits.pcapng", root.GetProperty("capture").GetString());
        Assert.Equal("World", root.GetProperty("service").GetString());
        Assert.Equal("TraceConfirmed", root.GetProperty("evidenceStatus").GetString());

        SetActorPropertyPacket[] packets = root.GetProperty("packets")
            .EnumerateArray()
            .Select(item =>
            {
                Assert.Equal("0x0137", item.GetProperty("opcodeKey").GetString());
                byte[] payload = Convert.FromHexString(item.GetProperty("payloadHex").GetString()!);
                SubPacket subPacket = SubPacket.Create(
                    PacketOpcode.SetActorProperty,
                    item.GetProperty("sourceActorId").GetUInt32(),
                    payload);
                return new SetActorPropertyPacketCodec().Decode(subPacket);
            })
            .ToArray();

        Assert.Equal(4, packets.Length);
        Assert.All(packets, packet => Assert.Equal("charaWork/exp", packet.Target));
        Assert.All(packets[..3], packet => Assert.True(packet.IsArrayMode));
        Assert.False(packets[3].IsArrayMode);
        Assert.Equal(
            [1, 3, 1, 3],
            packets.Select(packet => ((byte[])Assert.Single(packet.Values).Value)[^1]).ToArray());

        ushort[] levels = DecodePagedUInt16Array(packets[0], packets[1]);
        ushort[] caps = DecodePagedUInt16Array(packets[2], packets[3]);
        Assert.Equal(52, levels.Length);
        Assert.Equal((ushort)30, levels[1]);
        Assert.Equal((ushort)31, levels[2]);
        Assert.Equal((ushort)26, levels[3]);
        Assert.Equal((ushort)48, levels[22]);
        Assert.Equal((ushort)4, levels[39]);
        Assert.Equal(52, caps.Length);
        Assert.Equal((ushort)50, caps[1]);
        Assert.Equal((ushort)50, caps[9]);
        Assert.Equal((ushort)50, caps[40]);
        Assert.Equal((ushort)255, caps[0]);
        Assert.Equal((ushort)255, caps[8]);
        Assert.Equal((ushort)255, caps[51]);
    }

    [Fact]
    public void WorldCombatFixtureDistinguishesLockTargetFromActorAcknowledge()
    {
        string path = FindFixturePath(Path.Combine(
            "tests",
            "fixtures",
            "trace-evidence",
            "world-combat-lock-target-observed.json"));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        Assert.Equal("combat_autoattack.pcapng", root.GetProperty("capture").GetString());
        Assert.Equal("TraceConfirmed", root.GetProperty("evidenceStatus").GetString());
        ClientLockTargetPacket[] packets = root.GetProperty("packets")
            .EnumerateArray()
            .Select(item => new ClientLockTargetPacketCodec().Decode(SubPacket.Create(
                PacketOpcode.ClientLockTarget,
                0x029B2941,
                Convert.FromHexString(item.GetProperty("payloadHex").GetString()!))))
            .ToArray();

        Assert.Equal(new ClientLockTargetPacket(0x44D035D0, 0x0466E6A4), packets[0]);
        Assert.False(packets[0].IsClear);
        Assert.Equal(new ClientLockTargetPacket(0xC0000000, 0x00CF97AA), packets[1]);
        Assert.True(packets[1].IsClear);
    }

    [Fact]
    public void WorldBattleNpcLifecycleFixturePreservesObservedDefeatedStateAndRemoval()
    {
        string path = FindFixturePath(Path.Combine(
            "tests",
            "fixtures",
            "trace-evidence",
            "world-battle-npc-lifecycle-observed.json"));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        Assert.Equal("party_battle_leve.pcapng", root.GetProperty("captureName").GetString());
        Assert.Equal("World", root.GetProperty("service").GetString());
        Assert.Equal("TraceConfirmed", root.GetProperty("evidenceStatus").GetString());

        foreach (JsonElement observation in root.GetProperty("observations").EnumerateArray())
        {
            uint actorId = Convert.ToUInt32(observation.GetProperty("actorId").GetString(), 16);
            byte[] statePayload = Convert.FromHexString(observation.GetProperty("statePayloadHex").GetString()!);
            SetActorStatePacket state = new SetActorStatePacketCodec().Decode(SubPacket.Create(
                PacketOpcode.SetActorState,
                actorId,
                statePayload));
            byte[] removePayload = Convert.FromHexString(observation.GetProperty("removePayloadHex").GetString()!);
            RemoveActorPacket remove = new RemoveActorPacketCodec().Decode(SubPacket.Create(
                PacketOpcode.RemoveActor,
                actorId,
                removePayload));

            Assert.Equal("0x0134", observation.GetProperty("stateOpcode").GetString());
            Assert.Equal(3u, state.MainState);
            Assert.Equal(3u, state.SubState);
            Assert.Equal("0x00CB", observation.GetProperty("removeOpcode").GetString());
            Assert.Equal(actorId, remove.ActorId);
            Assert.All(removePayload, value => Assert.Equal(0, value));
            Assert.True(
                observation.GetProperty("removeTimestampMilliseconds").GetInt64()
                > observation.GetProperty("stateTimestampMilliseconds").GetInt64());
        }

        JsonElement timing = root.GetProperty("runtimeTimingEvidence");
        Assert.Equal(10, timing.GetProperty("deathToDespawnSeconds").GetInt32());
        Assert.True(timing.GetProperty("sameActorIdReused").GetBoolean());
        Assert.True(timing.GetProperty("restoresMaximumPools").GetBoolean());
    }

    [Fact]
    public void WorldChocoboStopFixturePreservesActorBindConditionsAndDisabledStatuses()
    {
        FixtureFrame frame = LoadFixtureFrames(
            "world-chocobo-stop-observed.json",
            "from_gridania_to_blackshroud.pcapng").Single();
        IReadOnlyList<WireLegacySubPacket> packets = DecodeCompleteTcpSegment(frame.PayloadHex);
        const uint actorId = 0x44D8002E;

        ActorInstantiatePacket actor = new ActorInstantiatePacketCodec().Decode(
            Assert.Single(packets, packet =>
                packet.SourceActorId == actorId && packet.Opcode == PacketOpcode.ActorInstantiate).ToSubPacket());
        Assert.Equal("chocoboStop_fst0Twn01_0J@09B00", actor.ObjectName);
        Assert.Equal("ChocoboStop", actor.ClassName);
        Assert.Equal(
            [
                "/Chara/Npc/Object/ChocoboStop",
                false, false, false, false, false,
                1090464,
                false, false,
                0, 1,
                "TEST"
            ],
            actor.InitParameters.Select(parameter => parameter.Value).ToArray());

        NoticeEventCondition notice = new SetNoticeEventConditionPacketCodec().Decode(
            Assert.Single(packets, packet =>
                packet.SourceActorId == actorId && packet.Opcode == PacketOpcode.SetNoticeEventCondition).ToSubPacket());
        Assert.Equal(new NoticeEventCondition("noticeEvent", 0, 1), notice);

        PushCircleEventCondition[] pushes = packets
            .Where(packet => packet.SourceActorId == actorId
                && packet.Opcode == PacketOpcode.SetPushEventConditionWithCircle)
            .Select(packet => new SetPushEventConditionWithCirclePacketCodec().Decode(packet.ToSubPacket()))
            .ToArray();
        Assert.Equal(["pushDefault", "_!pushRequest"], pushes.Select(push => push.ConditionName).ToArray());

        PushCircleEventCondition pushDefault = pushes[0];
        Assert.Equal(6.0f, pushDefault.Radius);
        Assert.Equal(6.0f, pushDefault.SecondaryRadius);
        Assert.Equal(0x4EA3ADB8u, pushDefault.Unknown1);
        Assert.Equal(0, pushDefault.Flags);
        Assert.Equal(3, pushDefault.Unknown2);
        Assert.False(pushDefault.Outwards);
        Assert.False(pushDefault.Silent);

        PushCircleEventCondition pushRequest = pushes[1];
        Assert.Equal(10.0f, pushRequest.Radius);
        Assert.Equal(10.0f, pushRequest.SecondaryRadius);
        Assert.Equal(0x097C623Eu, pushRequest.Unknown1);
        Assert.Equal(0, pushRequest.Flags);
        Assert.Equal(0, pushRequest.Unknown2);
        Assert.False(pushRequest.Outwards);
        Assert.True(pushRequest.Silent);

        EventStatusPacket[] statuses = packets
            .Where(packet => packet.SourceActorId == actorId
                && packet.Opcode == PacketOpcode.SetEventStatus)
            .Select(packet => new SetEventStatusPacketCodec().Decode(packet.ToSubPacket()))
            .ToArray();
        Assert.Equal(["pushDefault", "_!pushRequest"], statuses.Select(status => status.ConditionName).ToArray());
        Assert.All(statuses, status =>
        {
            Assert.Equal(2, status.Type);
            Assert.False(status.Enabled);
        });

        SetActorPositionPacket position = new SetActorPositionPacketCodec().Decode(
            Assert.Single(packets, packet =>
                packet.SourceActorId == actorId && packet.Opcode == PacketOpcode.SetActorPosition).ToSubPacket());
        Assert.Equal(318.72f, position.X, 2);
        Assert.Equal(4.04f, position.Y, 2);
        Assert.Equal(-992.74f, position.Z, 2);
    }

    private static ushort[] DecodePagedUInt16Array(
        SetActorPropertyPacket first,
        SetActorPropertyPacket second)
    {
        byte[] firstBytes = (byte[])Assert.Single(first.Values).Value;
        byte[] secondBytes = (byte[])Assert.Single(second.Values).Value;
        byte[] combined = [.. firstBytes[..^1], .. secondBytes[..^1]];
        ushort[] values = new ushort[combined.Length / sizeof(ushort)];
        for (int index = 0; index < values.Length; index++)
            values[index] = PacketBinary.ReadUInt16LittleEndian(combined.AsSpan(index * sizeof(ushort)));
        return values;
    }

    private static IReadOnlyList<FixtureFrame> LoadWorldFixtureFrames()
    {
        return LoadFixtureFrames(
            "world-gridania-to-coerthas-observed.json",
            "gridania_to_coerthas.pcapng");
    }

    private static IReadOnlyList<WireLegacySubPacket> DecodeTcpSegment(string payloadHex)
    {
        return DecodeTcpFrames(payloadHex)
            .SelectMany(frame => frame.SubPackets)
            .ToArray();
    }

    private static IReadOnlyList<WireLegacySubPacket> DecodeCompleteTcpSegment(string payloadHex)
    {
        byte[] payload = Convert.FromHexString(payloadHex);
        BasePacketFrameCodec codec = new();
        List<WireLegacySubPacket> packets = [];
        int offset = 0;
        while (offset + BasePacketFrameCodec.HeaderSize <= payload.Length)
        {
            ushort packetSize = PacketBinary.ReadUInt16LittleEndian(payload.AsSpan(offset + 4));
            if (packetSize < BasePacketFrameCodec.HeaderSize || offset + packetSize > payload.Length)
                break;

            packets.AddRange(codec.DecodeLegacy(payload.AsSpan(offset, packetSize)).SubPackets);
            offset += packetSize;
        }

        Assert.NotEmpty(packets);
        return packets;
    }

    private static IReadOnlyList<LegacyPacketFrame> DecodeTcpFrames(string payloadHex)
    {
        byte[] payload = Convert.FromHexString(payloadHex);
        BasePacketFrameCodec codec = new();
        List<LegacyPacketFrame> frames = [];
        int offset = 0;
        while (offset + BasePacketFrameCodec.HeaderSize <= payload.Length)
        {
            ushort packetSize = PacketBinary.ReadUInt16LittleEndian(payload.AsSpan(offset + 4));
            Assert.InRange(packetSize, BasePacketFrameCodec.HeaderSize, payload.Length - offset);
            LegacyPacketFrame frame = codec.DecodeLegacy(payload.AsSpan(offset, packetSize));
            frames.Add(frame);
            offset += packetSize;
        }

        Assert.Equal(payload.Length, offset);
        return frames;
    }

    private static bool IsObservedPlayerAutoAttack(WireLegacySubPacket packet)
    {
        if (packet.Opcode != PacketOpcode.CommandResultX01 || packet.SourceActorId != 0x029B2941)
            return false;

        return new CommandResultX01PacketCodec().Decode(packet.ToSubPacket()).CommandId == 0x5658;
    }

    private static CommandResultX01Packet DecodeObservedPlayerAutoAttack(LegacyPacketFrame frame)
    {
        WireLegacySubPacket packet = Assert.Single(frame.SubPackets, IsObservedPlayerAutoAttack);
        return new CommandResultX01PacketCodec().Decode(packet.ToSubPacket());
    }

    private static IReadOnlyList<FixtureFrame> LoadFixtureFrames(
        string fixtureFileName,
        string captureName,
        string expectedService = "World",
        int expectedPort = 54992,
        string expectedTool = "tshark")
    {
        string fixturePath = FindFixturePath(
            Path.Combine("tests", "fixtures", "trace-evidence", fixtureFileName));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(fixturePath));
        JsonElement root = document.RootElement;
        Assert.Equal("aetherxiv.trace.fixture.v1", root.GetProperty("schema").GetString());
        Assert.Equal(expectedService, root.GetProperty("service").GetString());
        Assert.Equal(expectedPort, root.GetProperty("serverPort").GetInt32());

        JsonElement capture = root.GetProperty("captures").EnumerateArray().Single();
        Assert.Equal(captureName, capture.GetProperty("capture").GetString());
        Assert.Equal(expectedTool, capture.GetProperty("tool").GetString());
        Assert.Empty(capture.GetProperty("accessIssues").EnumerateArray());

        return capture.GetProperty("frames")
            .EnumerateArray()
            .Select(ReadFrame)
            .ToArray();
    }

    private static FixtureFrame ReadFrame(JsonElement frame)
    {
        return new FixtureFrame(
            frame.GetProperty("captureName").GetString() ?? String.Empty,
            frame.GetProperty("service").GetString() ?? String.Empty,
            frame.GetProperty("direction").GetString() ?? String.Empty,
            frame.GetProperty("frameIndex").GetInt32(),
            frame.GetProperty("connectionType").GetInt32(),
            frame.GetProperty("opcodeKey").ValueKind == JsonValueKind.Null
                ? null
                : frame.GetProperty("opcodeKey").GetString(),
            frame.GetProperty("messageKeys")
                .EnumerateArray()
                .Select(ReadMessageKey)
                .ToArray(),
            frame.GetProperty("payloadLength").GetInt32(),
            frame.GetProperty("payloadHex").GetString() ?? String.Empty,
            frame.GetProperty("compressionState").GetString() ?? String.Empty,
            frame.GetProperty("evidenceStatus").GetString() ?? String.Empty);
    }

    private static FixtureMessageKey ReadMessageKey(JsonElement messageKey)
    {
        return new FixtureMessageKey(
            messageKey.GetProperty("messageType").GetString() ?? String.Empty,
            messageKey.GetProperty("category").GetString() ?? String.Empty,
            messageKey.GetProperty("subcode").GetString() ?? String.Empty,
            messageKey.GetProperty("messageLength").GetInt32());
    }

    private static string FindFixturePath(string relativePath)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find fixture {relativePath}.");
    }

    private sealed record FixtureFrame(
        string CaptureName,
        string Service,
        string Direction,
        int FrameIndex,
        int ConnectionType,
        string? OpcodeKey,
        IReadOnlyList<FixtureMessageKey> MessageKeys,
        int PayloadLength,
        string PayloadHex,
        string CompressionState,
        string EvidenceStatus);

    private sealed record FixtureMessageKey(
        string MessageType,
        string Category,
        string Subcode,
        int MessageLength);
}
