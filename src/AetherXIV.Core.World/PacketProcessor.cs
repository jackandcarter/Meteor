using System.Collections.Generic;

using AetherXIV.Core.Common;
using AetherXIV.Core.World.DataObjects;
using AetherXIV.Core.World.DataObjects.Group;
using AetherXIV.Core.World.Packets.Receive;
using AetherXIV.Core.World.Packets.Receive.Subpackets;
using AetherXIV.Core.World.Packets.Send;
using AetherXIV.Core.World.Packets.Send.Login;
using AetherXIV.Core.World.Packets.Send.Subpackets;
using AetherXIV.Core.World.Packets.WorldPackets.Receive;

namespace AetherXIV.Core.World
{
    class PacketProcessor
    {
        /*
        Session Creation:

            Get 0x1 from server
            Send 0x7
            Send 0x2

        Zone Change:

            Send 0x7
            Get 0x8 - Wait??
            Send 0x2
        */


        Server mServer;

        public PacketProcessor(Server server)
        {           
            mServer = server;
        }     

        public void ProcessPacket(ClientConnection client, BasePacket packet)
        {                      
            if (packet.header.isCompressed == 0x01)                       
                BasePacket.DecompressPacket(ref packet);
            
            List<SubPacket> subPackets = packet.GetSubpackets();
            foreach (SubPacket subpacket in subPackets)
            {
                //Initial Connect Packet, Create session
                if (subpacket.header.type == 0x01)
                {                    
                    HelloPacket hello = new HelloPacket(packet.data);
                    Program.Log.Info(
                        "World hello received: remote={0} connectionType=0x{1:X} session={2} invalid={3}",
                        client.GetAddress(),
                        packet.header.connectionType,
                        hello.sessionId,
                        hello.invalidPacket);

                    if (hello.invalidPacket || hello.sessionId == 0)
                    {
                        Program.Log.Error(
                            "World hello rejected: remote={0} connectionType=0x{1:X} session={2} invalid={3}",
                            client.GetAddress(),
                            packet.header.connectionType,
                            hello.sessionId,
                            hello.invalidPacket);
                        continue;
                    }

                    if (packet.header.connectionType == BasePacket.TYPE_ZONE)
                    {
                        mServer.AddSession(client, Session.Channel.ZONE, hello.sessionId);
                        Session session = mServer.GetSession(hello.sessionId);
                        if (session == null)
                        {
                            Program.Log.Error("World zone hello failed: session {0} was not added.", hello.sessionId);
                            continue;
                        }

                        Program.Log.Info(
                            "World zone session loaded: session={0} character={1} currentZone={2} activeLinkshell={3}",
                            session.sessionId,
                            session.characterName ?? "",
                            session.currentZoneId,
                            session.activeLinkshellName ?? "");

                        session.routing1 = mServer.GetWorldManager().GetZoneServer(session.currentZoneId);
                        if (session.routing1 == null)
                        {
                            Program.Log.Error(
                                "World zone hello failed: session={0} character={1} currentZone={2} has no map route.",
                                session.sessionId,
                                session.characterName ?? "",
                                session.currentZoneId);
                            continue;
                        }

                        session.routing1.SendSessionStart(session, true);       
                    }
                    else if (packet.header.connectionType == BasePacket.TYPE_CHAT)
                    {
                        mServer.AddSession(client, Session.Channel.CHAT, hello.sessionId);
                        Program.Log.Info("World chat session loaded: session={0}", hello.sessionId);
                    }
                    else
                    {
                        Program.Log.Error(
                            "World hello rejected: unsupported connectionType=0x{0:X} session={1}",
                            packet.header.connectionType,
                            hello.sessionId);
                        continue;
                    }

                    client.QueuePacket(_0x7Packet.BuildPacket(0x0E016EE5));
                    client.QueuePacket(_0x2Packet.BuildPacket(hello.sessionId));
                }
                //Ping from World Server
                else if (subpacket.header.type == 0x07)
                {
                    SubPacket init = _0x8PingPacket.BuildPacket(client.owner.sessionId);
                    client.QueuePacket(BasePacket.CreatePacket(init, true, false));
                }
                //Zoning Related
                else if (subpacket.header.type == 0x08)
                {
                    //Response, client's current [actorID][time]
                    //BasePacket init = Login0x7ResponsePacket.BuildPacket(BitConverter.ToUInt32(packet.data, 0x10), Utils.UnixTimeStampUTC(), 0x07);
                    //client.QueuePacket(init);
                    PacketDiagnostics.LogUnknownSubPacket("World", "world type 0x08", subpacket);
                    packet.DebugPrintPacket();
                }
                //Game Message
                else if (subpacket.header.type == 0x03)
                {                
                    //Send to the correct zone server
                    uint targetSession = subpacket.header.targetId;
                    Session target = mServer.GetSession(targetSession);

                    if (target == null)
                    {
                        Program.Log.Error(
                            "World game message dropped: opcode=0x{0:X} targetSession={1} has no active session.",
                            subpacket.gameMessage.opcode,
                            targetSession);
                        continue;
                    }

                    InterceptProcess(target, subpacket);

                    if (target.routing1 != null)
                        target.routing1.SendPacket(subpacket);
                    else
                        Program.Log.Error(
                            "World game message could not route: opcode=0x{0:X} session={1} zone={2} has no primary map route.",
                            subpacket.gameMessage.opcode,
                            target.sessionId,
                            target.currentZoneId);

                    if (target.routing2 != null)
                        target.routing2.SendPacket(subpacket);
                }
                //World Server Type
                else if (subpacket.header.type >= 0x1000)
                {
                    uint targetSession = subpacket.header.targetId;
                    Session session = mServer.GetSession(targetSession);

                    switch (subpacket.header.type)
                    {
                        //Session Begin Confirm
                        case 0x1000:
                            SessionBeginConfirmPacket beginConfirmPacket = new SessionBeginConfirmPacket(packet.data);

                            if (beginConfirmPacket.invalidPacket || beginConfirmPacket.errorCode != 0)
                                Program.Log.Error("Session {0} had a error beginning session.", beginConfirmPacket.sessionId);                            
                            else
                                Program.Log.Info("Session begin confirmed by world packet path: session={0}", beginConfirmPacket.sessionId);

                            break;
                        //Session End Confirm
                        case 0x1001:
                            SessionEndConfirmPacket endConfirmPacket = new SessionEndConfirmPacket(packet.data);
                            
                            if (!endConfirmPacket.invalidPacket && endConfirmPacket.errorCode == 0)
                            {
                                Program.Log.Info("Session end confirmed by world packet path: session={0} destinationZone={1}", endConfirmPacket.sessionId, endConfirmPacket.destinationZone);
                                //Check destination, if != 0, update route and start new session
                                if (endConfirmPacket.destinationZone != 0)
                                {
                                    session.currentZoneId = endConfirmPacket.destinationZone;
                                    session.routing1 = Server.GetServer().GetWorldManager().GetZoneServer(endConfirmPacket.destinationZone);
                                    session.routing1.SendSessionStart(session);
                                }
                                else
                                {                                    
                                    mServer.RemoveSession(Session.Channel.ZONE, endConfirmPacket.sessionId);
                                    mServer.RemoveSession(Session.Channel.CHAT, endConfirmPacket.sessionId);
                                }
                            }
                            else
                                Program.Log.Error("Session {0} had an error ending session.", endConfirmPacket.sessionId);

                            break;                        
                        //Zone Change Request
                        case 0x1002:
                            WorldRequestZoneChangePacket zoneChangePacket = new WorldRequestZoneChangePacket(packet.data);

                            if (!zoneChangePacket.invalidPacket)
                            {
                                Program.Log.Info(
                                    "World packet path zone change request: session={0} destinationZone={1} spawnType={2} pos=({3:F2},{4:F2},{5:F2}) rot={6:F2}",
                                    zoneChangePacket.sessionId,
                                    zoneChangePacket.destinationZoneId,
                                    zoneChangePacket.destinationSpawnType,
                                    zoneChangePacket.destinationX,
                                    zoneChangePacket.destinationY,
                                    zoneChangePacket.destinationZ,
                                    zoneChangePacket.destinationRot);
                                mServer.GetWorldManager().DoZoneServerChange(session, zoneChangePacket.destinationZoneId, "", zoneChangePacket.destinationSpawnType, zoneChangePacket.destinationX, zoneChangePacket.destinationY, zoneChangePacket.destinationZ, zoneChangePacket.destinationRot);
                            }
                           
                            break;
                        default:
                            PacketDiagnostics.LogUnknownSubPacket("World", "world server subpacket", subpacket);
                            break;
                    }

                }
                else
                {
                    PacketDiagnostics.LogUnknownSubPacket("World", "world subpacket", subpacket);
                    packet.DebugPrintPacket();
                }
            }
        }    

