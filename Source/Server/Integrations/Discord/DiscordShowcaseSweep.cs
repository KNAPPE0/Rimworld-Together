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
    // Deletes !showcase posts older than StaleHours. !showcase resets the timer.
    public static class DiscordShowcaseSweep
    {
        private const int StaleHours = 7 * 24;
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

            // GetAllUserFiles snapshots under UserCacheLock — safe to iterate freely.
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
