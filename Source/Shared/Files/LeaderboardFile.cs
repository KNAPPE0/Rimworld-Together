using System;
using System.Collections.Generic;

namespace Shared.Files
{
    public class LeaderboardFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        // Legacy: simple username -> score dictionary (kept for backward compatibility)
        public Dictionary<string, double> Scores { get; set; } = new Dictionary<string, double>();

        // KMH: Rich per-settlement entries for detailed leaderboard view
        public LeaderboardEntryFile[] Entries { get; set; } = Array.Empty<LeaderboardEntryFile>();
    }
}
