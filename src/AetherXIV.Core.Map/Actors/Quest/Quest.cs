using AetherXIV.Core.Common;
using AetherXIV.Core.Map.lua;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using AetherXIV.Core.Map.actors.chara.npc;
using AetherXIV.Core.Map.packets.receive.events;

namespace AetherXIV.Core.Map.Actors
{
    class Quest : Actor
    {
        private Player owner;
        private uint currentPhase = 0;
        private uint questFlags = 0;
        private Dictionary<string, Object> questData = new Dictionary<string, object>();
        private readonly Dictionary<uint, QuestENpc> activeENpcs = new Dictionary<uint, QuestENpc>();
        private Dictionary<uint, QuestENpc> previousENpcs = new Dictionary<uint, QuestENpc>();
        private QuestData scriptData;
        private bool stateInitialized;
        private bool forceAreaPresentationRefresh;

        public Quest(uint actorID, string name)
            : base(actorID)
        {
            actorName = name;
            scriptData = new QuestData(this);
        }

        public Quest(Player owner, uint actorID, string name, string questDataJson, uint questFlags, uint currentPhase)
            : base(actorID)
        {
            this.owner = owner;
            actorName = name;            
            this.questFlags = questFlags;

            if (questDataJson != null)
                this.questData = JsonConvert.DeserializeObject<Dictionary<string, Object>>(questDataJson);
            else
                questData = null;

            if (questData == null)
                questData = new Dictionary<string, object>();

            this.currentPhase = currentPhase;
            scriptData = new QuestData(this);
        }
       
        public void SetQuestData(string dataName, object data)
        {            
                questData[dataName] = data;

            DevDiagnostics.Trace(
                "quest.data",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "dataName", dataName,
                "value", data);
        }

        public uint GetQuestId()
        {
            return actorId & 0xFFFFF;
        }

        public object GetQuestData(string dataName)
        {
            if (questData.ContainsKey(dataName))
                return questData[dataName];
            else
                return null;
        }

        public void ClearQuestData()
        {
            questData.Clear();

            DevDiagnostics.Trace(
                "quest.data",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "action", "clear");
            SaveDataIfOwned();
        }       

        public void ClearQuestFlags()
        {
            uint oldFlags = questFlags;
            questFlags = 0;

            DevDiagnostics.Trace(
                "quest.flags",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "action", "clear",
                "oldFlags", Hex(oldFlags),
                "newFlags", Hex(questFlags));
            SaveDataIfOwned();
        }

        public void SetQuestFlag(int bitIndex, bool value)
        {
            if (bitIndex < 0 || bitIndex >= 32)
            {
                Program.Log.Error("Tried to access bit flag >= 32 for questId: {0}", actorId);
                return;
            }
            
            uint oldFlags = questFlags;

            if (value)
                questFlags |= (uint)(1 << bitIndex);
            else
                questFlags &= (uint)~(1 << bitIndex);

            DevDiagnostics.Trace(
                "quest.flags",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "bitIndex", bitIndex,
                "value", value,
                "oldFlags", Hex(oldFlags),
                "newFlags", Hex(questFlags));

            DoCompletionCheck();
            SaveDataIfOwned();
        }

        public bool GetQuestFlag(int bitIndex)
        {
            if (bitIndex < 0 || bitIndex >= 32)
            {
                Program.Log.Error("Tried to access bit flag >= 32 for questId: {0}", actorId);
                return false;
            }
            else
            return (questFlags & (1 << bitIndex)) == (1 << bitIndex);
        }

        public uint GetPhase()
        {
            return currentPhase;
        }

        public uint GetSequence()
        {
            return currentPhase;
        }

        public uint getSequence()
        {
            return GetSequence();
        }

        public void NextPhase(uint phaseNumber)
        {
            StartSequence(phaseNumber);
        }

        public void StartSequence(uint sequence)
        {
            uint oldPhase = currentPhase;
            currentPhase = sequence;
            DevDiagnostics.Trace(
                "quest.phase",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "oldPhase", oldPhase,
                "newPhase", currentPhase);
            owner.SendGameMessage(Server.GetWorldManager().GetActor(), 25116, 0x20, (object)GetQuestId());
            SaveData();
            RebuildENpcState();
            DoCompletionCheck();
        }

        public void StartSequenceForNpcLs(uint sequence)
        {
            StartSequence(sequence);
        }

        public QuestData GetData()
        {
            return scriptData;
        }

        internal uint GetCounter(int counterIndex)
        {
            if (counterIndex < 0 || counterIndex >= 4)
                return 0;

            return GetQuestDataUInt32("counter" + counterIndex);
        }

        internal void SetCounter(int counterIndex, uint value)
        {
            if (counterIndex < 0 || counterIndex >= 4)
                return;

            SetQuestData("counter" + counterIndex, value);
            SaveDataIfOwned();
        }

