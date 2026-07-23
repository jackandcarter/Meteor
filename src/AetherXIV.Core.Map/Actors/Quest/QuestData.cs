namespace AetherXIV.Core.Map.Actors
{
    /// <summary>
    /// Script-facing quest flags and counters. Values remain stored by Quest,
    /// preserving the existing questData/questFlags database contract.
    /// </summary>
    class QuestData
    {
        private readonly Quest quest;

        public QuestData(Quest quest)
        {
            this.quest = quest;
        }

        public uint GetFlags() => quest.GetQuestFlags();
        public bool GetFlag(int bitIndex) => quest.GetQuestFlag(bitIndex);
        public void SetFlag(int bitIndex) => quest.SetQuestFlag(bitIndex, true);
        public void ClearFlag(int bitIndex) => quest.SetQuestFlag(bitIndex, false);
        public uint GetCounter(int counterIndex) => quest.GetCounter(counterIndex);
        public void SetCounter(int counterIndex, uint value) => quest.SetCounter(counterIndex, value);

        public uint IncCounter(int counterIndex)
        {
            uint value = unchecked(GetCounter(counterIndex) + 1u);
            SetCounter(counterIndex, value);
            return value;
        }

        public uint DecCounter(int counterIndex)
        {
            uint value = unchecked(GetCounter(counterIndex) - 1u);
            SetCounter(counterIndex, value);
            return value;
        }

        public void ClearData()
        {
            quest.ClearQuestData();
            quest.ClearQuestFlags();
        }
    }
}
