using AetherXIV.Core.Common;
using AetherXIV.Core.Map.lua;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace AetherXIV.Core.Map.Actors
{
    class Quest : Actor
    {
        private Player owner;
        private uint currentPhase = 0;
        private uint questFlags = 0;
        private Dictionary<string, Object> questData = new Dictionary<string, object>();

        public Quest(uint actorID, string name)
            : base(actorID)
        {
            actorName = name;            
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
        }

        public void SetQuestFlag(int bitIndex, bool value)
        {
            if (bitIndex >= 32)
            {
                Program.Log.Error("Tried to access bit flag >= 32 for questId: {0}", actorId);
                return;
            }
            
            int mask = 1 << bitIndex;
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
        }

        public bool GetQuestFlag(int bitIndex)
        {
            if (bitIndex >= 32)
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

        public void NextPhase(uint phaseNumber)
        {
            uint oldPhase = currentPhase;
            currentPhase = phaseNumber;
            DevDiagnostics.Trace(
                "quest.phase",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "oldPhase", oldPhase,
                "newPhase", currentPhase);
            owner.SendGameMessage(Server.GetWorldManager().GetActor(), 25116, 0x20, (object)GetQuestId());
            SaveData();

            DoCompletionCheck();
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
