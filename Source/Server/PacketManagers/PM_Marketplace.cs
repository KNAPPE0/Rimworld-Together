using GameServer.Managers;
using Shared;
using Shared.Files.Economy;
using Shared.Misc;
using System;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;

namespace GameServer.PacketManager
{
    /// <summary>
    /// Server-side handler for marketplace operations: list / buy / cancel /
    /// snapshot. Stock movement is handled here; silver flows through the
    /// treasury system so audit trails stay consistent.
    /// </summary>
    public class PM_Marketplace : PM_Base
    {
        // KMH: Auto-broadcast to all clients on every marketplace mutation
        // (auto-listings from sites, expirations from sweep, etc.).
        static PM_Marketplace()
        {
            MarketplaceManager.OnMarketplaceChanged += BroadcastListingsToAll;
        }

        [HandlesPacket(PacketHeader.MarketplaceManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Marketplace data = Serializer.ConvertBytesToObject<PKT_Marketplace>(bytes);
            if (data == null) return;

            switch (data.CurrentStep)
            {
                case PKT_Marketplace.StepMode.RequestListings: SendListingsSnapshot(client); break;
                case PKT_Marketplace.StepMode.CreateListing:
                    if (!EconomyAccessGuard.IsAllowed(client))
                    {
                        Reply(client, Result(EconomyAccessGuard.DenyReason));
                        return;
                    }
                    HandleCreate(client, data);
                    break;
                case PKT_Marketplace.StepMode.Buy:
                    if (!EconomyAccessGuard.IsAllowed(client))
                    {
                        Reply(client, Result(EconomyAccessGuard.DenyReason));
                        return;
                    }
                    HandleBuy(client, data);
                    break;
                case PKT_Marketplace.StepMode.Cancel: HandleCancel(client, data); break;
            }
        }

        // -- snapshot --

        public static void SendListingsSnapshot(ServerClient client)
        {
            MarketplaceFile snap = MarketplaceManager.Snapshot();
            PKT_Marketplace reply = new PKT_Marketplace
            {
                CurrentStep = PKT_Marketplace.StepMode.ListingsSnapshot,
                Listings = snap.Listings,
                HouseSilverPool = snap.HouseSilverPool,
                LifetimeTradesCompleted = snap.LifetimeTradesCompleted,
                LifetimeSilverTraded = snap.LifetimeSilverTraded
            };
            Reply(client, reply);
        }

        /// <summary>
        /// KMH: Push a fresh listings snapshot to every connected client.
        /// Cheap because the snapshot is shared (one MarketplaceFile copy).
        /// </summary>
        public static void BroadcastListingsToAll()
        {
            try
            {
                MarketplaceFile snap = MarketplaceManager.Snapshot();
                PKT_Marketplace packet = new PKT_Marketplace
                {
                    CurrentStep = PKT_Marketplace.StepMode.ListingsSnapshot,
                    Listings = snap.Listings,
                    HouseSilverPool = snap.HouseSilverPool,
                    LifetimeTradesCompleted = snap.LifetimeTradesCompleted,
                    LifetimeSilverTraded = snap.LifetimeSilverTraded
                };

                foreach (ServerClient sc in GameServer.Hooks.TCPNetwork.ServerNetwork.GetConnectedClients())
                {
                    if (sc?.Listener == null) continue;
                    sc.Listener.EnqueuePacket(PacketHeader.MarketplaceManager, packet);
                }
            }
            catch (Exception e) { Shared.Misc.Printer.Warning($"[Market] Broadcast failed: {e}"); }
        }

        // -- create --

        private static void HandleCreate(ServerClient client, PKT_Marketplace data)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;

            // If listing from treasury, deduct stock first; on failure abort.
            if (data.UseTreasury)
            {
                string key = TreasuryManager.ResolveKeyForUser(client);
                if (key == null) { Reply(client, Result("No treasury available.")); return; }
                bool isGuild = !key.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
                TreasuryFile t = TreasuryManager.GetOrCreate(key, isGuild);

                if (!TreasuryManager.ClientCanAccessTreasury(client, t, needsWithdrawPermission: true))
                { Reply(client, Result("Permission denied on source treasury.")); return; }

                int taken = TreasuryManager.TryWithdrawItem(t, username, data.ItemDefName, data.Quantity,
                    TreasuryTransaction.TxKind.Withdraw, "marketplace-list");

                if (taken < data.Quantity)
                {
                    // Partial — refund what we took back, abort.
                    if (taken > 0)
                        TreasuryManager.DepositItemForUser(username, data.ItemDefName, taken,
                            TreasuryTransaction.TxKind.MarketplaceRefund, "abort-list");
                    // KMH 2.7: Friendly label so the player doesn't read a raw defName.
                    string itemLabel = GameServer.Managers.ItemLabelCache.LabelFor(data.ItemDefName);
                    Reply(client, Result($"Treasury only has {taken} {itemLabel}, need {data.Quantity}."));
                    return;
                }
            }
            // If not from treasury, the client deducts from caravan client-side (response signals success).

            var (ok, note, listing) = MarketplaceManager.CreateListing(
                username, data.ItemDefName, data.Quantity, data.UnitPriceSilver,
                isAutoListing: false,
                qualityIndex: data.QualityIndex,
                stuffDefName: data.StuffDefName);

            // If creation failed AND we already pulled from treasury, refund.
            if (!ok && data.UseTreasury)
            {
                TreasuryManager.DepositItemForUser(username, data.ItemDefName, data.Quantity,
                    TreasuryTransaction.TxKind.MarketplaceRefund, "list-create-failed");
            }

            Reply(client, new PKT_Marketplace
            {
                CurrentStep = PKT_Marketplace.StepMode.Result,
                ResultKind = PKT_Marketplace.ResultKindCode.ListResult,
                Note = note,
                ListingId = listing?.Id ?? 0,
                ItemDefName = data.ItemDefName,
                Quantity = data.Quantity,
                UnitPriceSilver = data.UnitPriceSilver
            });

            BroadcastListingsToAll();
        }

