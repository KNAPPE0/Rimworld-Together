using System;
using System.Collections.Generic;

namespace Shared.Files.Economy
{
    /// <summary>
    /// Server-side marketplace state: open listings + accumulated house tax.
    /// Stored as a single file for atomic writes; listings are small.
    /// </summary>
    public class MarketplaceFile
    {
        public List<MarketplaceListing> Listings { get; set; } = new List<MarketplaceListing>();

        /// <summary>Accumulated marketplace tax — admin-drainable sink.</summary>
        public long HouseSilverPool { get; set; }

        /// <summary>Lifetime trade-volume counters for telemetry.</summary>
        public long LifetimeTradesCompleted { get; set; }
        public long LifetimeSilverTraded { get; set; }
    }

    public class MarketplaceListing
    {
        /// <summary>Server-assigned monotonic id. Lets clients reference a listing by id.</summary>
        public long Id { get; set; }

        public string SellerUsername { get; set; } = string.Empty;

        /// <summary>Optional: who hosts this seller's storage. If null/empty, deliveries to seller go to caravan.</summary>
        public string SellerTreasuryKey { get; set; } = string.Empty;

        public string ItemDefName { get; set; } = string.Empty;

        /// <summary>Remaining quantity available. Decreases as buyers purchase units.</summary>
        public int RemainingQty { get; set; }

        /// <summary>Original posted quantity. Useful for displaying "X / Y remaining".</summary>
        public int OriginalQty { get; set; }

        /// <summary>Silver per single unit.</summary>
        public int UnitPriceSilver { get; set; }

        /// <summary>UTC ticks when the listing was created.</summary>
        public long ListedUtcTicks { get; set; }

        /// <summary>UTC ticks at which unsold stock returns to the seller's treasury.</summary>
        public long ExpiresUtcTicks { get; set; }

        /// <summary>True for system-created auto-listings from sites with RewardDestination=Marketplace.</summary>
        public bool IsAutoListing { get; set; }

        // KMH 2.7: RimWorld crafting variants. Optional — empty/0 means
        // "no quality and no stuff", which matches all bulk resources
        // (Steel, Wood, Plasteel, Meals, etc.).

        /// <summary>QualityCategory cast to int (1=Awful, 2=Poor, 3=Normal, 4=Good, 5=Excellent, 6=Masterwork, 7=Legendary). 0 means no quality.</summary>
        public int QualityIndex { get; set; }

        /// <summary>Stuff defName (e.g. "Steel" for a steel knife). Empty for non-stuffable items.</summary>
        public string StuffDefName { get; set; } = string.Empty;

        public bool IsExpired(long nowTicks) => ExpiresUtcTicks > 0 && nowTicks >= ExpiresUtcTicks;

        public int TotalAskingSilver(int qty) => UnitPriceSilver * Math.Max(0, qty);
    }
}
