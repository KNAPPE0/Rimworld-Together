namespace Shared.Files
{
    public class LeaderboardEntryFile
    {
        public int Rank { get; set; } = -1;

        public int Tile { get; set; } = -1;

        public string Username { get; set; } = string.Empty;

        public string SettlementName { get; set; } = string.Empty;

        public string FactionName { get; set; } = string.Empty;

        public int Wealth { get; set; } = -1;

        public double WealthExact { get; set; } = -1;

        public int ColonistCount { get; set; } = -1;

        public int GameTicks { get; set; } = -1;

        public double RealPlayTimeInteractingSeconds { get; set; } = -1;

        public long LastSavedUtcTicks { get; set; } = 0;
    }
}