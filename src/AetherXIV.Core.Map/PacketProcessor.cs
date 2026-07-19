using AetherXIV.Core.Common;

using System;
using AetherXIV.Core.Map.dataobjects;
using AetherXIV.Core.Map.packets.receive;
using AetherXIV.Core.Map.packets.send;
using AetherXIV.Core.Map.packets.send.login;
using AetherXIV.Core.Map.packets.send.actor;
using AetherXIV.Core.Map.packets.send.supportdesk;
using AetherXIV.Core.Map.packets.receive.social;
using AetherXIV.Core.Map.packets.send.social;
using AetherXIV.Core.Map.packets.receive.supportdesk;
using AetherXIV.Core.Map.packets.receive.recruitment;
using AetherXIV.Core.Map.packets.send.recruitment;
using AetherXIV.Core.Map.packets.receive.events;
using AetherXIV.Core.Map.packets.send.events;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.actors.chara;
using AetherXIV.Core.Map.actors.chara.ai.state;
using AetherXIV.Core.Map.packets.WorldPackets.Send;
using AetherXIV.Core.Map.packets.WorldPackets.Receive;
using AetherXIV.Core.Map.actors.director;

namespace AetherXIV.Core.Map
{
    class PacketProcessor
    {
        Server mServer;

        public PacketProcessor(Server server)
        {
            mServer = server;
        }     

