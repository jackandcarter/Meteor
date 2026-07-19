using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors.director.Work;
using AetherXIV.Core.Map.actors.group;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.dataobjects;
using AetherXIV.Core.Map.utils;
using System;
using System.Collections.Generic;

namespace AetherXIV.Core.Map.actors.director
{
    class GuildleveDirector : Director
    {
        public uint guildleveId;
        public Player guildleveOwner;        
        public byte selectedDifficulty;

        public GuildleveData guildleveData;
        public GuildleveWork guildleveWork = new GuildleveWork();

        public bool isEnded = false;
        public uint completionTime = 0;

        public bool UsesTraceRestoredContent
        {
            get { return guildleveId == 12487 && zone.actorId == 162; }
        }

        public GuildleveDirector(uint id, Area zone, string directorPath, uint guildleveId, byte selectedDifficulty, Player guildleveOwner, params object[] args)
            : base(id, zone, directorPath, true, args)
        {
            this.guildleveId = guildleveId;
            this.selectedDifficulty = selectedDifficulty;
            this.guildleveData = Server.GetGuildleveGamedata(guildleveId);
            this.guildleveOwner = guildleveOwner;

            if (UsesTraceRestoredContent)
            {
                positionX = -94.07f;
                positionY = 4.0f;
                positionZ = -543.16f;
                rotation = 0.0f;
            }

            guildleveWork.aimNum[0] = guildleveData.aimNum[0];
            guildleveWork.aimNum[1] = guildleveData.aimNum[1];
            guildleveWork.aimNum[2] = guildleveData.aimNum[2];
            guildleveWork.aimNum[3] = guildleveData.aimNum[3];

            if (guildleveWork.aimNum[0] != 0)
                guildleveWork.uiState[0] = 1;
            if (guildleveWork.aimNum[1] != 0)
                guildleveWork.uiState[1] = 1;
            if (guildleveWork.aimNum[2] != 0)
                guildleveWork.uiState[2] = 1;
            if (guildleveWork.aimNum[3] != 0)
                guildleveWork.uiState[3] = 1;

            guildleveWork.aimNumNow[0] = guildleveWork.aimNumNow[1] = guildleveWork.aimNumNow[2] = guildleveWork.aimNumNow[3] = 0;
        }

        public void LoadGuildleve()
        {
            if (!UsesTraceRestoredContent)
                return;

            SpawnInitialObject(1200161, "guildleve12487:bonus", 105.0f, 16.625f, -510.0f);
            SpawnInitialObject(1200036, "guildleve12487:search:0", 5.0f, 4.516114f, -450.0f);
            SpawnInitialObject(1200036, "guildleve12487:search:1", 37.0f, 17.013208f, -513.0f);
            SpawnInitialObject(1200036, "guildleve12487:search:2", 135.0f, 16.407284f, -429.0f);
            SpawnInitialObject(1200036, "guildleve12487:search:3", 11.000001f, 4.229520f, -435.0f);
            SpawnInitialObject(1200036, "guildleve12487:search:4", 57.0f, 16.406033f, -531.0f);
            SpawnInitialObject(1200036, "guildleve12487:search:5", 0.271608f, 5.008801f, -413.047607f);
            SpawnInitialObject(1200036, "guildleve12487:search:6", 187.290344f, 15.943882f, -425.487305f);
            SpawnInitialObject(1200036, "guildleve12487:search:7", 1.000001f, 4.713294f, -421.0f);
            SpawnInitialObject(1200036, "guildleve12487:search:8", 63.121704f, 17.018314f, -539.018188f);
            SpawnInitialObject(1200036, "guildleve12487:search:9", 141.184448f, 16.727419f, -434.126892f);
            SpawnInitialObject(1200036, "guildleve12487:search:10", 63.543701f, 16.390932f, -520.013733f);

            StartGuildleve();
            SyncAllInfo();
            UpdateMarkers(0, 0.271608f, 5.008801f, -413.047607f);
            UpdateMarkers(1, 141.184448f, 16.727419f, -434.126892f);
            UpdateMarkers(2, 60.375732f, 16.375f, -525.834412f);

            DevDiagnostics.Trace(
                "guildleve.content.loaded",
                "guildleveId", guildleveId,
                "zone", zone.actorId,
                "director", String.Format("0x{0:X}", actorId),
                "groupType", contentGroup == null ? 0 : contentGroup.GetTypeId(),
                "memberCount", contentGroup == null ? 0 : contentGroup.GetMemberCount(),
                "initialObjects", 12,
                "source", "party_battle_leve");
        }

