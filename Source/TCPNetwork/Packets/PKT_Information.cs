using Shared.Files;

namespace TCPNetwork.Packets
{
    public class PKT_Information : PKT_Base
    {
        public enum InfoStepMode { Connection, Wealth, Stats, Leaderboard }

        public InfoStepMode _stepMode { get; set; } = InfoStepMode.Connection;

        public bool _isPlayerOnline { get; set; } = false;

        public int _settlementTile { get; set; } = -1;

        public int _settlementWealth { get; set; } = -1;

        public byte[] _settlementRawData { get; set; } = null;

        // KMH: Colony stats
        public MapStatsFile _settlementStats { get; set; } = null;

        // KMH: Leaderboard
        public enum LeaderboardSortMode : byte
        {
            Wealth,
            WealthExact,
            Colonists,
            PlaytimeTicks,
            Days,
            SettlementName,
            FactionName,
            LastSavedUtcTicks
        }

        public enum LeaderboardOrder : byte { Desc, Asc }

        public LeaderboardSortMode _leaderboardSort { get; set; } = LeaderboardSortMode.WealthExact;
        public LeaderboardOrder _leaderboardOrder { get; set; } = LeaderboardOrder.Desc;

        public int _leaderboardLimit { get; set; } = 10;
        public int _leaderboardOffset { get; set; } = 0;

        public int _leaderboardTotal { get; set; } = -1;
        public LeaderboardEntryFile[] _leaderboardEntries { get; set; } = null;
    }
}