        public uint GetQuestFlags()
        {
            return questFlags;
        }

        public string GetSerializedQuestData()
        {
            return JsonConvert.SerializeObject(questData, Formatting.Indented);
        }

        public void SaveData()
        {
            DevDiagnostics.Trace(
                "quest.save",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "phase", currentPhase,
                "flags", Hex(questFlags));
            Database.SaveQuest(owner, this);
        }

        public void DoCompletionCheck()
        {
            List<LuaParam> returned = LuaEngine.GetInstance().CallLuaFunctionForReturn(owner, this, "isObjectivesComplete", true);
            if (returned != null && returned.Count >= 1 && returned[0].typeID == 3)
            {
                owner.SendDataPacket("attention", Server.GetWorldManager().GetActor(), "", 25225, (object)GetQuestId());
                owner.SendGameMessage(Server.GetWorldManager().GetActor(), 25225, 0x20, (object)GetQuestId());	
            }
        }

        public void DoAbandon()
        {
            LuaEngine.GetInstance().CallLuaFunctionForReturn(owner, this, "onAbandonQuest", true);
            owner.SendGameMessage(owner, Server.GetWorldManager().GetActor(), 25236, 0x20, (object)GetQuestId());
        }

        public void SetENpc(
            uint actorClassId,
            byte questFlagType = 0,
            bool isTalkEnabled = true,
            bool isPushEnabled = false,
            bool isEmoteEnabled = false,
            bool isSpawned = false)
        {
            QuestENpc enpc = new QuestENpc(actorClassId, questFlagType, isTalkEnabled,
                isPushEnabled, isEmoteEnabled, isSpawned);

            QuestENpc previous;
            previousENpcs.TryGetValue(actorClassId, out previous);
            previousENpcs.Remove(actorClassId);
            activeENpcs[actorClassId] = enpc;

            if (QuestENpc.ShouldBroadcast(previous, enpc, forceAreaPresentationRefresh))
                BroadcastENpc(enpc, false);
        }

        public void UpdateENPCs(bool forceForAreaChange = false)
        {
            RebuildENpcState(forceForAreaChange);
        }

        public bool HasENpc(uint actorClassId)
        {
            EnsureENpcState();
            return activeENpcs.ContainsKey(actorClassId);
        }

        public QuestENpc GetENpc(uint actorClassId)
        {
            EnsureENpcState();
            QuestENpc enpc;
            activeENpcs.TryGetValue(actorClassId, out enpc);
            return enpc;
        }

        internal bool TryHandleNpcEvent(Player player, Npc npc, EventStartPacket start)
        {
            EnsureENpcState();

            QuestENpc enpc;
            if (!activeENpcs.TryGetValue(npc.GetActorClassId(), out enpc))
                return false;

            string hook;
            switch (start.eventType)
            {
                case 1 when enpc.IsTalkEnabled:
                    hook = "onTalk";
                    break;
                case 2 when enpc.IsPushEnabled:
                    hook = "onPush";
                    break;
                case 3 when enpc.IsEmoteEnabled:
                    hook = "onEmote";
                    break;
                default:
                    return false;
            }

            DevDiagnostics.Trace(
                "quest.event.route",
                "player", player == null ? "" : player.customDisplayName,
                "quest", actorName,
                "questId", GetQuestId(),
                "sequence", currentPhase,
                "npcClassId", npc.GetActorClassId(),
                "npcActor", String.Format("0x{0:X}", npc.actorId),
                "hook", hook);
            LuaEngine.GetInstance().CallLuaFunction(player ?? owner, this, hook, false, npc);
            return true;
        }

        internal void EnsureENpcState()
        {
            if (!stateInitialized)
                RebuildENpcState();
        }

        private void RebuildENpcState(bool forceForAreaChange = false)
        {
            previousENpcs = new Dictionary<uint, QuestENpc>(activeENpcs);
            activeENpcs.Clear();
            stateInitialized = true;

            forceAreaPresentationRefresh = forceForAreaChange;
            DevDiagnostics.Trace(
                "quest.enpc.refresh",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "phase", currentPhase,
                "forceForAreaChange", forceForAreaChange,
                "zone", owner == null ? 0 : owner.zoneId,
                "privateArea", owner == null ? "" : owner.privateArea ?? "",
                "privateAreaType", owner == null ? 0 : owner.privateAreaType);

            try
            {
                LuaEngine.GetInstance().CallLuaFunction(owner, this, "onStateChange", true, currentPhase);
            }
            finally
            {
                forceAreaPresentationRefresh = false;
            }

            foreach (QuestENpc stale in previousENpcs.Values)
                BroadcastENpc(stale, true);
            previousENpcs.Clear();
        }

