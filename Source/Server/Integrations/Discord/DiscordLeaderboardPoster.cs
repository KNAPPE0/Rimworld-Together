using Discord;
using GameServer.Core;
using GameServer.Managers;
using Shared;
using Shared.Misc;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GameServer.Integrations.Discord
{
    /// <summary>
    /// Live leaderboard poster.
    ///
    /// Behaviour:
    ///   * Posts a single combined embed (Top Guilds + Top Players) to the
    ///     configured leaderboard channel.
    ///   * Re-edits that same Discord message every <c>DiscordLeaderboardIntervalMinutes</c>
    ///     (default 5 min) so the channel doesn't fill up.
    ///   * After <c>DiscordLeaderboardRolloverHours</c> (default 24h) the
    ///     current message is finalised in place ("End of Live Updates")
    ///     and a fresh live embed is posted underneath. This produces a
    ///     daily archive trail similar to game leaderboards in other
    ///     Discord communities.
    ///
    /// State (last live message id + when it was started) is persisted to
    /// disk so a server restart doesn't orphan the live embed.
    /// </summary>
    public static class DiscordLeaderboardPoster
    {
        private static string StatePath => Path.Combine(Master.AssetsPath, "DiscordLeaderboardState.json");

        public class State
        {
            public ulong LiveMessageId { get; set; }
            public long LiveStartedUtcTicks { get; set; }
            public long LastUpdatedUtcTicks { get; set; }
        }

        private static State LoadState()
        {
            try
            {
                if (File.Exists(StatePath))
                    return Serializer.SerializeFromFile<State>(StatePath) ?? new State();
            }
            catch (Exception e) { Printer.Warning($"[DiscordLeaderboard] State load failed: {e}"); }
            return new State();
        }

        private static void SaveState(State s)
        {
            try { Serializer.SerializeToFile(StatePath, s); }
            catch (Exception e) { Printer.Warning($"[DiscordLeaderboard] State save failed: {e}"); }
        }

        public static void StartFeature()
        {
            // Initial 30-second warm-up so other startup work (Discord
            // connection, cache rebuild) settles before we touch the channel.
            Thread.Sleep(TimeSpan.FromSeconds(30));

            State state = LoadState();

            while (true)
            {
                int interval = 5;
                try
                {
                    interval = Math.Max(1, Master.ServerConfig?.DiscordLeaderboardIntervalMinutes ?? 5);
                    int rolloverHours = Math.Max(0, Master.ServerConfig?.DiscordLeaderboardRolloverHours ?? 24);
                    int topCount = Math.Max(1, Master.ServerConfig?.DiscordLeaderboardTopCount ?? 10);

                    var guilds = GuildManager.ComputeLeaderboard();
                    var players = PlayerStatsManager.ComputeLeaderboard();

                    // First-ever post: stamp the start time before building.
                    if (state.LiveStartedUtcTicks == 0)
                        state.LiveStartedUtcTicks = DateTime.UtcNow.Ticks;

                    DateTime liveStartedUtc = new DateTime(state.LiveStartedUtcTicks, DateTimeKind.Utc);
                    bool rolloverDue = rolloverHours > 0 &&
                        (DateTime.UtcNow - liveStartedUtc).TotalHours >= rolloverHours;

                    if (rolloverDue && state.LiveMessageId != 0)
                    {
                        // Step 1: finalise the current live embed in place.
                        Embed finalEmbed = DiscordAnnouncer.BuildCombinedLeaderboardEmbed(
                            guilds, players, topCount,
                            liveStartedUtc, rolloverHours, isFinalised: true);
                        if (finalEmbed != null)
                        {
                            try { _ = DiscordBridge.PostOrEditEmbedAsync(finalEmbed, state.LiveMessageId).GetAwaiter().GetResult(); }
                            catch (Exception fe) { Printer.Warning($"[DiscordLeaderboard] Final-edit failed: {fe.Message}"); }
                        }

                        // Step 2: open a brand-new live embed.
                        state.LiveMessageId = 0;
                        state.LiveStartedUtcTicks = DateTime.UtcNow.Ticks;
                        liveStartedUtc = new DateTime(state.LiveStartedUtcTicks, DateTimeKind.Utc);
                    }

                    Embed liveEmbed = DiscordAnnouncer.BuildCombinedLeaderboardEmbed(
                        guilds, players, topCount,
                        liveStartedUtc, rolloverHours, isFinalised: false);

                    if (liveEmbed != null)
                    {
                        ulong newId = PostOrEditAsync(liveEmbed, state.LiveMessageId).GetAwaiter().GetResult();
                        if (newId != 0)
                        {
                            state.LiveMessageId = newId;
                            state.LastUpdatedUtcTicks = DateTime.UtcNow.Ticks;
                            SaveState(state);
                        }
                    }
                }
                catch (Exception e)
                {
                    Printer.Warning($"[DiscordLeaderboard] Tick failed: {e}");
                }

                try { Thread.Sleep(TimeSpan.FromMinutes(interval)); }
                catch { Thread.Sleep(TimeSpan.FromMinutes(5)); }
            }
        }

        private static async Task<ulong> PostOrEditAsync(Embed embed, ulong existingId)
        {
            try { return await DiscordBridge.PostOrEditEmbedAsync(embed, existingId); }
            catch (Exception e)
            {
                Printer.Warning($"[DiscordLeaderboard] Post/edit threw: {e}");
                return 0;
            }
        }
    }
}