        public void IncludeEligiblePartyMembers()
        {
            Party party = guildleveOwner.currentParty as Party;
            if (party == null)
                return;

            foreach (uint memberId in party.members)
            {
                Player member = Server.GetWorldManager().GetActorInWorld(memberId) as Player;
                if (member != null && member.GetZone() == zone)
                    AddMember(member);
            }
        }

        private void SpawnInitialObject(uint classId, string uniqueId, float x, float y, float z)
        {
            Npc actor = zone.SpawnActor(classId, uniqueId, x, y, z);
            if (actor == null)
            {
                DevDiagnostics.Trace(
                    "guildleve.content.spawnFailed",
                    "guildleveId", guildleveId,
                    "zone", zone.actorId,
                    "classId", classId,
                    "uniqueId", uniqueId);
                return;
            }

            AddMember(actor);
        }

        public void StartGuildleve()
        {
            foreach (Actor p in GetPlayerMembers())
            {
                Player player = (Player) p;

                //Set music
                if (guildleveData.location == 1)
                    player.ChangeMusic(22);
                else if (guildleveData.location == 2)
                    player.ChangeMusic(14);
                else if (guildleveData.location == 3)
                    player.ChangeMusic(26);
                else if (guildleveData.location == 4)
                    player.ChangeMusic(16);

                //Show Start Messages
                player.SendGameMessage(Server.GetWorldManager().GetActor(), 50022, 0x20, guildleveId, selectedDifficulty);
                player.SendDataPacket("attention", Server.GetWorldManager().GetActor(), "", 50022, guildleveId, selectedDifficulty);
                player.SendGameMessage(Server.GetWorldManager().GetActor(), 50026, 0x20, (object)(int)guildleveData.timeLimit);
            }

            guildleveWork.startTime = Utils.UnixTimeStampUTC();
            ActorPropertyPacketUtil propertyBuilder = new ActorPropertyPacketUtil("guildleveWork/start", this);
            propertyBuilder.AddProperty("guildleveWork.startTime");
            SendPacketsToPlayers(propertyBuilder.Done());            
        }

        public void EndGuildleve(bool wasCompleted)
        {
            if (isEnded)
                return;
            isEnded = true;

            completionTime = Utils.UnixTimeStampUTC() - guildleveWork.startTime;

            if (wasCompleted)
            {
                foreach (Actor a in GetPlayerMembers())
                {
                    Player player = (Player)a;
                    player.MarkGuildleve(guildleveId, true, true);
                    player.PlayAnimation(0x02000002, true);
                    player.ChangeMusic(81);
                    player.SendGameMessage(Server.GetWorldManager().GetActor(), 50023, 0x20, (object)(int)guildleveId);
                    player.SendDataPacket("attention", Server.GetWorldManager().GetActor(), "", 50023, (object)(int)guildleveId);
                }
            }

            foreach (Actor a in GetNpcMembers())
            {
                Npc npc = (Npc)a;
                npc.Despawn();
                RemoveMember(a);
            }

            guildleveWork.startTime = 0;
            guildleveWork.signal = -1;
            ActorPropertyPacketUtil propertyBuilder = new ActorPropertyPacketUtil("guildleveWork/signal", this);
            propertyBuilder.AddProperty("guildleveWork.signal");
            propertyBuilder.NewTarget("guildleveWork/start");
            propertyBuilder.AddProperty("guildleveWork.startTime");
            SendPacketsToPlayers(propertyBuilder.Done());
            
            if (wasCompleted)
            {
                Npc aetheryteNode = zone.SpawnActor(1200040, String.Format("{0}:warpExit", guildleveOwner.actorName), guildleveOwner.positionX, guildleveOwner.positionY, guildleveOwner.positionZ);
                AddMember(aetheryteNode);

                foreach (Actor a in GetPlayerMembers())
                {
                    Player player = (Player)a;
                    player.SendGameMessage(Server.GetWorldManager().GetActor(), 50029, 0x20);
                    player.SendGameMessage(Server.GetWorldManager().GetActor(), 50032, 0x20);
                }
            }
        }   
        
        public void AbandonGuildleve()
        {
            foreach (Actor p in GetPlayerMembers())
            {
                Player player = (Player)p;                
                player.SendGameMessage(Server.GetWorldManager().GetActor(), 50147, 0x20, (object)guildleveId);
                player.MarkGuildleve(guildleveId, true, false);
            }

            EndGuildleve(false);
            EndDirector();
        }

        //Delete ContentGroup, change music back
        public void EndGuildleveDirector()
        {            
            foreach (Actor p in GetPlayerMembers())
            {
                Player player = (Player)p;
                player.ChangeMusic(player.GetZone().bgmDay);
            }
        }

