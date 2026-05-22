using GameServer.Core;
using Shared;
using Shared.Files.Economy;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TCPNetwork.Files.Client;

namespace GameServer.Managers
{
    // Marketplace state — single JSON file fronted by a lock + cache, with an O(1) id index.
    public static class MarketplaceManager
    {
        private static readonly object Lock = new object();
        private static MarketplaceFile _cached;
        private static long _lastExpirySweepTicks;
        private static long _nextListingId = 1;

        // O(1) listing-ID index. Write paths must touch both this and _cached.Listings.
        private static Dictionary<long, MarketplaceListing> _byId;

        // Caps `unitPrice * units` overflow risk; 10_000 is well above any legit buy.
        private const int MaxBuyQty = 10_000;

        // Fires on any state mutation; PM_Marketplace broadcasts snapshot.
        public static event System.Action OnMarketplaceChanged;

        // -- internal index helpers (caller must hold Lock) --

        private static Dictionary<long, MarketplaceListing> ByIdLocked()
        {
            if (_byId != null) return _byId;
            _byId = new Dictionary<long, MarketplaceListing>(_cached?.Listings?.Count ?? 0);
            if (_cached?.Listings != null)
            {
                foreach (MarketplaceListing l in _cached.Listings)
                    if (l != null) _byId[l.Id] = l;
            }
            return _byId;
        }

        private static void IndexAddLocked(MarketplaceListing l)
        {
            if (l == null) return;
            ByIdLocked()[l.Id] = l;
        }

        private static void IndexRemoveLocked(MarketplaceListing l)
        {
            if (l == null || _byId == null) return;
            _byId.Remove(l.Id);
        }

        private static void NotifyChanged()
        {
            try { OnMarketplaceChanged?.Invoke(); }
            catch { }
        }

        // -- bootstrap --

        private static MarketplaceFile Get()
        {
            lock (Lock)
            {
                if (_cached != null) return _cached;

                if (File.Exists(Master.MarketplaceFilePath))
                {
                    try { _cached = Serializer.SerializeFromFile<MarketplaceFile>(Master.MarketplaceFilePath); }
                    catch (Exception e) { Printer.Warning($"[Marketplace] Failed to load: {e}"); }
                }
                if (_cached == null) _cached = new MarketplaceFile();
                if (_cached.Listings == null) _cached.Listings = new List<MarketplaceListing>();

                // Recompute monotonic id counter on first load — cheap with bounded list size.
                _nextListingId = _cached.Listings.Count == 0 ? 1 : _cached.Listings.Max(l => l.Id) + 1;
                // Force rebuild from fresh listings.
                _byId = null;
                return _cached;
            }
        }

        private static void SaveLocked()
        {
            try { Serializer.SerializeToFile(Master.MarketplaceFilePath, _cached); }
            catch (Exception e) { Printer.Warning($"[Marketplace] Save failed: {e}"); }
        }

        // -- expiry sweep --

        private static void SweepExpiriesLocked()
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            // Sweep at most once per 60s to keep cost predictable.
            if (nowTicks - _lastExpirySweepTicks < TimeSpan.FromSeconds(60).Ticks) return;
            _lastExpirySweepTicks = nowTicks;

            // Single pass: refund + index-remove inline, then RemoveAll on the listing list.
            // Previous impl did `_cached.Listings.Remove(l)` in a foreach — O(n) per remove → O(n²).
            HashSet<long> expiredIds = null;
            for (int i = 0; i < _cached.Listings.Count; i++)
            {
                MarketplaceListing l = _cached.Listings[i];
                if (!l.IsExpired(nowTicks)) continue;

                expiredIds ??= new HashSet<long>();
                expiredIds.Add(l.Id);
                IndexRemoveLocked(l);

                if (l.RemainingQty > 0)
                {
                    TreasuryManager.DepositItemForUser(
                        l.SellerUsername, l.ItemDefName, l.RemainingQty,
                        TreasuryTransaction.TxKind.MarketplaceRefund,
                        $"expired-listing#{l.Id}");

                    Printer.Warning($"[Marketplace] Expired listing #{l.Id}: returned {l.RemainingQty} {ItemLabelCache.LabelFor(l.ItemDefName)} to {l.SellerUsername}.");
                }
            }

            if (expiredIds == null) return;

            _cached.Listings.RemoveAll(l => expiredIds.Contains(l.Id));
            SaveLocked();
        }

        // -- snapshot for clients --

        public static MarketplaceFile Snapshot()
        {
            lock (Lock)
            {
                MarketplaceFile m = Get();
                SweepExpiriesLocked();
                return new MarketplaceFile
                {
                    // Shallow copy of the list — entries are immutable from the client's view.
                    Listings = new List<MarketplaceListing>(m.Listings),
                    HouseSilverPool = m.HouseSilverPool,
                    LifetimeTradesCompleted = m.LifetimeTradesCompleted,
                    LifetimeSilverTraded = m.LifetimeSilverTraded
                };
            }
        }

        // -- listing creation --

