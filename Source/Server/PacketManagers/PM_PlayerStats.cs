using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;

namespace GameServer.PacketManager
{
    /// <summary>
    /// Server-side handler for the per-player leaderboard.
    /// Reads <see cref="PlayerStatsManager.ComputeLeaderboard"/> and packs
    /// the results into a <see cref="PKT_PlayerStats"/> for the requester.
    ///
    /// KMH 2.7 (live updates): Also subscribes to
    /// <see cref="PlayerStatsManager.OnStatsChanged"/> and pushes a fresh
    /// snapshot to every connected client whenever a tracked stat ticks.
    /// Throttled to once per <see cref="MinBroadcastIntervalMs"/> so a
    /// burst of events doesn't flood the wire — the trailing event still
    /// fires on a delayed timer so the final state is always pushed.
    /// </summary>
    public class PM_PlayerStats : PM_Base
    {
        // Min gap between broadcasts. Sites that record XP every few
        // seconds shouldn't spam every connected client every few seconds.
        private const int MinBroadcastIntervalMs = 3000;

        private static readonly object BroadcastLock = new object();
        private static long LastBroadcastUtcTicks;
        private static System.Threading.Timer TrailingBroadcastTimer;

        // Idempotent guard so re-construction (e.g. PM_Base reflection scan)
        // doesn't double-subscribe.
        private static bool SubscribedToStatsChanged;

        static PM_PlayerStats()
        {
            EnsureSubscribed();
        }

        private static void EnsureSubscribed()
        {
            if (SubscribedToStatsChanged) return;
            SubscribedToStatsChanged = true;
            PlayerStatsManager.OnStatsChanged += OnStatsChanged;
        }

        private static void OnStatsChanged()
        {
            long now = DateTime.UtcNow.Ticks;
            long sinceMs;
            bool fireNow;
            lock (BroadcastLock)
            {
                sinceMs = (now - LastBroadcastUtcTicks) / TimeSpan.TicksPerMillisecond;
                fireNow = LastBroadcastUtcTicks == 0 || sinceMs >= MinBroadcastIntervalMs;

                if (fireNow)
                {
                    LastBroadcastUtcTicks = now;
                }
                else
                {
                    // Schedule a trailing broadcast so the *last* event in a
                    // burst still gets out. Reset the timer each call.
                    int waitMs = (int)Math.Max(50, MinBroadcastIntervalMs - sinceMs);
                    TrailingBroadcastTimer?.Dispose();
                    TrailingBroadcastTimer = new System.Threading.Timer(_ =>
                    {
                        lock (BroadcastLock) { LastBroadcastUtcTicks = DateTime.UtcNow.Ticks; }
                        BroadcastSnapshotToAll();
                    }, null, waitMs, System.Threading.Timeout.Infinite);
                }
            }

            if (fireNow) BroadcastSnapshotToAll();
        }

        public static void BroadcastSnapshotToAll()
        {
            try
            {
                PKT_PlayerStats packet = BuildSnapshotPacket();
                foreach (ServerClient sc in ServerNetwork.GetConnectedClients())
                {
                    if (sc?.Listener == null) continue;
                    sc.Listener.EnqueuePacket(PacketHeader.PlayerStatsManager, packet);
                }
            }
            catch (Exception e)
            {
                Printer.Warning($"[PlayerStats] Broadcast failed: {e}");
            }
        }

        [HandlesPacket(PacketHeader.PlayerStatsManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            // Defensive: ensure we're subscribed (PM_Base may reflectively
            // construct us after Initialize() runs).
            EnsureSubscribed();

            PKT_PlayerStats data = Serializer.ConvertBytesToObject<PKT_PlayerStats>(bytes);
            if (data == null) return;

            switch (data.CurrentStep)
            {
                case PKT_PlayerStats.StepMode.RequestLeaderboard:
                    SendSnapshot(client);
                    break;
            }
        }

        private static void SendSnapshot(ServerClient client)
        {
            client.Listener.EnqueuePacket(PacketHeader.PlayerStatsManager, BuildSnapshotPacket());
        }

        private static PKT_PlayerStats BuildSnapshotPacket()
        {
            List<PlayerStatsManager.PlayerSummary> rows = PlayerStatsManager.ComputeLeaderboard();
            PKT_PlayerStats packet = new PKT_PlayerStats { CurrentStep = PKT_PlayerStats.StepMode.LeaderboardSnapshot };
            foreach (var s in rows)
            {
                packet.Players.Add(new PlayerLeaderboardEntry
                {
                    Username = s.Username,
                    GuildName = s.GuildName,
                    IsLinkedToDiscord = s.IsLinkedToDiscord,
                    FirstSeenUtcTicks = s.FirstSeenUtcTicks,
                    SilverDonated = s.SilverDonated,
                    SalesEarned = s.SalesEarned,
                    PurchasesSpent = s.PurchasesSpent,
                    QuestsCompleted = s.QuestsCompleted,
                    QuestsPosted = s.QuestsPosted,
                    MarketplaceSales = s.MarketplaceSales,
                    SitesBuilt = s.SitesBuilt,
                    SitesRaided = s.SitesRaided,
                    WorkerXp = s.WorkerXp,
                    EconomyScore = s.TotalEconomyScore
                });
            }
            return packet;
        }
    }
}
