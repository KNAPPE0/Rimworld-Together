using GameClient.Managers;
using GameClient.Misc;
using RimWorld;
using RimWorld.Planet;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using Verse;
using static Shared.Misc.Printer;

namespace GameClient.PacketManagers
{
    /// <summary>
    /// Client-side marketplace packet handler.
    /// Snapshots refresh the cache; result-on-buy spawns purchased items
    /// and deducts silver from caravan; result-on-list deducts the listed
    /// items from caravan when not sourced from treasury.
    /// </summary>
    public class PM_Marketplace : PM_Base
    {
        [HandlesPacket(PacketHeader.MarketplaceManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Marketplace data = Serializer.ConvertBytesToObject<PKT_Marketplace>(bytes);
            if (data == null) return;

            switch (data.CurrentStep)
            {
                case PKT_Marketplace.StepMode.ListingsSnapshot:
                    MarketplaceClientCache.Apply(data);
                    break;

                case PKT_Marketplace.StepMode.Result:
                    HandleResult(data);
                    break;
            }
        }

        // -- senders --

        public static void RequestListings()
        {
            try
            {
                PKT_Marketplace req = new PKT_Marketplace { CurrentStep = PKT_Marketplace.StepMode.RequestListings };
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.MarketplaceManager, req);
            }
            catch { }
        }

        // KMH 2.7: Pending listing memo — remembers the variant-aware drain
        // request so we can apply it once the server has confirmed the
        // listing succeeded. Without this, the client used to drain caravan
        // stacks BEFORE the server replied; if the server then rejected the
        // listing (max-open cap, validation failure, etc.) the items were
        // permanently lost.
        private class PendingListing
        {
            public string ItemDefName;
            public int Quantity;
            public int QualityIndex;
            public string StuffDefName;
        }

        // Keyed by (defName, quality, stuff) — the same listing key the
        // server echoes back on Result. Multiple listings of the same item
        // shouldn't really collide because the server processes them
        // sequentially per client.
        private static readonly System.Collections.Generic.Queue<PendingListing> PendingListings
            = new System.Collections.Generic.Queue<PendingListing>();

        public static void CreateListing(string itemDefName, int qty, int unitPrice, bool fromTreasury,
            int qualityIndex = 0, string stuffDefName = null)
        {
            try
            {
                PKT_Marketplace req = new PKT_Marketplace
                {
                    CurrentStep = PKT_Marketplace.StepMode.CreateListing,
                    ItemDefName = itemDefName,
                    Quantity = qty,
                    UnitPriceSilver = unitPrice,
                    UseTreasury = fromTreasury,
                    QualityIndex = qualityIndex,
                    StuffDefName = stuffDefName ?? string.Empty
                };
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.MarketplaceManager, req);

                // KMH 2.7: NO LONGER deduct here. We queue a pending memo and
                // wait for ResultKind=ListResult to confirm before draining
                // caravan stock. Treasury-source listings continue to be
                // server-side: the server pulls from treasury inside HandleCreate
                // and refunds itself on failure.
                if (!fromTreasury)
                {
                    PendingListings.Enqueue(new PendingListing
                    {
                        ItemDefName = itemDefName,
                        Quantity = qty,
                        QualityIndex = qualityIndex,
                        StuffDefName = stuffDefName ?? string.Empty
                    });
                }
            }
            catch { }
        }

