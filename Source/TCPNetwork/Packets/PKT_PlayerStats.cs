using System.Collections.Generic;

namespace TCPNetwork.Packets
{
    /// <summary>
    /// KMH 2.7: Player-leaderboard packet. The client requests a snapshot
    /// and the server responds with the per-player lifetime stats summary.
    /// </summary>
    public class PKT_PlayerStats : PKT_Base
    {
        public enum StepMode
        {
            RequestLeaderboard,
            LeaderboardSnapshot
        }

        public StepMode CurrentStep { get; set; } = StepMode.RequestLeaderboard;

        public List<PlayerLeaderboardEntry> Players { get; set; } = new List<PlayerLeaderboardEntry>();
    }

    /// <summary>One row in the global player leaderboard snapshot.</summary>
    public class PlayerLeaderboardEntry
    {
        public string Username { get; set; }
        public string GuildName { get; set; }
        public bool IsLinkedToDiscord { get; set; }
        public long FirstSeenUtcTicks { get; set; }

        public long SilverDonated { get; set; }
        public long SalesEarned { get; set; }
        public long PurchasesSpent { get; set; }
        public int QuestsCompleted { get; set; }
        public int QuestsPosted { get; set; }
        public int MarketplaceSales { get; set; }
        public int SitesBuilt { get; set; }
        public int SitesRaided { get; set; }
        public long WorkerXp { get; set; }
        public long EconomyScore { get; set; }
    }
}
