using System;
using AetherXIV.Core.Common;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.actors.chara.player;
using AetherXIV.Core.Map.dataobjects;
using AetherXIV.Core.Map.packets.send.actor;
using AetherXIV.Core.Map.packets.send.actor.inventory;
using AetherXIV.Core.Map.packets.send.player;

namespace AetherXIV.Core.Map
{
    enum ChocoboRideKind : byte
    {
        None = 0,
        Personal = 1,
        Rental = 2
    }

    enum ChocoboResult
    {
        Success = 0,
        Cancelled = 1,
        InvalidStablemaster = 2,
        LevelTooLow = 3,
        InsufficientGil = 4,
        WrongGrandCompany = 5,
        RankTooLow = 6,
        MissingIssuance = 7,
        InvalidName = 8,
        AlreadyOwned = 9,
        MissingWhistle = 10,
        InvalidZone = 11,
        InCombat = 12,
        Busy = 13,
        DatabaseError = 14,
        InventoryFull = 15
    }

    readonly struct StablemasterPolicy
    {
        public readonly uint actorClassId;
        public readonly byte grandCompany;
        public readonly uint issuanceItemId;
        public readonly byte appearance;

        public StablemasterPolicy(uint actorClassId, byte grandCompany, uint issuanceItemId, byte appearance)
        {
            this.actorClassId = actorClassId;
            this.grandCompany = grandCompany;
            this.issuanceItemId = issuanceItemId;
            this.appearance = appearance;
        }
    }

    static class ChocoboPolicy
    {
        public const int RentalPrice = 800;
        public const byte RentalMinutes = 10;
        public const byte MinimumRentalLevel = 10;
        public const byte PrivateThirdClassRank = 11;
        public const byte RentalAppearance = SetCurrentMountChocoboPacket.CHOCOBO_NORMAL;
        public const uint WhistleItemId = 2001007;

        public static bool TryGetStablemaster(uint actorClassId, out StablemasterPolicy policy)
        {
            switch (actorClassId)
            {
                case 1500006: // Isleen, Maelstrom
                    policy = new StablemasterPolicy(actorClassId, 1, 2001004, 1);
                    return true;
                case 1500061: // Fruhdhem, Twin Adder
                    policy = new StablemasterPolicy(actorClassId, 2, 2001005, 0x1F);
                    return true;
                case 1000840: // Rururaji, Immortal Flames
                    policy = new StablemasterPolicy(actorClassId, 3, 2001006, 0x3D);
                    return true;
                default:
                    policy = default(StablemasterPolicy);
                    return false;
            }
        }

        public static bool IsRentalLevelEligible(int highestClassLevel) => highestClassLevel >= MinimumRentalLevel;

        public static bool IsPrivateThirdClassOrHigher(byte rank) =>
            rank >= PrivateThirdClassRank && rank != 127;

        public static bool IsClientApprovedName(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name.Length > 10)
                return false;
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (!((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '\'' || c == '-'))
                    return false;
            }
            return true;
        }

