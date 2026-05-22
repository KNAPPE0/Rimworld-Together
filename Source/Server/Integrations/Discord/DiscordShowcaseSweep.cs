using GameServer.Core;
using GameServer.Managers;
using Shared.Misc;
using System;
using System.Threading;
using System.Threading.Tasks;
using TCPNetwork.Files.Client;
using static Shared.Misc.Printer;

namespace GameServer.Integrations.Discord
{
    /// <summary>
    /// KMH 26.5.20: Background sweep that deletes stale `!showcase` posts.
    /// Without this, a player who set up a showcase and then went inactive
    /// would leave a frozen embed in the marketplace channel/forum forever,
    /// cluttering the space and misleading buyers about what's actually
    /// available right now.
    ///
    /// A showcase is considered stale when its last update is older than
    /// <see cref="StaleHours"/>. The sweep runs once per <see cref="SweepIntervalMinutes"/>.
    ///
    /// Edits and refreshes from <c>!showcase</c> reset the timer, so an
    /// active seller never gets surprise-deleted.
    /// </summary>
    public static class DiscordShowcaseSweep
    {
        /// <summary>How old a showcase must be (since last refresh) before
        /// it's deleted.</summary>
        private const int StaleHours = 7 * 24; // 7 days

        /// <summary>How often the sweep runs.</summary>
        private const int SweepIntervalMinutes = 60;

        private static int _started;

        public static void StartFeature()
        {
            // Idempotent — Task.Run can be called multiple times safely.
            if (Interlocked.Exchange(ref _started, 1) == 1) return;
            Task.Run(SweepLoopAsync);
        }

        private static async Task SweepLoopAsync()
        {
            // Initial warmup so Discord + UserManager have time to settle.
            await Task.Delay(TimeSpan.FromMinutes(2));

            while (true)
            {
                try { await SweepOnceAsync(); }
                catch (Exception e) { Printer.Warning($"[ShowcaseSweep] tick failed: {e.Message}"); }

                try { await Task.Delay(TimeSpan.FromMinutes(SweepIntervalMinutes)); }
                catch { await Task.Delay(TimeSpan.FromMinutes(SweepIntervalMinutes)); }
            }
        }

        private static async Task SweepOnceAsync()
        {
            // No point sweeping if the feature is disabled.
            string showcaseChannelStr = Master.ServerConfig?.DiscordMarketplaceForumChannelId;
            if (string.IsNullOrWhiteSpace(showcaseChannelStr)) return;

            long cutoffTicks = DateTime.UtcNow.AddHours(-StaleHours).Ticks;
            int deleted = 0;

            // GetAllUserFiles returns a fresh array snapshot (taken under
            // UserCacheLock as of KMH 26.5.20), so iterating it here is safe
            // even while OnUserFileSaved is mutating the underlying cache on
            // another thread. Each UserFile mutation happens through that
            // same lock, so we read consistent objects.
            foreach (UserFile uf in UserManagerH.GetAllUserFiles())
            {
                if (uf == null) continue;
                if (string.IsNullOrEmpty(uf.DiscordShowcaseChannelId)) continue;
                if (string.IsNullOrEmpty(uf.DiscordShowcaseMessageId)) continue;

                // If a showcase exists but was never timestamped (older
                // UserFile from before we tracked this), grant a one-time
                // grace period by stamping it now instead of deleting.
                if (uf.DiscordShowcaseLastUpdatedUtcTicks == 0)
                {
                    uf.DiscordShowcaseLastUpdatedUtcTicks = DateTime.UtcNow.Ticks;
                    uf.SaveUserFile();
                    continue;
                }

                if (uf.DiscordShowcaseLastUpdatedUtcTicks > cutoffTicks) continue;

                // Stale → delete.
                if (!ulong.TryParse(uf.DiscordShowcaseChannelId, out ulong ch)) { ClearLocally(uf); continue; }
                if (!ulong.TryParse(uf.DiscordShowcaseMessageId, out ulong msg)) { ClearLocally(uf); continue; }

                bool ok = await DiscordBridge.DeleteShowcaseAsync(ch, msg);
                ClearLocally(uf);
                deleted++;

                Printer.Warning($"[ShowcaseSweep] Removed stale showcase for {uf.Username} (deletedOnDiscord={ok})", LogImportanceMode.Verbose);
            }

            if (deleted > 0)
                Printer.Warning($"[ShowcaseSweep] Pruned {deleted} stale showcase(s).");
        }

        private static void ClearLocally(UserFile uf)
        {
            uf.DiscordShowcaseChannelId = null;
            uf.DiscordShowcaseMessageId = null;
            // Keep tagline — the player chose it intentionally, no reason
            // to wipe it when their showcase merely expired.
            uf.DiscordShowcaseLastUpdatedUtcTicks = 0;
            uf.SaveUserFile();
        }
    }
}
