// File: StatsEmbedBuilder.cs  (Server File)
using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using GameServer.Managers;
using GameServer.Files;
using Shared.Packets.Data;

namespace GameServer.Misc
{
    public static class StatsEmbedBuilder
    {
        static readonly string[] _valid =
            { "wealth", "colonistcount", "playtimeseconds", "dayspassed" };

        public static Embed BuildLive(
            int topCount,
            string sortKey,
            Color colour,
            IReadOnlyList<string> cols,
            string titleTpl,
            string footTpl)
        {
            if (topCount <= 0) topCount = 10;

            var key = sortKey.ToLowerInvariant();
            Func<StatisticsData, double> sel = key switch
            {
                "colonistcount"   => s => s._colonistCount,
                "playtimeseconds" => s => s._playtimeSeconds,
                "dayspassed"      => s => s._daysPassed,
                _                 => s => s._wealth
            };

            var rows = StatsManager.GetLiveTop(int.MaxValue)
                                   .OrderByDescending(sel)
                                   .Take(topCount)
                                   .ToList();

            if (rows.Count == 0)
                return Empty(colour, footTpl);

            string label = key switch
            {
                "colonistcount"   => "Colonist Count",
                "playtimeseconds" => "Playtime",
                "dayspassed"      => "Days Passed",
                _                 => "Wealth"
            };

            var wanted = (cols ?? Array.Empty<string>())
                         .Select(c => c.Trim().ToLowerInvariant())
                         .Where(c => _valid.Contains(c))
                         .Distinct()
                         .ToList();

            if (wanted.Count == 0)
                wanted = _valid.ToList();

            var eb = new EmbedBuilder()
                .WithTitle(titleTpl
                    .Replace("{count}", rows.Count.ToString())
                    .Replace("{sortLabel}", label))
                .WithColor(colour)
                .WithFooter(f => f.Text = footTpl
                    .Replace("{timestampUtc}",
                        DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC"));

            for (int i = 0; i < rows.Count; i++)
            {
                var s      = rows[i];
                string med = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => $"{i + 1}." };
                string nm  = UserManagerH.GetUserFileFromName(s._uid)?.Label ?? s._uid;

                var bullets = new List<string>();

                foreach (var c in wanted)
                {
                    switch (c)
                    {
                        case "wealth":
                            bullets.Add($"• **Wealth:** ${s._wealth:N2}");
                            break;
                        case "colonistcount":
                            bullets.Add($"• **Colonists:** {s._colonistCount}");
                            break;
                        case "playtimeseconds":
                            var ts = TimeSpan.FromSeconds(s._playtimeSeconds);
                            bullets.Add($"• **Playtime:** {ts.Hours}h {ts.Minutes}m");
                            break;
                        case "dayspassed":
                            bullets.Add($"• **Days:** {s._daysPassed}");
                            break;
                    }
                }

                eb.AddField($"{med}  {nm}", string.Join('\n', bullets), false);
            }

            return eb.Build();
        }

        public static Embed BuildOnly(int topN, string sortKey)
        {
            if (topN <= 0) topN = 10;

            var key = sortKey.ToLowerInvariant();
            Func<StatisticsData, double> sel = key switch
            {
                "colonistcount"   => s => s._colonistCount,
                "playtimeseconds" => s => s._playtimeSeconds,
                "dayspassed"      => s => s._daysPassed,
                _                 => s => s._wealth
            };

            var rows = StatsManager.GetLiveTop(int.MaxValue)
                                   .OrderByDescending(sel)
                                   .Take(topN)
                                   .ToList();

            if (rows.Count == 0)
                return Empty(Color.DarkerGrey, $"Updated on {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");

            string label = key switch
            {
                "colonistcount"   => "Colonists",
                "playtimeseconds" => "Playtime",
                "dayspassed"      => "Days",
                _                 => "Wealth"
            };

            var eb = new EmbedBuilder()
                .WithTitle($"📊 Top {rows.Count} by {label}")
                .WithColor(Color.Purple)
                .WithFooter(f => f.Text =
                    $"Updated on {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC");

            for (int i = 0; i < rows.Count; i++)
            {
                var s      = rows[i];
                string med = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => $"{i + 1}." };
                string nm  = UserManagerH.GetUserFileFromName(s._uid)?.Label ?? s._uid;

                string metric = key switch
                {
                    "colonistcount"   => s._colonistCount.ToString(),
                    "playtimeseconds" =>
                        $"{TimeSpan.FromSeconds(s._playtimeSeconds).Hours}h " +
                        $"{TimeSpan.FromSeconds(s._playtimeSeconds).Minutes}m",
                    "dayspassed"      => s._daysPassed.ToString(),
                    _                 => $"${s._wealth:N2}"
                };

                eb.AddField($"{med}  {nm}", $"**{label}:** {metric}", false);
            }

            return eb.Build();
        }

        static Embed Empty(Color colour, string footer)
        {
            return new EmbedBuilder()
                .WithTitle("📊 Live Top 0")
                .WithDescription("No data available.")
                .WithColor(colour)
                .WithFooter(f => f.Text = footer)
                .Build();
        }
    }
}