        /// <summary>
        /// Called from HandleResult when a successful ListResult comes back.
        /// Pops the matching pending memo and drains caravan stock.
        /// </summary>
        internal static void ApplyConfirmedCaravanDrain(string itemDefName, int qty)
        {
            if (PendingListings.Count == 0) return;
            // FIFO — listings are processed in order on the server, so the
            // oldest pending memo matches the oldest received result.
            PendingListing memo = PendingListings.Dequeue();
            if (memo == null) return;

            // Defensive: if the memo somehow doesn't match (race or out-of-order
            // packet), fall back to the values the server returned.
            string defName = !string.IsNullOrEmpty(memo.ItemDefName) ? memo.ItemDefName : itemDefName;
            int quantity = memo.Quantity > 0 ? memo.Quantity : qty;

            // Pull from the player's caravans. Same fallback logic the picker
            // used to populate the "From caravan" view: if SessionHandler has
            // a specific caravan, prefer it; otherwise drain across all.
            List<Caravan> caravans = new List<Caravan>();
            if (SessionHandler.ChosenCaravan != null) caravans.Add(SessionHandler.ChosenCaravan);
            else if (Find.WorldObjects?.Caravans != null)
            {
                foreach (Caravan c in Find.WorldObjects.Caravans)
                    if (c?.Faction != null && c.Faction.IsPlayer) caravans.Add(c);
            }
            if (caravans.Count == 0) return;

            int remaining = quantity;
            foreach (Caravan caravan in caravans)
            {
                if (remaining <= 0) break;
                if (memo.QualityIndex > 0 || !string.IsNullOrEmpty(memo.StuffDefName))
                    remaining -= RemoveMatchingVariantFromCaravan(caravan, defName, remaining, memo.QualityIndex, memo.StuffDefName);
                else
                    remaining -= RemoveStackFromCaravan(caravan, defName, remaining);
            }
        }

        // KMH 2.7: Pull `qty` from the caravan, matching the caller's chosen
        // (qualityIndex, stuffDefName) tuple. Returns the count actually
        // removed so multi-caravan drains can chain.
        private static int RemoveMatchingVariantFromCaravan(Caravan caravan, string itemDefName,
            int qty, int qualityIndex, string stuffDefName)
        {
            if (caravan == null || qty <= 0) return 0;
            int taken = 0;
            int remaining = qty;
            var inv = CaravanInventoryUtility.AllInventoryItems(caravan);
            var matches = new System.Collections.Generic.List<Thing>();
            foreach (Thing t in inv)
            {
                if (t?.def == null || t.def.defName != itemDefName) continue;
                int q = 0;
                if (t.TryGetQuality(out QualityCategory qc)) q = (int)qc;
                string sd = t.Stuff?.defName ?? string.Empty;
                if (q != qualityIndex) continue;
                if ((sd ?? string.Empty) != (stuffDefName ?? string.Empty)) continue;
                matches.Add(t);
            }
            foreach (Thing m in matches)
            {
                if (remaining <= 0) break;
                int take = System.Math.Min(remaining, m.stackCount);
                m.SplitOff(take).Destroy();
                remaining -= take;
                taken += take;
            }
            return taken;
        }

        // KMH 2.7: Bulk-resource path — non-quality, non-stuff items.
        // Returns count taken so the caller can chain across caravans.
        private static int RemoveStackFromCaravan(Caravan caravan, string itemDefName, int qty)
        {
            if (caravan == null || qty <= 0) return 0;
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(itemDefName);
            if (def == null) return 0;

            int have = RimworldManager.GetItemCountInCaravan(caravan, itemDefName);
            int take = System.Math.Min(qty, have);
            if (take <= 0) return 0;

            // Walk stacks and split off rather than relying on holdingOwner.Take
            // (which leaks the returned Thing rather than destroying it).
            int remaining = take;
            var inv = CaravanInventoryUtility.AllInventoryItems(caravan);
            var matches = new System.Collections.Generic.List<Thing>();
            foreach (Thing t in inv)
            {
                if (t?.def == null || t.def.defName != itemDefName) continue;
                matches.Add(t);
            }
            foreach (Thing m in matches)
            {
                if (remaining <= 0) break;
                int n = System.Math.Min(remaining, m.stackCount);
                m.SplitOff(n).Destroy();
                remaining -= n;
            }
            return take - remaining;
        }