        public void InterceptProcess(Session session, SubPacket subpacket)
        {
            if (session == null)
            {
                Program.Log.Error(
                    "World intercept skipped: opcode=0x{0:X} has no session.",
                    subpacket.gameMessage.opcode);
                return;
            }

            switch (subpacket.gameMessage.opcode)
            {
                case 0x00C9:
                    subpacket.DebugPrintSubPacket();
                    PartyChatMessagePacket partyChatMessagePacket = new PartyChatMessagePacket(subpacket.data);                 
                    Party playerParty = mServer.GetWorldManager().GetPartyManager().GetParty(session.sessionId);
                    for (int i = 0; i < playerParty.members.Count; i++)
                    {
                        Session thatSession = mServer.GetSession(playerParty.members[i]);
                        if (thatSession != null && !session.Equals(thatSession))
                        {
                            thatSession.clientConnection.QueuePacket(SendMessagePacket.BuildPacket(session.sessionId, thatSession.sessionId, SendMessagePacket.MESSAGE_TYPE_PARTY, mServer.GetNameForId(session.sessionId), partyChatMessagePacket.message));
                        }
                    }                    
                    break;
                case 0x6:
                    Program.Log.Info(
                        "World login ready opcode received: session={0} character={1} zone={2}",
                        session.sessionId,
                        session.characterName ?? "",
                        session.currentZoneId);
                    mServer.GetWorldManager().DoLogin(session);
                    break;
                    //Special case for groups. If it's a world group, send values, else send to zone server
                case 0x133:            
                    GroupCreatedPacket groupCreatedPacket = new GroupCreatedPacket(subpacket.data);
                    if (!mServer.GetWorldManager().SendGroupInit(session, groupCreatedPacket.groupId))                                    
                        session.clientConnection.QueuePacket(subpacket);
                    break;
                case 0x0001:
                case 0x0002:
                case 0x0003:
                case 0x0007:
                case 0x00CA:
                case 0x00CC:
                case 0x00CD:
                case 0x00CE:
                case 0x00CF:
                case 0x012D:
                case 0x012E:
                case 0x012F:
                case 0x0131:
                case 0x0135:
                case 0x01C3:
                case 0x01C4:
                case 0x01C5:
                case 0x01C6:
                case 0x01C7:
                case 0x01C8:
                case 0x01C9:
                case 0x01CA:
                case 0x01CB:
                case 0x01CC:
                case 0x01CD:
                case 0x01CE:
                case 0x01CF:
                case 0x01D0:
                case 0x01D1:
                case 0x01D2:
                case 0x01D3:
                case 0x01D4:
                case 0x01D5:
                case 0x01D6:
                    break;
                default:
                    PacketDiagnostics.LogUnknownGameMessage("World", "world game message passthrough", subpacket);
                    break;
            }
        }
    }
}