        public void SyncAllInfo()
        {
            ActorPropertyPacketUtil propertyBuilder = new ActorPropertyPacketUtil("guildleveWork/infoVariable", this);

            if (guildleveWork.aimNum[0] != 0)
                propertyBuilder.AddProperty("guildleveWork.aimNum[0]");
            if (guildleveWork.aimNum[1] != 0)
                propertyBuilder.AddProperty("guildleveWork.aimNum[1]");
            if (guildleveWork.aimNum[2] != 0)
                propertyBuilder.AddProperty("guildleveWork.aimNum[2]");
            if (guildleveWork.aimNum[3] != 0)
                propertyBuilder.AddProperty("guildleveWork.aimNum[3]");

            if (guildleveWork.aimNumNow[0] != 0)
                propertyBuilder.AddProperty("guildleveWork.aimNumNow[0]");
            if (guildleveWork.aimNumNow[1] != 0)
                propertyBuilder.AddProperty("guildleveWork.aimNumNow[1]");
            if (guildleveWork.aimNumNow[2] != 0)
                propertyBuilder.AddProperty("guildleveWork.aimNumNow[2]");
            if (guildleveWork.aimNumNow[3] != 0)
                propertyBuilder.AddProperty("guildleveWork.aimNumNow[3]");

            if (guildleveWork.uiState[0] != 0)
                propertyBuilder.AddProperty("guildleveWork.uiState[0]");
            if (guildleveWork.uiState[1] != 0)
                propertyBuilder.AddProperty("guildleveWork.uiState[1]");
            if (guildleveWork.uiState[2] != 0)
                propertyBuilder.AddProperty("guildleveWork.uiState[2]");
            if (guildleveWork.uiState[3] != 0)
                propertyBuilder.AddProperty("guildleveWork.uiState[3]");

            SendPacketsToPlayers(propertyBuilder.Done());
        }

        public void UpdateAimNumNow(int index, sbyte value)
        {
            guildleveWork.aimNumNow[index] = value;
            ActorPropertyPacketUtil propertyBuilder = new ActorPropertyPacketUtil("guildleveWork/infoVariable", this);
            propertyBuilder.AddProperty(String.Format("guildleveWork.aimNumNow[{0}]", index));
            SendPacketsToPlayers(propertyBuilder.Done());
        }

        public void UpdateUiState(int index, sbyte value)
        {
            guildleveWork.uiState[index] = value;
            ActorPropertyPacketUtil propertyBuilder = new ActorPropertyPacketUtil("guildleveWork/infoVariable", this);
            propertyBuilder.AddProperty(String.Format("guildleveWork.uiState[{0}]", index));
            SendPacketsToPlayers(propertyBuilder.Done());
        }

        public void UpdateMarkers(int markerIndex, float x, float y, float z)
        {
            guildleveWork.markerX[markerIndex] = x;
            guildleveWork.markerY[markerIndex] = y;
            guildleveWork.markerZ[markerIndex] = z;
            ActorPropertyPacketUtil propertyBuilder = new ActorPropertyPacketUtil("guildleveWork/marker", this);
            propertyBuilder.AddProperty(String.Format("guildleveWork.markerX[{0}]", markerIndex));
            propertyBuilder.AddProperty(String.Format("guildleveWork.markerY[{0}]", markerIndex));
            propertyBuilder.AddProperty(String.Format("guildleveWork.markerZ[{0}]", markerIndex));
            SendPacketsToPlayers(propertyBuilder.Done());
        }

        public void SendPacketsToPlayers(List<SubPacket> packets)
        {
            List<Actor> players = GetPlayerMembers();
            foreach (Actor p in players)
            {
                ((Player)p).QueuePackets(packets);
            }
        }

        public static uint GlBorderIconIDToAnimID(uint iconId)
        {
	        return iconId - 20000;
        }

        public static uint GlPlateIconIDToAnimID(uint iconId)
        {
	        return iconId - 20020;
        }

        public static uint GetGLStartAnimationFromSheet(uint border, uint plate, bool isBoost)
        {
	        return GetGLStartAnimation(GlBorderIconIDToAnimID(border), GlPlateIconIDToAnimID(plate), isBoost);
        }

        public static uint GetGLStartAnimation(uint border, uint plate, bool isBoost)
        {
            uint borderBits = border;
	        uint plateBits = plate << 7;

            uint boostBits = isBoost ? (uint)0x8000 : (uint) 0;
	        
	        return 0x0B000000 | boostBits | plateBits | borderBits;
        }
	
    }
}
