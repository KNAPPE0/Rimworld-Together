using System;
using System.Globalization;

namespace GameServer.Misc
{
    public static class LeaderboardCommandParser
    {
        public static void Parse(string[] parts, out int limit, out string sortKey)
        {
            limit   = 10;
            sortKey = "wealth";

            if (parts.Length <= 1)
                return;

            if (int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
            {
                limit = n;
                if (parts.Length > 2)
                    sortKey = MapToSortKey(parts[2]);
            }
            else
            {
                sortKey = MapToSortKey(parts[1]);
            }
        }

        private static string MapToSortKey(string token)
        {
            token = token.ToLowerInvariant();
            return token switch
            {
                "days"            => "daysPassed",
                "dayspassed"      => "daysPassed",
                "playtime"        => "playtimeSeconds",
                "playtimeseconds" => "playtimeSeconds",
                "colonists"       => "colonistCount",
                "colonistcount"   => "colonistCount",
                "wealth"          => "wealth",
                _                 => "wealth"
            };
        }
    }
}