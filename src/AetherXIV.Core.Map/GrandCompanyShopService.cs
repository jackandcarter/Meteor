using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.actors.chara.player;
using AetherXIV.Core.Map.dataobjects;
using AetherXIV.Core.Map.packets.send.actor.inventory;

namespace AetherXIV.Core.Map
{
    enum GrandCompanyShopResult
    {
        Success = 0,
        InvalidShop = 1,
        WrongGrandCompany = 2,
        RankTooLow = 3,
        InvalidSelection = 4,
        InsufficientSeals = 5,
        AlreadyOwned = 6,
        InventoryFull = 7,
        StaleSelection = 8,
        DatabaseError = 9
    }

    readonly struct GrandCompanyShopPolicyEntry
    {
        public readonly uint actorClassId;
        public readonly byte grandCompany;
        public readonly uint sealItemId;
        public readonly uint chocoboIssuanceItemId;

        public GrandCompanyShopPolicyEntry(
            uint actorClassId,
            byte grandCompany,
            uint sealItemId,
            uint chocoboIssuanceItemId)
        {
            this.actorClassId = actorClassId;
            this.grandCompany = grandCompany;
            this.sealItemId = sealItemId;
            this.chocoboIssuanceItemId = chocoboIssuanceItemId;
        }
    }

    static class GrandCompanyShopPolicy
    {
        public const int ChocoboIssuancePrice = 3000;

        public static bool TryGetShop(uint actorClassId, out GrandCompanyShopPolicyEntry policy)
        {
            switch (actorClassId)
            {
                case 1500202: // Maelstrom
                    policy = new GrandCompanyShopPolicyEntry(actorClassId, 1, 1000201, 2001004);
                    return true;
                case 1500203: // Order of the Twin Adder
                    policy = new GrandCompanyShopPolicyEntry(actorClassId, 2, 1000202, 2001005);
                    return true;
                case 1500201: // Immortal Flames
                    policy = new GrandCompanyShopPolicyEntry(actorClassId, 3, 1000203, 2001006);
                    return true;
                default:
                    policy = default(GrandCompanyShopPolicyEntry);
                    return false;
            }
        }

        public static bool IsExactIssuanceSelection(
            GrandCompanyShopPolicyEntry policy,
            uint itemId,
            int price) =>
            itemId == policy.chocoboIssuanceItemId && price == ChocoboIssuancePrice;
    }

    static class GrandCompanyShopService
    {
        public static GrandCompanyShopResult TryPurchaseChocoboIssuance(
            Player player,
            uint shopActorClassId,
            uint expectedItemId,
            int expectedPrice)
        {
            if (!GrandCompanyShopPolicy.TryGetShop(shopActorClassId, out GrandCompanyShopPolicyEntry shop))
                return GrandCompanyShopResult.InvalidShop;
            if (!GrandCompanyShopPolicy.IsExactIssuanceSelection(shop, expectedItemId, expectedPrice))
                return GrandCompanyShopResult.InvalidSelection;
            if (player.gcCurrent != shop.grandCompany)
                return GrandCompanyShopResult.WrongGrandCompany;
            if (!ChocoboPolicy.IsPrivateThirdClassOrHigher(ChocoboPolicy.GetRank(player, shop.grandCompany)))
                return GrandCompanyShopResult.RankTooLow;
            if (player.hasChocobo)
                return GrandCompanyShopResult.AlreadyOwned;

            ItemPackage keyItems = player.GetItemPackage(ItemPackage.KEYITEMS);
            if (keyItems.GetItemByCatelogId(shop.chocoboIssuanceItemId) != null)
                return GrandCompanyShopResult.AlreadyOwned;
            if (keyItems.IsFull())
                return GrandCompanyShopResult.InventoryFull;

            ItemPackage currency = player.GetItemPackage(ItemPackage.CURRENCY_CRYSTALS);
            InventoryItem seals = currency.GetItemByCatelogId(shop.sealItemId);
            if (seals == null || seals.quantity < GrandCompanyShopPolicy.ChocoboIssuancePrice)
                return GrandCompanyShopResult.InsufficientSeals;

            ushort issuanceSlot = (ushort)keyItems.GetNextEmptySlot();
            GrandCompanyShopResult committed = Database.TryPurchaseChocoboIssuanceTransaction(
                player.actorId,
                shop.grandCompany,
                shop.sealItemId,
                seals.uniqueId,
                shop.chocoboIssuanceItemId,
                issuanceSlot,
                out uint issuanceServerItemId,
                out int remainingSeals);
            if (committed != GrandCompanyShopResult.Success)
                return committed;

            seals.quantity = remainingSeals;
            currency.MarkDirty(seals);
            keyItems.ApplyCommittedAdd(new InventoryItem(
                issuanceServerItemId,
                shop.chocoboIssuanceItemId,
                1,
                1));

            player.QueuePacket(InventoryBeginChangePacket.BuildPacket(player.actorId));
            currency.SendUpdate();
            keyItems.SendUpdate();
            player.QueuePacket(InventoryEndChangePacket.BuildPacket(player.actorId));
            return GrandCompanyShopResult.Success;
        }
    }
}
