using Shared.Files.Economy;
using System.Collections.Generic;

namespace GameClient.Managers
{
    /// <summary>
    /// Last server snapshot of marketplace listings + house pool stats.
    /// The dialog reads from here. The packet receiver writes to here.
    /// </summary>
    public static class MarketplaceClientCache
    {
        public static List<MarketplaceListing> Listings { get; set; } = new List<MarketplaceListing>();
        public static long HouseSilverPool { get; set; }
        public static long LifetimeTradesCompleted { get; set; }
        public static long LifetimeSilverTraded { get; set; }
        public static bool HasSnapshot { get; set; }

        public static System.Action OnSnapshotUpdated;

        public static void Apply(TCPNetwork.Packets.PKT_Marketplace snap)
        {
            if (snap == null) return;
            Listings = snap.Listings ?? new List<MarketplaceListing>();
            HouseSilverPool = snap.HouseSilverPool;
            LifetimeTradesCompleted = snap.LifetimeTradesCompleted;
            LifetimeSilverTraded = snap.LifetimeSilverTraded;
            HasSnapshot = true;

            try { OnSnapshotUpdated?.Invoke(); }
            catch { }
        }
    }
}