        public static void Buy(long listingId, int qty, bool deliverToTreasury)
        {
            try
            {
                PKT_Marketplace req = new PKT_Marketplace
                {
                    CurrentStep = PKT_Marketplace.StepMode.Buy,
                    ListingId = listingId,
                    Quantity = qty,
                    BuyerWantsTreasuryDelivery = deliverToTreasury
                };
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.MarketplaceManager, req);
            }
            catch { }
        }

        public static void CancelListing(long listingId)
        {
            try
            {
                PKT_Marketplace req = new PKT_Marketplace
                {
                    CurrentStep = PKT_Marketplace.StepMode.Cancel,
                    ListingId = listingId
                };
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.MarketplaceManager, req);
            }
            catch { }
        }

        // -- result handling --

        /// <summary>
        /// KMH 2.7: Spawns/deducts ONLY for explicit BuyResult packets.
        /// Previously the same field set (ItemDefName + Quantity) was sent on
        /// both list and buy results, so a CreateListing reply caused the
        /// listed items to spawn back at the seller's home — the "items came
        /// back" bug. Now we gate the spawn behind ResultKind.
        ///
        /// Also: ListResult triggers the deferred caravan drain so we never
        /// destroy items optimistically before the server confirmed the
        /// listing succeeded.
        /// </summary>
        private static void HandleResult(PKT_Marketplace data)
        {
            // List confirmation — drain caravan now that the server has the listing.
            if (data.ResultKind == PKT_Marketplace.ResultKindCode.ListResult
                && data.ListingId > 0
                && data.Quantity > 0
                && !string.IsNullOrEmpty(data.ItemDefName))
            {
                ApplyConfirmedCaravanDrain(data.ItemDefName, data.Quantity);
                // KMH 26.5.20.1: A successful listing may have come from the
                // player's treasury (UseTreasury=true at create time). Even
                // for caravan-sourced listings, the server may have moved
                // overflow items into the treasury — request a fresh snapshot
                // so the open DLG_Treasury (and any other treasury consumer)
                // reflects the new balance immediately instead of waiting
                // for the user to re-open the dialog.
                PM_Treasury.RequestSnapshot();
            }
            // List failure (ListingId==0) — pop the pending memo so we don't
            // accidentally drain on a later success.
            else if (data.ResultKind == PKT_Marketplace.ResultKindCode.ListResult
                && data.ListingId == 0
                && PendingListings.Count > 0)
            {
                PendingListings.Dequeue();
            }

            // KMH 26.5.20.1: CancelResult — server returned the unsold stock
            // to the seller's treasury. Refresh the treasury snapshot so
            // the count updates live.
            if (data.ResultKind == PKT_Marketplace.ResultKindCode.CancelResult)
            {
                PM_Treasury.RequestSnapshot();
            }

            if (data.ResultKind == PKT_Marketplace.ResultKindCode.BuyResult
                && data.Quantity > 0
                && !string.IsNullOrEmpty(data.ItemDefName)
                && !data.BuyerWantsTreasuryDelivery)
            {
                Map map = Find.AnyPlayerHomeMap;
                if (map != null)
                {
                    IntVec3 pos = RimworldManager.GetTransferLocationInMap(map);
                    ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(data.ItemDefName);
                    if (def != null)
                    {
                        Thing t = ThingMaker.MakeThing(def);
                        t.stackCount = data.Quantity;
                        t.HitPoints = def.BaseMaxHitPoints;
                        RimworldManager.PlaceThingIntoMap(t, map, pos, true);
                    }
                }

                // Silver deduction from caravan for the buy.
                int totalCost = data.UnitPriceSilver * data.Quantity;
                Caravan caravan = SessionHandler.ChosenCaravan;
                if (caravan != null && totalCost > 0)
                {
                    if (RimworldManager.CheckIfHasEnoughItemInCaravan(caravan, ThingDefOf.Silver.defName, totalCost))
                        RimworldManager.RemoveThingFromCaravan(caravan, ThingDefOf.Silver, totalCost);
                }
            }

            // KMH 26.5.20.1: BuyResult — the seller's treasury silver went
            // up (proceeds of the sale), and if the buyer requested
            // treasury delivery their treasury got items. Either side that
            // owns this client should refresh its snapshot so the open
            // treasury dialog reflects the change live.
            if (data.ResultKind == PKT_Marketplace.ResultKindCode.BuyResult)
            {
                PM_Treasury.RequestSnapshot();
            }

            if (!string.IsNullOrEmpty(data.Note))
                Printer.Message(data.Note, LogImportanceMode.Verbose);
        }
    }
}