        public static (bool ok, string note, MarketplaceListing listing) CreateListing(
            string sellerUsername, string itemDefName, int qty, int unitPrice, bool isAutoListing,
            int qualityIndex = 0, string stuffDefName = null)
        {
            if (string.IsNullOrEmpty(sellerUsername)) return (false, "No seller.", null);
            if (string.IsNullOrEmpty(itemDefName)) return (false, "No item.", null);
            if (qty <= 0) return (false, "Quantity must be > 0.", null);

            // Cap defNames — blocks persisted Marketplace.json bloat.
            if (itemDefName.Length > 96) itemDefName = itemDefName.Substring(0, 96);
            if (!string.IsNullOrEmpty(stuffDefName) && stuffDefName.Length > 96)
                stuffDefName = stuffDefName.Substring(0, 96);
            // RimWorld quality enum is 0..6.
            if (qualityIndex < 0 || qualityIndex > 6) qualityIndex = 0;

            var cfg = Master.ActionConfigs?.SiteAction;
            int minPrice = cfg?.MarketplaceMinUnitPrice ?? 1;
            int maxPrice = cfg?.MarketplaceMaxUnitPrice ?? 100_000;
            int maxOpen = cfg?.MarketplaceMaxOpenListingsPerUser ?? 25;
            int lifetimeHours = cfg?.MarketplaceListingLifetimeHours ?? 168;

            if (unitPrice < minPrice) return (false, $"Min unit price is {minPrice}.", null);
            if (unitPrice > maxPrice) return (false, $"Max unit price is {maxPrice}.", null);

            // Cap qty — OriginalQty * UnitPrice arithmetic must fit in int.
            if (qty > 1_000_000) qty = 1_000_000;

            lock (Lock)
            {
                MarketplaceFile m = Get();
                SweepExpiriesLocked();

                int existingFromSeller = m.Listings.Count(l => string.Equals(l.SellerUsername, sellerUsername, StringComparison.OrdinalIgnoreCase));
                if (existingFromSeller >= maxOpen)
                    return (false, $"You already have the max {maxOpen} open listings.", null);

                long now = DateTime.UtcNow.Ticks;
                MarketplaceListing listing = new MarketplaceListing
                {
                    Id = _nextListingId++,
                    SellerUsername = sellerUsername,
                    SellerTreasuryKey = TreasuryManager.ResolveKeyForUsername(sellerUsername) ?? string.Empty,
                    ItemDefName = itemDefName,
                    OriginalQty = qty,
                    RemainingQty = qty,
                    UnitPriceSilver = unitPrice,
                    ListedUtcTicks = now,
                    ExpiresUtcTicks = now + TimeSpan.FromHours(lifetimeHours).Ticks,
                    IsAutoListing = isAutoListing,
                    QualityIndex = qualityIndex,
                    StuffDefName = stuffDefName ?? string.Empty
                };
                m.Listings.Add(listing);
                IndexAddLocked(listing);
                SaveLocked();

                // KMH: Discord announcement for big listings.
                try { GameServer.Integrations.Discord.DiscordAnnouncer.NotableListingPosted(sellerUsername, itemDefName, qty, unitPrice); }
                catch { }

                NotifyChanged();
                return (true, $"Listed {qty}x {itemDefName} @ {unitPrice} silver.", listing);
            }
        }

        public static void AutoListFromSite(string sellerUsername, string itemDefName, int qty, int unitPrice, int siteTile)
        {
            var (ok, note, listing) = CreateListing(sellerUsername, itemDefName, qty, unitPrice, isAutoListing: true);
            if (!ok)
            {
                // If auto-listing fails (eg. seller hit cap), fall back to treasury so items aren't lost.
                Printer.Warning($"[Marketplace] Auto-list from site#{siteTile} fell through ({note}); routing to treasury.");
                TreasuryManager.DepositItemForUser(
                    sellerUsername, itemDefName, qty,
                    TreasuryTransaction.TxKind.SiteRewardItem,
                    $"site#{siteTile} (marketplace fallthrough)");
                return;
            }
            Printer.Warning($"[Marketplace] Auto-listed {qty} {itemDefName} from site#{siteTile} for {sellerUsername} (id #{listing.Id}).");
        }

        // -- purchase --