        public void ProcessPacket(ZoneConnection client, SubPacket subpacket)
        {                          
                Session session = mServer.GetSession(subpacket.header.sourceId);

                if (session == null && subpacket.gameMessage.opcode != 0x1000)
                {
                    PacketDiagnostics.LogUnknownGameMessage("Map", "missing session", subpacket);
                    return;
                }

                if (session != null && session.isEnding && subpacket.gameMessage.opcode < 0x1000)
                {
                    if (subpacket.gameMessage.opcode == 0x0001)
                    {
                        PingPacket pingPacket = new PingPacket(subpacket.data);
                        if (!pingPacket.invalidPacket)
                        {
                            session.QueuePacket(PongPacket.BuildPacket(session.id, pingPacket.time));
                            session.Ping();
                        }
                        else
                        {
                            DevDiagnostics.Trace(
                                "client.packet.invalid",
                                "session", session.id,
                                "opcode", "0x0001",
                                "reason", "ping payload is truncated");
                        }
                    }
                    else
                    {
                        DevDiagnostics.Trace(
                            "client.packet.afterSessionEnd",
                            "session", session.id,
                            "player", session.GetActor().customDisplayName,
                            "opcode", String.Format("0x{0:X4}", subpacket.gameMessage.opcode));
                    }

                    return;
                }

                //Normal Game Opcode
                switch (subpacket.gameMessage.opcode)
                {
                    //World Server - Error
                    case 0x100A:
                        ErrorPacket worldError = new ErrorPacket(subpacket.data);
                        switch (worldError.errorCode)
                        {
                            case 0x01:
                                session.GetActor().SendGameMessage(Server.GetWorldManager().GetActor(), 60005, 0x20);
                                break;
                        }
                        break;
                    //World Server - Session Begin
                    case 0x1000:
                        subpacket.DebugPrintSubPacket();

                        SessionBeginPacket beginSessionPacket = new SessionBeginPacket(subpacket.data);

                        session = mServer.AddSession(subpacket.header.sourceId);
                        Program.Log.Info("World requested session begin: session={0} login={1}", session.id, beginSessionPacket.isLogin);

                        if (!beginSessionPacket.isLogin)
                        {
                            Program.Log.Info(
                                "Map zone-in starting: session={0} actor={1} destinationSpawnType={2}",
                                session.id,
                                session.GetActor().customDisplayName,
                                session.GetActor().destinationSpawnType);
                            Server.GetWorldManager().DoZoneIn(session.GetActor(), false, session.GetActor().destinationSpawnType);
                        }

                        Program.Log.Info("{0} has been added to the session list.", session.GetActor().customDisplayName);

                        session.QueuePacket(SessionBeginConfirmPacket.BuildPacket(session));
                        client.FlushQueuedSendPackets();
                        break;
                    //World Server - Session End
                    case 0x1001:
                        SessionEndPacket endSessionPacket = new SessionEndPacket(subpacket.data);
                        Program.Log.Info(
                            "World requested session end: session={0} destinationZone={1} spawnType={2} pos=({3:F2},{4:F2},{5:F2}) rot={6:F2}",
                            session.id,
                            endSessionPacket.destinationZoneId,
                            endSessionPacket.destinationSpawnType,
                            endSessionPacket.destinationX,
                            endSessionPacket.destinationY,
                            endSessionPacket.destinationZ,
                            endSessionPacket.destinationRot);

                        if (endSessionPacket.destinationZoneId == 0 && session.isEnding)
                        {
                            DevDiagnostics.Trace(
                                "player.logout.cleanupSkipped",
                                "reason", "session already ended locally",
                                "session", session.id,
                                "player", session.GetActor().customDisplayName,
                                "destinationZone", endSessionPacket.destinationZoneId);
                        }
                        else if (endSessionPacket.destinationZoneId == 0)
                            session.GetActor().CleanupAndSave();
                        else                        
                            session.GetActor().CleanupAndSave(endSessionPacket.destinationZoneId, endSessionPacket.destinationSpawnType, endSessionPacket.destinationX, endSessionPacket.destinationY, endSessionPacket.destinationZ, endSessionPacket.destinationRot);

                        session.QueuePacket(SessionEndConfirmPacket.BuildPacket(session, endSessionPacket.destinationZoneId));
                        Program.Log.Info("Map queued session end confirm: session={0} destinationZone={1}", session.id, endSessionPacket.destinationZoneId);
                        client.FlushQueuedSendPackets();

                        if (endSessionPacket.destinationZoneId == 0)
                        {
                            Server.GetServer().BeginSessionEnd(session.id);
                            Program.Log.Info("{0} has been marked for session close.", session.GetActor().customDisplayName);
                        }
                        else
                        {
                            Server.GetServer().RemoveSession(session.id);
                            Program.Log.Info("{0} has been removed from the session list.", session.GetActor().customDisplayName);
                        }
                        break;
                    //World Server - Party Synch
                    case 0x1020:
                        PartySyncPacket partySyncPacket = new PartySyncPacket(subpacket.data);
                        Server.GetWorldManager().PartyMemberListRecieved(partySyncPacket);
                        break;
                    //World Server - Linkshell Creation Result
                    case 0x1025:
                        LinkshellResultPacket lsResult = new LinkshellResultPacket(subpacket.data);
                        LuaEngine.GetInstance().OnSignal("ls_result", lsResult.resultCode);
                        break;
                    //Ping
                    case 0x0001:
                        //subpacket.DebugPrintSubPacket();
                        PingPacket pingPacket = new PingPacket(subpacket.data);
                        if (!pingPacket.invalidPacket)
                        {
                            session.QueuePacket(PongPacket.BuildPacket(session.id, pingPacket.time));
                            session.Ping();
                        }
                        else
                        {
                            DevDiagnostics.Trace(
                                "client.packet.invalid",
                                "session", session.id,
                                "opcode", "0x0001",
                                "reason", "ping payload is truncated");
                        }
                        break;
                    //Unknown
                    case 0x0002:

                        PacketDiagnostics.LogUnknownGameMessage("Map", "map opcode 0x0002", subpacket);
                        subpacket.DebugPrintSubPacket();
                        session.QueuePacket(_0x2Packet.BuildPacket(session.id));
                        client.FlushQueuedSendPackets();

                        break;
                    //Chat Received
                    case 0x0003:
                        ChatMessagePacket chatMessage = new ChatMessagePacket(subpacket.data);
                        //Program.Log.Info("Got type-{5} message: {0} @ {1}, {2}, {3}, Rot: {4}", chatMessage.message, chatMessage.posX, chatMessage.posY, chatMessage.posZ, chatMessage.posRot, chatMessage.logType);
                   
                        if (Server.GetCommandProcessor().HandleChatInput(chatMessage.message, session))
                            return;

                        if (chatMessage.logType == SendMessagePacket.MESSAGE_TYPE_SAY || chatMessage.logType == SendMessagePacket.MESSAGE_TYPE_SHOUT)
                            session.GetActor().BroadcastPacket(SendMessagePacket.BuildPacket(session.id, chatMessage.logType, session.GetActor().customDisplayName, chatMessage.message), false);

                        break;
                    //Langauge Code (Client safe to send packets to now)
                    case 0x0006:
                        LangaugeCodePacket langCode = new LangaugeCodePacket(subpacket.data);
                        ushort pendingSpawnType = session.GetActor().destinationSpawnType;
                        ushort loginSpawnType = (ushort)0x1;
                        DevDiagnostics.Trace(
                            "client.login.ready",
                            "session", session.id,
                            "player", session.GetActor().customDisplayName,
                            "languageCode", langCode.languageCode,
                            "invalidPacket", langCode.invalidPacket,
                            "zone", session.GetActor().zoneId,
                            "privateArea", session.GetActor().privateArea ?? "",
                            "privateAreaType", session.GetActor().privateAreaType,
                            "destinationSpawnType", pendingSpawnType,
                            "loginSpawnType", loginSpawnType);
                        LuaEngine.GetInstance().CallLuaFunction(session.GetActor(), session.GetActor(), "onBeginLogin", true);                    
                        Server.GetWorldManager().DoZoneIn(session.GetActor(), true, loginSpawnType);
                        LuaEngine.GetInstance().CallLuaFunction(session.GetActor(), session.GetActor(), "onLogin", true);
                        session.languageCode = langCode.languageCode;
                        DevDiagnostics.Trace(
                            "client.login.ready.done",
                            "session", session.id,
                            "player", session.GetActor().customDisplayName,
                            "zone", session.GetActor().zoneId,
                            "privateArea", session.GetActor().privateArea ?? "",
                            "privateAreaType", session.GetActor().privateAreaType);
                        break;
                    //Unknown - Happens a lot at login, then once every time player zones
                    case 0x0007:
                        //subpacket.DebugPrintSubPacket();
                        ZoneInCompletePacket zoneInCompletePacket = new ZoneInCompletePacket(subpacket.data);
                        DevDiagnostics.Trace(
                            "client.zoneInComplete",
                            "session", session.id,
                            "player", session.GetActor().customDisplayName,
                            "zone", session.GetActor().zoneId,
                            "privateArea", session.GetActor().privateArea ?? "",
                            "privateAreaType", session.GetActor().privateAreaType,
                            "timestampRaw", zoneInCompletePacket.timestamp,
                            "unknown", zoneInCompletePacket.unknown,
                            "invalidPacket", zoneInCompletePacket.invalidPacket);
                        break;
                    //Update Position
                    case 0x00CA:
                        //Update Position
                        UpdatePlayerPositionPacket posUpdate = new UpdatePlayerPositionPacket(subpacket.data);
                        if (posUpdate.invalidPacket)
                        {
                            DevDiagnostics.Trace(
                                "client.packet.invalid",
                                "session", session.id,
                                "opcode", "0x00CA",
                                "reason", "position payload is truncated");
                            break;
                        }
                        DevDiagnostics.Trace(
                            "client.position",
                            "session", session.id,
                            "player", session.GetActor().customDisplayName,
                            "zone", session.GetActor().zoneId,
                            "privateArea", session.GetActor().privateArea ?? "",
                            "privateAreaType", session.GetActor().privateAreaType,
                            "x", posUpdate.x,
                            "y", posUpdate.y,
                            "z", posUpdate.z,
                            "rot", posUpdate.rot,
                            "moveState", posUpdate.moveState,
                            "isZoneChanging", session.GetActor().IsInZoneChange());
                        Player positionPlayer = session.GetActor();
                        if (positionPlayer.IsInZoneChange()
                            && !positionPlayer.IsZoneChangePositionAcceptable(posUpdate.x, posUpdate.y, posUpdate.z))
                        {
                            break;
                        }

                        session.UpdatePlayerActorPosition(posUpdate.x, posUpdate.y, posUpdate.z, posUpdate.rot, posUpdate.moveState);
                        positionPlayer.SendInstanceUpdate();

                        if (positionPlayer.IsInZoneChange())
                            positionPlayer.CompleteZoneChange();

                        break;
                    //Set Target 
                    case 0x00CD:
                        //subpacket.DebugPrintSubPacket();

                        SetTargetPacket setTarget = new SetTargetPacket(subpacket.data);
                        ClientInteractionDiagnostics.TraceTarget(session, setTarget);
                        var player = session.GetActor();
                        var targetActor = player.zone == null ? null : player.zone.FindActorInArea<Character>(setTarget.actorID);
                        bool autoAttackRequested = setTarget.attackTarget != 0xE0000000;
                        player.currentTarget = setTarget.actorID;

                        if (autoAttackRequested)
                        {
                            player.isAutoAttackEnabled = player.GetMountState() == 0;
                            if (player.GetMountState() != 0)
                            {
                                player.SendGameMessage(Server.GetWorldManager().GetActor(), 32553, 0x20);
                                session.GetActor().BroadcastPacket(SetActorTargetAnimatedPacket.BuildPacket(session.id, setTarget.actorID), true);
                                break;
                            }
                            if (targetActor != null)
                            {
                                if (targetActor.CanBeAttackedBy(player))
                                {
                                    player.aiContainer.Engage(targetActor);
                                }
                                else
                                {
                                    DevDiagnostics.Trace(
                                        "battle.engage.blocked",
                                        "reason", "target packet target is not attackable",
                                        "actor", String.Format("0x{0:X}", player.actorId),
                                        "actorName", player.customDisplayName != null ? player.customDisplayName : player.actorName,
                                        "requestedTarget", String.Format("0x{0:X}", setTarget.actorID),
                                        "targetName", targetActor.customDisplayName != null ? targetActor.customDisplayName : targetActor.actorName,
                                        "targetType", targetActor.GetType().Name,
                                        "attackTarget", String.Format("0x{0:X}", setTarget.attackTarget));
                                }
                            }
                            else
                            {
                                DevDiagnostics.Trace(
                                    "battle.engage.blocked",
                                    "reason", "target packet target missing",
                                    "actor", String.Format("0x{0:X}", player.actorId),
                                    "actorName", player.customDisplayName != null ? player.customDisplayName : player.actorName,
                                    "requestedTarget", String.Format("0x{0:X}", setTarget.actorID),
                                    "attackTarget", String.Format("0x{0:X}", setTarget.attackTarget));
                            }
                        }
                        else if (player.aiContainer.IsCurrentState<AttackState>())
                        {
                            player.aiContainer.ChangeTarget(targetActor);
                        }

                        session.GetActor().BroadcastPacket(SetActorTargetAnimatedPacket.BuildPacket(session.id, setTarget.actorID), true);
                        break;
                    //Lock Target
                    case 0x00CC:
                        LockTargetPacket lockTarget = new LockTargetPacket(subpacket.data);
                        ClientInteractionDiagnostics.TraceLockTarget(session, lockTarget);
                        session.GetActor().currentLockedTarget = lockTarget.actorID;
                        break;
                    //Start Event
                    case 0x012D:
                        subpacket.DebugPrintSubPacket();
                        EventStartPacket eventStart = new EventStartPacket(subpacket.data);

                        /*
                        if (eventStart.error != null)
                        {
                            player.errorMessage += eventStart.error;

                            if (eventStart.errorIndex == eventStart.errorNum - 1)
                                Program.Log.Error("\n"+player.errorMessage);


                            break;
                        }
                        */

                        Actor ownerActor = Server.GetStaticActors(eventStart.ownerActorID);
                        var eventPlayer = session.GetActor();
                            
                        if (ownerActor == null)
                        {
                            //Is it your retainer?
                            if (eventPlayer.currentSpawnedRetainer != null && eventPlayer.currentSpawnedRetainer.actorId == eventStart.ownerActorID)
                                ownerActor = eventPlayer.currentSpawnedRetainer;
                            //Is it a instance actor?
                            if (ownerActor == null && eventPlayer.zone != null)
                                ownerActor = eventPlayer.zone.FindActorInArea(eventStart.ownerActorID);
                            if (ownerActor == null)
                            {
                                //Is it a Director?
                                Director director = eventPlayer.GetDirector(eventStart.ownerActorID);
                                if (director != null)
                                    ownerActor = director;
                                else
                                {
                                    Program.Log.Debug("\n===Event START===\nCould not find owner actor 0x{0:X} for event started by trigger: 0x{1:X}\nEvent Starter: {2}\nParams: {3}", eventStart.ownerActorID, eventStart.triggerActorID, eventStart.eventName, LuaUtils.DumpParams(eventStart.luaParams));
                                    ClientInteractionDiagnostics.TraceEventStartOwnerMissing(session, eventStart);
                                    ClientInteractionDiagnostics.TraceEventStartOwnerMissingClosed(session, eventStart);
                                    SubPacket endEvent = EndEventPacket.BuildPacket(eventPlayer.actorId, eventStart.ownerActorID, eventStart.eventName, eventStart.eventType);
                                    endEvent.DebugPrintSubPacket();
                                    eventPlayer.QueuePacket(endEvent);
                                    break;
                                }
                            }                                    
                        }

                        session.GetActor().StartEvent(ownerActor, eventStart);

                        Program.Log.Debug("\n===Event START===\nSource Actor: 0x{0:X}\nCaller Actor: 0x{1:X}\nVal1: 0x{2:X}\nVal2: 0x{3:X}\nEvent Starter: {4}\nParams: {5}", eventStart.triggerActorID, eventStart.ownerActorID, eventStart.serverCodes, eventStart.unknown, eventStart.eventName, LuaUtils.DumpParams(eventStart.luaParams));
                        break;
                    //Unknown, happens at npc spawn and cutscene play????
                    case 0x00CE:
                        ClientInteractionDiagnostics.TraceStateMessage(session, subpacket);
                        PacketDiagnostics.LogUnknownGameMessage("Map", "map opcode 0x00CE", subpacket);
                        subpacket.DebugPrintSubPacket();
                        break;
                    //Countdown requested
                    case 0x00CF:
                        CountdownRequestPacket countdownPacket = new CountdownRequestPacket(subpacket.data);
                        session.GetActor().BroadcastCountdown(countdownPacket.countdownLength, countdownPacket.syncTime);
                        break;
                    //Event Result
                    case 0x012E:
                        subpacket.DebugPrintSubPacket();
                        EventUpdatePacket eventUpdate = new EventUpdatePacket(subpacket.data);
                        Program.Log.Debug("\n===Event UPDATE===\nSource Actor: 0x{0:X}\nCaller Actor: 0x{1:X}\nVal1: 0x{2:X}\nVal2: 0x{3:X}\nStep: 0x{4:X}\nParams: {5}", eventUpdate.triggerActorID, eventUpdate.serverCodes, eventUpdate.unknown1, eventUpdate.unknown2, eventUpdate.eventType, LuaUtils.DumpParams(eventUpdate.luaParams));
                        /*
                        //Is it a static actor? If not look in the player's instance
                        Actor updateOwnerActor = Server.GetStaticActors(session.GetActor().currentEventOwner);
                        if (updateOwnerActor == null)
                        {
                            updateOwnerActor = Server.GetWorldManager().GetActorInWorld(session.GetActor().currentEventOwner);

                            if (session.GetActor().currentDirector != null && session.GetActor().currentEventOwner == session.GetActor().currentDirector.actorId)
                                updateOwnerActor = session.GetActor().currentDirector;

                            if (updateOwnerActor == null)
                                break;
                        }
                        */
                        session.GetActor().UpdateEvent(eventUpdate);

                        //LuaEngine.DoActorOnEventUpdated(session.GetActor(), updateOwnerActor, eventUpdate);
                            
                        break;
                    case 0x012F:
                        subpacket.DebugPrintSubPacket();
                        ParameterDataRequestPacket paramRequest = new ParameterDataRequestPacket(subpacket.data);
                        bool handledParameterRequest = !paramRequest.invalidPacket && paramRequest.paramName != null && paramRequest.paramName.Equals("charaWork/exp");
                        DevDiagnostics.Trace(
                            "client.parameter.request",
                            "player", session.GetActor().customDisplayName,
                            "actor", String.Format("0x{0:X}", session.GetActor().actorId),
                            "requestedActor", String.Format("0x{0:X}", paramRequest.actorID),
                            "paramName", paramRequest.paramName,
                            "invalidPacket", paramRequest.invalidPacket,
                            "handled", handledParameterRequest);
                        if (handledParameterRequest)
                            session.GetActor().SendCharaExpInfo();
                        break;
                    //Item Package Request
                    case 0x0131:
                        UpdateItemPackagePacket packageRequest = new UpdateItemPackagePacket(subpacket.data);
                        if (Server.GetWorldManager().GetActorInWorld(packageRequest.actorID) != null)
                        {
                            ((Character)Server.GetWorldManager().GetActorInWorld(packageRequest.actorID)).SendItemPackage(session.GetActor(), packageRequest.packageId);
                            break;
                        }
                        if (session.GetActor().GetSpawnedRetainer() != null && session.GetActor().GetSpawnedRetainer().actorId == packageRequest.actorID)
                            session.GetActor().GetSpawnedRetainer().SendItemPackage(session.GetActor(), packageRequest.packageId);
                        break;
                    //Group Created Confirm
                    case 0x0133:
                        GroupCreatedPacket groupCreated = new GroupCreatedPacket(subpacket.data);
                        Server.GetWorldManager().SendGroupInit(session, groupCreated.groupId);
                        break;
                    //Achievement Progress Request
                    case 0x0135:
                        AchievementProgressRequestPacket progressRequest = new AchievementProgressRequestPacket(subpacket.data);
                        session.QueuePacket(Database.GetAchievementProgress(session.GetActor(), progressRequest.achievementId));
                        break;
                    /* RECRUITMENT */
                    //Start Recruiting
                    case 0x01C3:
                        StartRecruitingRequestPacket recruitRequestPacket = new StartRecruitingRequestPacket(subpacket.data);
                        session.QueuePacket(StartRecruitingResponse.BuildPacket(session.id, true));
                        break;
                    //End Recruiting
                    case 0x01C4:
                        session.QueuePacket(EndRecruitmentPacket.BuildPacket(session.id));
                        break;
                    //Party Window Opened, Request State
                    case 0x01C5:
                        session.QueuePacket(RecruiterStatePacket.BuildPacket(session.id, false, false, 0));
                        break;
                    //Search Recruiting
                    case 0x01C7:
                        RecruitmentSearchRequestPacket recruitSearchPacket = new RecruitmentSearchRequestPacket(subpacket.data);
                        break;
                    //Get Recruitment Details
                    case 0x01C8:
                        RecruitmentDetailsRequestPacket currentRecruitDetailsPacket = new RecruitmentDetailsRequestPacket(subpacket.data);
                        RecruitmentDetails details = new RecruitmentDetails();
                        details.recruiterName = "Localhost Character";
                        details.purposeId = 2;
                        details.locationId = 1;
                        details.subTaskId = 1;
                        details.comment = "This is a test details packet sent by the server. No implementation has been Created yet...";
                        details.num[0] = 1;
                        session.QueuePacket(CurrentRecruitmentDetailsPacket.BuildPacket(session.id, details));
                        break;
                    //Accepted Recruiting
                    case 0x01C6:
                        subpacket.DebugPrintSubPacket();
                        break;
                    /* SOCIAL STUFF */
                    case 0x01C9:
                        AddRemoveSocialPacket addBlackList = new AddRemoveSocialPacket(subpacket.data);
                        session.QueuePacket(BlacklistAddedPacket.BuildPacket(session.id, true, addBlackList.name));
                        break;
                    case 0x01CA:
                        AddRemoveSocialPacket RemoveBlackList = new AddRemoveSocialPacket(subpacket.data);
                        session.QueuePacket(BlacklistRemovedPacket.BuildPacket(session.id, true, RemoveBlackList.name));
                        break;
                    case 0x01CB:
                        int offset1 = 0;
                        session.QueuePacket(SendBlacklistPacket.BuildPacket(session.id, new String[] { "Test" }, ref offset1));
                        break;
                    case 0x01CC:
                        AddRemoveSocialPacket addFriendList = new AddRemoveSocialPacket(subpacket.data);
                        session.QueuePacket(FriendlistAddedPacket.BuildPacket(session.id, true, (uint)addFriendList.name.GetHashCode(), true, addFriendList.name));
                        break;
                    case 0x01CD:
                        AddRemoveSocialPacket RemoveFriendList = new AddRemoveSocialPacket(subpacket.data);
                        session.QueuePacket(FriendlistRemovedPacket.BuildPacket(session.id, true, RemoveFriendList.name));
                        break;
                    case 0x01CE:
                        int offset2 = 0;
                        session.QueuePacket(SendFriendlistPacket.BuildPacket(session.id, new Tuple<long, string>[] { new Tuple<long, string>(01, "Test2") }, ref offset2));
                        break;
                    case 0x01CF:
                        session.QueuePacket(FriendStatusPacket.BuildPacket(session.id, null));
                        break;
                    /* SUPPORT DESK STUFF */
                    //Request for FAQ/Info List
                    case 0x01D0:
                        FaqListRequestPacket faqRequest = new FaqListRequestPacket(subpacket.data);
                        session.QueuePacket(FaqListResponsePacket.BuildPacket(session.id, new string[] { "Testing FAQ1", "Coded style!" }));
                        break;
                    //Request for body of a faq/info selection
                    case 0x01D1:
                        FaqBodyRequestPacket faqBodyRequest = new FaqBodyRequestPacket(subpacket.data);
                        session.QueuePacket(FaqBodyResponsePacket.BuildPacket(session.id, "HERE IS A GIANT BODY. Nothing else to say!"));
                        break;
                    //Request issue list
                    case 0x01D2:
                        GMTicketIssuesRequestPacket issuesRequest = new GMTicketIssuesRequestPacket(subpacket.data);
                        session.QueuePacket(IssueListResponsePacket.BuildPacket(session.id, new string[] { "Test1", "Test2", "Test3", "Test4", "Test5" }));
                        break;
                    //Request if GM ticket exists
                    case 0x01D3:
                        session.QueuePacket(StartGMTicketPacket.BuildPacket(session.id, false));
                        break;
                    //Request for GM response message
                    case 0x01D4:
                        session.QueuePacket(GMTicketPacket.BuildPacket(session.id, "This is a GM Ticket Title", "This is a GM Ticket Body."));
                        break;
                    //GM Ticket Sent
                    case 0x01D5:
                        GMSupportTicketPacket gmTicket = new GMSupportTicketPacket(subpacket.data);
                        Program.Log.Info("Got GM Ticket: \n" + gmTicket.ticketTitle + "\n" + gmTicket.ticketBody);
                        session.QueuePacket(GMTicketSentResponsePacket.BuildPacket(session.id, true));
                        break;
                    //Request to end ticket
                    case 0x01D6:
                        session.QueuePacket(EndGMTicketPacket.BuildPacket(session.id));
                        break;
                    default:
                        Program.Log.Debug("Unknown command 0x{0:X} received.", subpacket.gameMessage.opcode);
                        PacketDiagnostics.LogUnknownGameMessage("Map", "map game message", subpacket);
                        subpacket.DebugPrintSubPacket();
                        break;
                }
            
        }        

    }
}
