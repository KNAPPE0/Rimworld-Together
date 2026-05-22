using System;
using System.Collections.Generic;

namespace Shared.Files.Economy
{
    public class MarketplaceFile
    {
        public List<MarketplaceListing> Listings { get; set; } = new List<MarketplaceListing>();

        // Admin-drainable sink for accumulated marketplace tax.
        public long HouseSilverPool { get; set; }

        public long LifetimeTradesCompleted { get; set; }
        public long LifetimeSilverTraded { get; set; }
    }

    public class MarketplaceListing
    {
        // Server-assigned monotonic id.
        public long Id { get; set; }

        public string SellerUsername { get; set; } = string.Empty;

        // Empty → deliveries go to seller's caravan.
        public string SellerTreasuryKey { get; set; } = string.Empty;

        public string ItemDefName { get; set; } = string.Empty;

        public int RemainingQty { get; set; }

        public int OriginalQty { get; set; }

        public int UnitPriceSilver { get; set; }

        public long ListedUtcTicks { get; set; }

        // 0 = never expires; otherwise unsold stock returns to seller's treasury.
        public long ExpiresUtcTicks { get; set; }

        // System-created listings from sites with RewardDestination=Marketplace.
        public bool IsAutoListing { get; set; }

        // 1=Awful..7=Legendary; 0 = no quality (bulk resources).
        public int QualityIndex { get; set; }

        // Empty for non-stuffable items (Steel, Wood, etc).
        public string StuffDefName { get; set; } = string.Empty;

        public bool IsExpired(long nowTicks) => ExpiresUtcTicks > 0 && nowTicks >= ExpiresUtcTicks;

        public int TotalAskingSilver(int qty) => UnitPriceSilver * Math.Max(0, qty);
    }
}