        public static (bool ok, string note, int unitsBought, int totalSilverPaid, string itemDefName) Buy(
            string buyerUsername, long listingId, int qty)
        {
            if (string.IsNullOrEmpty(buyerUsername)) return (false, "No buyer.", 0, 0, null);
            if (qty <= 0) return (false, "Quantity must be > 0.", 0, 0, null);
            // Clamp client-supplied qty to a sane upper bound BEFORE
            // doing arithmetic. Without this, an int×int totalCost can
            // overflow into negative silver — a real economy exploit. The
            // cap is well above any legit purchase.
            if (qty > MaxBuyQty) qty = MaxBuyQty;

            lock (Lock)
            {
                MarketplaceFile m = Get();
                SweepExpiriesLocked();

                // O(1) lookup via the index. Falls back to a list
                // scan if the index ever got out of sync (defensive).
                ByIdLocked().TryGetValue(listingId, out MarketplaceListing l);
                if (l == null) l = m.Listings.FirstOrDefault(x => x.Id == listingId);
                if (l == null) return (false, "Listing not found or expired.", 0, 0, null);
                if (string.Equals(l.SellerUsername, buyerUsername, StringComparison.OrdinalIgnoreCase))
                    return (false, "You can't buy your own listing.", 0, 0, null);

                int units = Math.Min(qty, l.RemainingQty);
                if (units <= 0) return (false, "Listing is sold out.", 0, 0, null);

                // Compute totalCost in long to detect overflow safely,
                // then bail out cleanly if the result would not fit in int.
                long totalCostLong = (long)l.UnitPriceSilver * units;
                if (totalCostLong > int.MaxValue || totalCostLong < 0)
                    return (false, "Purchase value too large.", 0, 0, null);
                int totalCost = (int)totalCostLong;

                // KMH: House tax % can be reduced by the seller's guild perk.
                int basePct = Math.Max(0, Math.Min(50, Master.ActionConfigs?.SiteAction?.MarketplaceTaxPercent ?? 5));
                TCPNetwork.Files.Client.UserFile sellerUf = UserManagerH.GetUserFileFromName(l.SellerUsername);
                int reduction = sellerUf == null ? 0 : GuildManager.GetMarketplaceTaxReductionPoints(sellerUf.GuildName);
                int taxPercent = Math.Max(0, basePct - reduction);

                int tax = (int)Math.Round(totalCost * (taxPercent / 100.0));
                int sellerNet = totalCost - tax;
                if (sellerNet < 0) sellerNet = 0;

                // KMH: Guild sale tax — skim a configurable cut into the seller's guild treasury.
                sellerNet = GuildManager.ApplyGuildMarketplaceSaleTax(l.SellerUsername, sellerNet);

                // Resolve once and reuse for both the treasury
                // transaction note (shown to seller in their activity log)
                // and the chat reply (shown to the buyer).
                string itemLabel = ItemLabelCache.LabelFor(l.ItemDefName);

                // Pay seller into treasury (silver always goes to treasury; cash-flow consistency).
                TreasuryManager.DepositSilverForUser(
                    l.SellerUsername, sellerNet,
                    TreasuryTransaction.TxKind.MarketplaceSale,
                    $"sold {units}x {itemLabel} (listing#{l.Id})");

                // House tax pool.
                m.HouseSilverPool += tax;
                m.LifetimeTradesCompleted += 1;
                m.LifetimeSilverTraded += totalCost;

                l.RemainingQty -= units;
                if (l.RemainingQty <= 0)
                {
                    m.Listings.Remove(l);
                    IndexRemoveLocked(l);
                }

                SaveLocked();

                // KMH: Discord announcement for big sales.
                try { GameServer.Integrations.Discord.DiscordAnnouncer.NotableSale(l.SellerUsername, buyerUsername, l.ItemDefName, units, totalCost); }
                catch { }

                // Lifetime stats for the player leaderboard.
                try
                {
                    PlayerStatsManager.RecordMarketplaceSale(l.SellerUsername, sellerNet, units);
                    PlayerStatsManager.RecordMarketplacePurchase(buyerUsername, totalCost);
                }
                catch { }

                NotifyChanged();
                return (true, $"Bought {units}x {itemLabel} for {totalCost} silver (tax {tax}).", units, totalCost, l.ItemDefName);
            }
        }

        public static (bool ok, string note) CancelListing(string username, long listingId)
        {
            if (string.IsNullOrEmpty(username)) return (false, "No user.");

            lock (Lock)
            {
                MarketplaceFile m = Get();
                // O(1) listing lookup via the index.
                ByIdLocked().TryGetValue(listingId, out MarketplaceListing l);
                if (l == null) l = m.Listings.FirstOrDefault(x => x.Id == listingId);
                if (l == null) return (false, "Listing not found.");
                if (!string.Equals(l.SellerUsername, username, StringComparison.OrdinalIgnoreCase))
                    return (false, "Only the seller can cancel.");

                int returned = l.RemainingQty;
                m.Listings.Remove(l);
                IndexRemoveLocked(l);
                SaveLocked();

                if (returned > 0)
                {
                    TreasuryManager.DepositItemForUser(
                        username, l.ItemDefName, returned,
                        TreasuryTransaction.TxKind.MarketplaceRefund,
                        $"cancelled-listing#{l.Id}");
                }

                NotifyChanged();
                // Friendly label in the chat confirmation.
                return (true, $"Cancelled listing #{l.Id} (returned {returned}x {ItemLabelCache.LabelFor(l.ItemDefName)} to treasury).");
            }
        }

        /// <summary>Admin sink — drain the house pool to a username's treasury.</summary>
        public static int DrainHousePool(string toUsername)
        {
            lock (Lock)
            {
                MarketplaceFile m = Get();
                long pool = m.HouseSilverPool;
                if (pool <= 0) return 0;
                int silverPart = (int)Math.Min(pool, int.MaxValue);
                m.HouseSilverPool -= silverPart;
                SaveLocked();

                TreasuryManager.DepositSilverForUser(
                    toUsername, silverPart,
                    TreasuryTransaction.TxKind.Deposit,
                    "house-pool-drain");

                return silverPart;
            }
        }
    }
}