        public static byte GetRank(Player player, byte grandCompany)
        {
            if (grandCompany == 1)
                return player.gcRankLimsa;
            if (grandCompany == 2)
                return player.gcRankGridania;
            if (grandCompany == 3)
                return player.gcRankUldah;
            return 0;
        }
    }

    static class ChocoboCombatParameters
    {
        // 1.23b numeric constants have not been recovered. Keep every modifier
        // inert until a trace/client-backed evidence revision explicitly enables it.
        public const bool EvidenceBacked = false;
        public const float DamageMultiplier = 1.0f;
        public const float RearHitSpeedLossChance = 0.0f;
        public const int ForcedDismountDamageThreshold = 0;
    }

    static class ChocoboService
    {
        public static ChocoboResult TryIssuePersonal(Player player, uint stablemasterActorClassId, string name)
        {
            if (!ChocoboPolicy.TryGetStablemaster(stablemasterActorClassId, out StablemasterPolicy stablemaster))
                return ChocoboResult.InvalidStablemaster;
            if (player.hasChocobo)
                return ChocoboResult.AlreadyOwned;
            if (player.gcCurrent != stablemaster.grandCompany)
                return ChocoboResult.WrongGrandCompany;
            if (!ChocoboPolicy.IsPrivateThirdClassOrHigher(
                    ChocoboPolicy.GetRank(player, stablemaster.grandCompany)))
                return ChocoboResult.RankTooLow;
            if (!ChocoboPolicy.IsClientApprovedName(name))
                return ChocoboResult.InvalidName;

            ItemPackage keyItems = player.GetItemPackage(ItemPackage.KEYITEMS);
            InventoryItem issuance = keyItems.GetItemByCatelogId(stablemaster.issuanceItemId);
            if (issuance == null || issuance.quantity < 1)
                return ChocoboResult.MissingIssuance;
            if (keyItems.GetItemByCatelogId(ChocoboPolicy.WhistleItemId) != null)
                return ChocoboResult.AlreadyOwned;

            // The issuance is consumed in the same transaction, so even a
            // currently full key-item package always has room for its whistle.
            // The temporary tail slot is shifted down after the issuance row.
            ushort whistleSlot = (ushort)keyItems.GetNextEmptySlot();

            ChocoboResult committed = Database.TryIssueChocoboTransaction(
                player.actorId,
                issuance.uniqueId,
                stablemaster.issuanceItemId,
                issuance.slot,
                whistleSlot,
                stablemaster.appearance,
                name,
                out uint whistleUniqueId);
            if (committed != ChocoboResult.Success)
                return committed;

            keyItems.ApplyCommittedRemove(issuance);
            InventoryItem whistle = new InventoryItem(whistleUniqueId, ChocoboPolicy.WhistleItemId, 1, 1);
            keyItems.ApplyCommittedAdd(whistle);
            player.hasChocobo = true;
            player.chocoboAppearance = stablemaster.appearance;
            player.chocoboName = name;

            player.QueuePacket(InventoryBeginChangePacket.BuildPacket(player.actorId));
            keyItems.SendUpdate();
            player.QueuePacket(InventoryEndChangePacket.BuildPacket(player.actorId));
            player.QueuePacket(SetChocoboNamePacket.BuildPacket(player.actorId, name));
            player.QueuePacket(SetHasChocoboPacket.BuildPacket(player.actorId, true));
            return ChocoboResult.Success;
        }

        public static ChocoboResult TryStartRental(Player player, uint stablemasterActorClassId)
        {
            if (!ChocoboPolicy.TryGetStablemaster(stablemasterActorClassId, out StablemasterPolicy stablemaster))
                return ChocoboResult.InvalidStablemaster;
            if (!ChocoboPolicy.IsRentalLevelEligible(player.GetHighestLevel()))
                return ChocoboResult.LevelTooLow;
            if (player.GetMountState() != 0 || player.currentMainState != SetActorStatePacket.MAIN_STATE_PASSIVE)
                return ChocoboResult.Busy;

            InventoryItem gil = player.GetItemPackage(ItemPackage.CURRENCY_CRYSTALS).GetItemByCatelogId(1000001);
            if (gil == null || gil.quantity < ChocoboPolicy.RentalPrice)
                return ChocoboResult.InsufficientGil;
            ChocoboResult debitResult = Database.TryDebitGilTransaction(
                player.actorId,
                gil.uniqueId,
                ChocoboPolicy.RentalPrice,
                out int remainingGil);
            if (debitResult != ChocoboResult.Success)
                return debitResult;

            gil.quantity = remainingGil;
            ItemPackage currency = player.GetItemPackage(ItemPackage.CURRENCY_CRYSTALS);
            currency.MarkDirty(gil);
            player.QueuePacket(InventoryBeginChangePacket.BuildPacket(player.actorId));
            currency.SendUpdate();
            player.QueuePacket(InventoryEndChangePacket.BuildPacket(player.actorId));

            player.rentalChocoboAppearance = ChocoboPolicy.RentalAppearance;
            player.rentalExpireTime = Utils.UnixTimeStampUTC() + ((uint)ChocoboPolicy.RentalMinutes * 60U);
            player.rentalMinLeft = ChocoboPolicy.RentalMinutes;
            player.chocoboRideKind = ChocoboRideKind.Rental;
            StartRide(player, 64);
            return ChocoboResult.Success;
        }

        public static ChocoboResult TryMountPersonal(Player player, bool stablemasterShortcut)
        {
            if (!player.hasChocobo || !player.HasItem(ChocoboPolicy.WhistleItemId))
                return ChocoboResult.MissingWhistle;
            if (!stablemasterShortcut && (player.GetZone() == null || !player.GetZone().canRideChocobo))
                return ChocoboResult.InvalidZone;
            if (player.aiContainer.IsEngaged() || player.currentMainState == SetActorStatePacket.MAIN_STATE_ACTIVE)
                return ChocoboResult.InCombat;
            if (player.GetMountState() != 0 || player.currentMainState != SetActorStatePacket.MAIN_STATE_PASSIVE)
                return ChocoboResult.Busy;

            player.rentalExpireTime = 0;
            player.rentalMinLeft = 0;
            player.chocoboRideKind = ChocoboRideKind.Personal;
            StartRide(player, 83);
            return ChocoboResult.Success;
        }

        private static void StartRide(Player player, ushort musicId)
        {
            player.ChangeMusic(musicId, AetherXIV.Core.Map.packets.send.SetMusicPacket.EFFECT_FADEIN);
            player.SetMountState(1);
            player.ChangeSpeed(0.0f, 3.6f, 9.0f, 9.0f);
            player.ChangeState(SetActorStatePacket.MAIN_STATE_MOUNTED);
        }

        public static void EndRide(Player player, bool sendPackets = true)
        {
            if (player == null)
                return;
            player.rentalExpireTime = 0;
            player.rentalMinLeft = 0;
            player.rentalChocoboAppearance = ChocoboPolicy.RentalAppearance;
            player.chocoboRideKind = ChocoboRideKind.None;
            if (!sendPackets)
            {
                player.mountState = 0;
                player.ChangeSpeed(0.0f, 2.0f, 5.0f, 5.0f);
                player.ChangeState(SetActorStatePacket.MAIN_STATE_PASSIVE);
                return;
            }
            if (player.GetZone() != null)
                player.ChangeMusic(player.GetZone().bgmDay, AetherXIV.Core.Map.packets.send.SetMusicPacket.EFFECT_FADEIN);
            player.SetMountState(0);
            player.ChangeSpeed(0.0f, 2.0f, 5.0f, 5.0f);
            player.ChangeState(SetActorStatePacket.MAIN_STATE_PASSIVE);
        }
    }
}
