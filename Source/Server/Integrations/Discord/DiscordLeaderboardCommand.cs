using Discord;
using Discord.WebSocket;
using GameServer.Core;
using GameServer.Managers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameServer.Integrations.Discord
{
    /// <summary>
    /// KMH: !leaderboard / !lb — on-demand top-guilds embed.
    /// Reuses <see cref="DiscordAnnouncer.BuildLeaderboardEmbed"/> so the
    /// formatting matches the auto-poster exactly.
    ///
    /// Usage:
    ///   !leaderboard / !lb                — top N (config) by lifetime silver
    ///   !leaderboard wealth | members | quests | perks | contrib   — alt sort
    /// </summary>
    internal static class DiscordLeaderboardCommand
    {
        public static async Task<bool> TryDispatchAsync(SocketMessage raw, string[] parts)
        {
            if (parts == null || parts.Length == 0) return false;
            string cmd = parts[0].ToLowerInvariant();
            if (cmd != "leaderboard" && cmd != "lb") return false;

            string sortKey = parts.Length >= 2 ? parts[1].ToLowerInvariant() : "wealth";

            try
            {
                List<GuildManager.GuildSummary> rows = GuildManager.ComputeLeaderboard();
                if (rows == null || rows.Count == 0)
                {
                    await raw.Channel.SendMessageAsync(embed: DiscordResponseBuilder.Build(
                        DiscordResponseBuilder.Style.Info, "Top Guilds",
                        "No guilds on this server yet."));
                    return true;
                }

                rows.Sort(GetComparer(sortKey));

                int topCount = Math.Max(1, Master.ServerConfig?.DiscordLeaderboardTopCount ?? 5);
                Embed embed = DiscordAnnouncer.BuildLeaderboardEmbed(rows, topCount);
                if (embed == null)
                {
                    await raw.Channel.SendMessageAsync("No leaderboard data right now.");
                    return true;
                }

                await raw.Channel.SendMessageAsync(embed: embed);
                return true;
            }
            catch (Exception e)
            {
                await raw.Channel.SendMessageAsync(embed: DiscordResponseBuilder.Build(
                    DiscordResponseBuilder.Style.Error, "Leaderboard failed", e.Message));
                return true;
            }
        }

        private static Comparison<GuildManager.GuildSummary> GetComparer(string sortKey)
        {
            switch (sortKey)
            {
                case "members": return (a, b) => b.MemberCount.CompareTo(a.MemberCount);
                case "treasury": return (a, b) => b.TreasurySilver.CompareTo(a.TreasurySilver);
                case "quests": return (a, b) => b.QuestsCompletedByMembers.CompareTo(a.QuestsCompletedByMembers);
                case "perks": return (a, b) => b.TotalPerkLevels.CompareTo(a.TotalPerkLevels);
                case "contrib":
                case "contributions": return (a, b) => b.SilverContributedByMembers.CompareTo(a.SilverContributedByMembers);
                case "wealth":
                default: return (a, b) => b.LifetimeSilverIn.CompareTo(a.LifetimeSilverIn);
            }
        }
    }
}