        private void BroadcastENpc(QuestENpc enpc, bool clear)
        {
            if (owner == null || owner.zone == null)
                return;

            foreach (Npc npc in owner.zone.GetAllActors<Npc>())
            {
                if (npc.GetActorClassId() != enpc.ActorClassId)
                    continue;

                if (npc.eventConditions != null)
                {
                    if (npc.eventConditions.talkEventConditions != null)
                        foreach (var condition in npc.eventConditions.talkEventConditions)
                            owner.SetEventStatus(npc, condition.conditionName, !clear && enpc.IsTalkEnabled, 1);

                    if (npc.eventConditions.pushWithCircleEventConditions != null)
                        foreach (var condition in npc.eventConditions.pushWithCircleEventConditions)
                            owner.SetEventStatus(npc, condition.conditionName, !clear && enpc.IsPushEnabled, 2);

                    if (npc.eventConditions.pushWithFanEventConditions != null)
                        foreach (var condition in npc.eventConditions.pushWithFanEventConditions)
                            owner.SetEventStatus(npc, condition.conditionName, !clear && enpc.IsPushEnabled, 2);

                    if (npc.eventConditions.pushWithBoxEventConditions != null)
                        foreach (var condition in npc.eventConditions.pushWithBoxEventConditions)
                            owner.SetEventStatus(npc, condition.conditionName, !clear && enpc.IsPushEnabled, 2);

                    if (npc.eventConditions.emoteEventConditions != null)
                        foreach (var condition in npc.eventConditions.emoteEventConditions)
                            owner.SetEventStatus(npc, condition.conditionName, !clear && enpc.IsEmoteEnabled, 3);
                }

                npc.SetQuestGraphic(owner, clear ? 0 : enpc.QuestFlagType);
            }
        }

        private void SaveDataIfOwned()
        {
            if (owner != null)
                SaveData();
        }

        public void OnNotice(Player player)
        {
            LuaEngine.GetInstance().CallLuaFunctionForReturn(player ?? owner, this, "onNotice", true);
        }

        public void OnNpcLs(Player player, uint from, uint messageStep)
        {
            LuaEngine.GetInstance().CallLuaFunction(player ?? owner, this, "onNpcLS", false, from, messageStep);
        }

        public uint GetNpcLsFrom()
        {
            return GetQuestDataUInt32("npcLsFrom");
        }

        public uint GetNpcLsMessageStep()
        {
            return GetQuestDataUInt32("npcLsMessageStep");
        }

        public void NewNpcLsMsg(uint from)
        {
            if (!TryGetNpcLinkshellIndex(from, out uint npcLsId))
                return;

            SetQuestData("npcLsFrom", from);
            SetQuestData("npcLsMessageStep", 1u);
            owner.SetNpcLS(npcLsId, Player.NPCLS_ALERT);
            owner.SendGameMessage(Server.GetWorldManager().GetActor(), 25119, 0x20, (object)from);
            SaveData();
        }

        public void ReadNpcLsMsg()
        {
            uint from = GetQuestDataUInt32("npcLsFrom");
            if (!TryGetNpcLinkshellIndex(from, out uint npcLsId))
                return;

            uint step = GetQuestDataUInt32("npcLsMessageStep");
            SetQuestData("npcLsMessageStep", step + 1u);
            owner.SetNpcLS(npcLsId, Player.NPCLS_ACTIVE);
            SaveData();
        }

        public void EndOfNpcLsMsgs()
        {
            uint from = GetQuestDataUInt32("npcLsFrom");
            if (TryGetNpcLinkshellIndex(from, out uint npcLsId))
                owner.SetNpcLS(npcLsId, Player.NPCLS_INACTIVE);

            SetQuestData("npcLsFrom", 0u);
            SetQuestData("npcLsMessageStep", 0u);
            SaveData();
        }

        private uint GetQuestDataUInt32(string key)
        {
            object value = GetQuestData(key);
            if (value == null)
                return 0;

            try
            {
                return Convert.ToUInt32(value);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static bool TryGetNpcLinkshellIndex(uint from, out uint npcLsId)
        {
            // Quest scripts use one-based linkpearl identifiers while
            // playerWork.npcLinkshellChat is a zero-based 64-entry array.
            if (from == 0 || from > 64)
            {
                npcLsId = 0;
                return false;
            }

            npcLsId = from - 1;
            return true;
        }

        private string PlayerName()
        {
            if (owner == null)
                return "";

            if (!String.IsNullOrEmpty(owner.customDisplayName))
                return owner.customDisplayName;

            return owner.GetName();
        }

        private static string Hex(uint value)
        {
            return String.Format("0x{0:X}", value);
        }

    }
}
