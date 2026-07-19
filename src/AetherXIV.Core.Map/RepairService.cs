using System;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.actors.chara.player;
using AetherXIV.Core.Map.dataobjects;
using AetherXIV.Core.Map.packets.send.actor.inventory;

namespace AetherXIV.Core.Map
{
    enum RepairResult
    {
        Success = 0,
        Cancelled = 1,
        InvalidItem = 2,
        NotDamaged = 3,
        Unrepairable = 4,
        InsufficientGil = 5,
        StaleSelection = 6,
        DatabaseError = 7,
        Mounted = 8
    }

    readonly struct RepairQuote
    {
        public readonly RepairResult result;
        public readonly uint itemId;
        public readonly int fee;
        public readonly uint targetDurability;

        public RepairQuote(RepairResult result, uint itemId = 0, int fee = 0, uint targetDurability = 0)
        {
            this.result = result;
            this.itemId = itemId;
            this.fee = fee;
            this.targetDurability = targetDurability;
        }
    }

    static class RepairPolicy
    {
        public const int TargetConditionPercent = 99;

        public static bool IsRepairableCatalogId(uint catalogId) =>
            catalogId >= 3900000 && catalogId <= 9999999;

        public static int FeeForLevel(int equippableLevel)
        {
            if (equippableLevel <= 0)
                return 0;
            if (equippableLevel <= 10)
                return 100;
            if (equippableLevel <= 20)
                return 500;
            if (equippableLevel <= 30)
                return 1000;
            if (equippableLevel <= 40)
                return 2100;
            return 5000;
        }

        public static uint TargetDurability(int maximumDurability)
        {
            if (maximumDurability <= 0)
                return 0;
            return (uint)(((long)maximumDurability * TargetConditionPercent) / 100L);
        }
    }

    static class RepairService
    {
        public static int GetCandidateCount(Player player)
        {
            if (player == null)
                return 0;
            ItemPackage package = player.GetItemPackage(ItemPackage.NORMAL);
            int count = 0;
            for (ushort slot = 0; slot < package.GetCount(); slot++)
            {
                LuaUtils.ItemRefParam reference = new LuaUtils.ItemRefParam(
                    player.actorId,
                    0,
                    (byte)slot,
                    (byte)ItemPackage.NORMAL);
                if (QuoteItem(player, reference).result == RepairResult.Success)
                    count++;
            }
            return count;
        }

        public static LuaUtils.ItemRefParam GetCandidate(Player player, int candidateIndex)
        {
            if (player == null || candidateIndex < 0)
                return null;
            ItemPackage package = player.GetItemPackage(ItemPackage.NORMAL);
            int current = 0;
            for (ushort slot = 0; slot < package.GetCount(); slot++)
            {
                LuaUtils.ItemRefParam reference = new LuaUtils.ItemRefParam(
                    player.actorId,
                    0,
                    (byte)slot,
                    (byte)ItemPackage.NORMAL);
                if (QuoteItem(player, reference).result != RepairResult.Success)
                    continue;
                if (current == candidateIndex)
                    return reference;
                current++;
            }
            return null;
        }

        public static RepairQuote QuoteItem(Player player, LuaUtils.ItemRefParam reference)
        {
            if (player == null || reference == null)
                return new RepairQuote(RepairResult.InvalidItem);
            if (player.GetMountState() != 0)
                return new RepairQuote(RepairResult.Mounted);

            InventoryItem item = player.GetItem(reference);
            if (item == null || item.owner != player)
                return new RepairQuote(RepairResult.InvalidItem);

            ItemData data = item.GetItemData();
            if (data == null || !RepairPolicy.IsRepairableCatalogId(data.catalogID)
                || data.durability <= 0 || item.modifiers == null)
                return new RepairQuote(RepairResult.Unrepairable, item.itemId);

            uint target = RepairPolicy.TargetDurability(data.durability);
            if (target == 0)
                return new RepairQuote(RepairResult.Unrepairable, item.itemId);
            if (item.modifiers.durability >= target)
                return new RepairQuote(RepairResult.NotDamaged, item.itemId);

            return new RepairQuote(RepairResult.Success, item.itemId, RepairPolicy.FeeForLevel(data.level), target);
        }

        public static RepairResult TryRepairItem(
            Player player,
            LuaUtils.ItemRefParam reference,
            uint expectedItemId,
            int expectedFee)
        {
            RepairQuote quote = QuoteItem(player, reference);
            if (quote.result != RepairResult.Success)
                return quote.result;
            if (quote.itemId != expectedItemId || quote.fee != expectedFee)
                return RepairResult.StaleSelection;

            InventoryItem item = player.GetItem(reference);
            InventoryItem gil = player.GetItemPackage(ItemPackage.CURRENCY_CRYSTALS).GetItemByCatelogId(1000001);
            if (item == null)
                return RepairResult.StaleSelection;
            if (gil == null || gil.quantity < quote.fee)
                return RepairResult.InsufficientGil;

            uint oldDurability = item.modifiers.durability;
            RepairResult committed = Database.TryRepairItemTransaction(
                player.actorId,
                item.uniqueId,
                item.itemPackage,
                item.slot,
                item.itemId,
                gil.uniqueId,
                quote.fee,
                quote.targetDurability,
                out int remainingGil);
            if (committed != RepairResult.Success)
                return committed;

            item.modifiers.durability = quote.targetDurability;
            gil.quantity = remainingGil;
            ItemPackage itemPackage = player.GetItemPackage(item.itemPackage);
            ItemPackage gilPackage = player.GetItemPackage(gil.itemPackage);
            itemPackage.MarkDirty(item);
            gilPackage.MarkDirty(gil);

            player.QueuePacket(InventoryBeginChangePacket.BuildPacket(player.actorId));
            itemPackage.SendUpdate();
            if (gilPackage != itemPackage)
                gilPackage.SendUpdate();
            player.QueuePacket(InventoryEndChangePacket.BuildPacket(player.actorId));

            // A zero-condition equipped item contributes no parameters. Repair
            // can therefore change the active stat layer immediately.
            if (item.itemPackage == ItemPackage.NORMAL)
                player.RecalculateStats("repair");

            int restoredPercent = item.itemData.durability <= 0
                ? 0
                : (int)(((long)(quote.targetDurability - oldDurability) * 100L) / item.itemData.durability);
            player.SendGameMessage(Server.GetWorldManager().GetActor(), 40125, 0x20, item.itemId, item.quality, restoredPercent);
            return RepairResult.Success;
        }
    }
}