        // -- buy --

        private static void HandleBuy(ServerClient client, PKT_Marketplace data)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;

            var (ok, note, units, totalSilver, defName) =
                MarketplaceManager.Buy(username, data.ListingId, data.Quantity);

            if (!ok)
            {
                Reply(client, Result(note));
                return;
            }

            // The Result packet tells the client how much silver to deduct + items to deliver.
            Reply(client, new PKT_Marketplace
            {
                CurrentStep = PKT_Marketplace.StepMode.Result,
                ResultKind = PKT_Marketplace.ResultKindCode.BuyResult,
                ListingId = data.ListingId,
                ItemDefName = defName,
                Quantity = units,
                UnitPriceSilver = units > 0 ? totalSilver / units : 0,
                BuyerWantsTreasuryDelivery = data.BuyerWantsTreasuryDelivery,
                Note = note
            });

            // If the buyer wants delivery to their treasury (e.g. they're not in a caravan),
            // deposit on the server side and skip caravan delivery on the client.
            if (data.BuyerWantsTreasuryDelivery)
            {
                TreasuryManager.DepositItemForUser(username, defName, units,
                    TreasuryTransaction.TxKind.Deposit, $"market-buy listing#{data.ListingId}");
            }

            BroadcastListingsToAll();
        }

        private static void HandleCancel(ServerClient client, PKT_Marketplace data)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;

            var (ok, note) = MarketplaceManager.CancelListing(username, data.ListingId);
            Reply(client, new PKT_Marketplace
            {
                CurrentStep = PKT_Marketplace.StepMode.Result,
                ResultKind = PKT_Marketplace.ResultKindCode.CancelResult,
                Note = note,
                ListingId = data.ListingId
            });
            BroadcastListingsToAll();
        }

        // KMH 2.7: Plain status reply with no item payload (e.g. permission
        // denied, generic error). The discriminator stays Unspecified so the
        // client never spawns items off the back of it.
        private static PKT_Marketplace Result(string note) =>
            new PKT_Marketplace { CurrentStep = PKT_Marketplace.StepMode.Result, Note = note };

        private static void Reply(ServerClient client, PKT_Marketplace packet)
        {
            client.Listener.EnqueuePacket(PacketHeader.MarketplaceManager, packet);
        }
    }
}
