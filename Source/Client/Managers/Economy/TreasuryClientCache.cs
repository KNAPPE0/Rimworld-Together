using Shared.Files.Economy;
using System.Collections.Generic;

namespace GameClient.Managers
{
    /// <summary>
    /// Last server snapshot of the player's currently-viewed treasury.
    /// The dialog reads from here. The packet receiver writes to here.
    /// </summary>
    public static class TreasuryClientCache
    {
        public static string OwnerKey { get; set; } = string.Empty;
        public static bool IsGuildOwned { get; set; }
        public static int SilverBalance { get; set; }
        public static Dictionary<string, int> Items { get; set; } = new Dictionary<string, int>();
        public static long LifetimeSilverIn { get; set; }
        public static long LifetimeSilverOut { get; set; }
        public static List<TreasuryTransaction> RecentTransactions { get; set; } = new List<TreasuryTransaction>();
        public static bool CanDeposit { get; set; }
        public static bool CanWithdraw { get; set; }
        public static bool HasSnapshot { get; set; }

        /// <summary>Set when a snapshot arrives so the open dialog can refresh.</summary>
        public static System.Action OnSnapshotUpdated;

        public static void Apply(TCPNetwork.Packets.PKT_Treasury snap)
        {
            if (snap == null) return;
            OwnerKey = snap.OwnerKey ?? string.Empty;
            IsGuildOwned = snap.IsGuildOwned;
            SilverBalance = snap.SilverAmount;
            Items = snap.ItemBundle ?? new Dictionary<string, int>();
            LifetimeSilverIn = snap.LifetimeSilverIn;
            LifetimeSilverOut = snap.LifetimeSilverOut;
            RecentTransactions = snap.RecentTransactions ?? new List<TreasuryTransaction>();
            CanDeposit = snap.CanDeposit;
            CanWithdraw = snap.CanWithdraw;
            HasSnapshot = true;

            try { OnSnapshotUpdated?.Invoke(); }
            catch { }
        }
    }
}
