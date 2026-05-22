using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using Shared;
using Shared.Files.Guilds;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;

namespace GameServer.PacketManager
{
    /// <summary>
    /// Server-side handler for the Guild Hall surface (settings/perks/ranks).
    /// All actions check the requester's rank via <see cref="GuildManager"/>.
    ///
    /// KMH 2.7 (live leaderboard): Subscribes to
    /// <see cref="PlayerStatsManager.OnStatsChanged"/> and pushes a fresh
    /// guild-leaderboard snapshot to every connected client whenever a tracked
    /// stat changes — guild rankings depend on per-member stats, so the
    /// guild board has to refresh on the same events. Throttled to once per
    /// <see cref="MinBroadcastIntervalMs"/>.
    /// </summary>
    public class PM_GuildHall : PM_Base
    {
        private const int MinBroadcastIntervalMs = 3000;

        private static readonly object BroadcastLock = new object();
        private static long LastBroadcastUtcTicks;
        private static System.Threading.Timer TrailingBroadcastTimer;
        private static bool SubscribedToStatsChanged;

        static PM_GuildHall()
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
                    int waitMs = (int)Math.Max(50, MinBroadcastIntervalMs - sinceMs);
                    TrailingBroadcastTimer?.Dispose();
                    TrailingBroadcastTimer = new System.Threading.Timer(_ =>
                    {
                        lock (BroadcastLock) { LastBroadcastUtcTicks = DateTime.UtcNow.Ticks; }
                        BroadcastLeaderboardToAll();
                    }, null, waitMs, System.Threading.Timeout.Infinite);
                }
            }
            if (fireNow) BroadcastLeaderboardToAll();
        }

        public static void BroadcastLeaderboardToAll()
        {
            try
            {
                PKT_GuildHall packet = BuildLeaderboardPacket();
                foreach (ServerClient sc in ServerNetwork.GetConnectedClients())
                {
                    if (sc?.Listener == null) continue;
                    sc.Listener.EnqueuePacket(PacketHeader.GuildHallManager, packet);
                }
            }
            catch (Exception e)
            {
                Printer.Warning($"[GuildHall] Leaderboard broadcast failed: {e}");
            }
        }

        [HandlesPacket(PacketHeader.GuildHallManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            EnsureSubscribed();

            PKT_GuildHall data = Serializer.ConvertBytesToObject<PKT_GuildHall>(bytes);
            if (data == null) return;

            switch (data.CurrentStep)
            {
                case PKT_GuildHall.StepMode.RequestSnapshot: SendSnapshot(client); break;
                case PKT_GuildHall.StepMode.UpdateSettings: HandleUpdateSettings(client, data); break;
                case PKT_GuildHall.StepMode.PurchasePerk: HandlePurchasePerk(client, data); break;
                case PKT_GuildHall.StepMode.SetRank: HandleSetRank(client, data); break;
                case PKT_GuildHall.StepMode.ProposeAlliance: HandleProposeAlliance(client, data); break;
                case PKT_GuildHall.StepMode.BreakAlliance: HandleBreakAlliance(client, data); break;
                case PKT_GuildHall.StepMode.DeclareHostile: HandleDeclareHostile(client, data); break;
                case PKT_GuildHall.StepMode.DistributeBonus: HandleDistributeBonus(client, data); break;
                case PKT_GuildHall.StepMode.RequestLeaderboard: SendLeaderboardSnapshot(client); break;
            }
        }

        private static void HandleProposeAlliance(ServerClient client, PKT_GuildHall data)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;
            var (ok, note) = GuildManager.ProposeAlliance(username, data.OtherGuildName);
            Reply(client, Result(note));
        }

        private static void HandleBreakAlliance(ServerClient client, PKT_GuildHall data)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;
            var (ok, note) = GuildManager.BreakAlliance(username, data.OtherGuildName);
            Reply(client, Result(note));
        }

        private static void HandleDeclareHostile(ServerClient client, PKT_GuildHall data)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;
            var (ok, note) = GuildManager.DeclareHostile(username, data.OtherGuildName);
            Reply(client, Result(note));
        }

        private static void HandleDistributeBonus(ServerClient client, PKT_GuildHall data)
        {
            string username = client.UserFile?.Username;
            if (string.IsNullOrEmpty(username)) return;
            var (ok, note) = GuildManager.DistributeSilverBonus(username, data.BonusSilverTotal);
            Reply(client, Result(note));
            if (ok && !string.IsNullOrEmpty(client.UserFile?.GuildName))
                BroadcastSnapshotToGuild(client.UserFile.GuildName);
        }

        private static void SendLeaderboardSnapshot(ServerClient client)
        {
            Reply(client, BuildLeaderboardPacket());
        }

        private static PKT_GuildHall BuildLeaderboardPacket()
        {
            List<GameServer.Managers.GuildManager.GuildSummary> rows = GuildManager.ComputeLeaderboard();
            PKT_GuildHall packet = new PKT_GuildHall { CurrentStep = PKT_GuildHall.StepMode.LeaderboardSnapshot };
            foreach (var s in rows)
            {
                packet.Leaderboard.Add(new GuildLeaderboardEntry
                {
                    Name = s.Name,
                    MemberCount = s.MemberCount,
                    TreasurySilver = s.TreasurySilver,
                    LifetimeSilverIn = s.LifetimeSilverIn,
                    TotalPerkLevels = s.TotalPerkLevels,
                    QuestsCompletedByMembers = s.QuestsCompletedByMembers,
                    SilverContributedByMembers = s.SilverContributedByMembers,
                    TotalSites = s.TotalSites,
                    AlliesCount = s.AlliesCount,
                    HostilesCount = s.HostilesCount,
                    AvgMemberTenureDays = s.AvgMemberTenureDays,
                    TotalMemberWorkerXp = s.TotalMemberWorkerXp,
                    TotalMemberSilverEarned = s.TotalMemberSilverEarned,
                    TotalMemberSitesBuilt = s.TotalMemberSitesBuilt
                });
            }
            return packet;
        }

        private static void SendSnapshot(ServerClient client)
        {
            string guildName = client.UserFile?.GuildName;
            if (string.IsNullOrEmpty(guildName))
            {
                Reply(client, Result("You're not in a guild."));
                return;
            }

            GuildFile g = GuildManagerH.GetFactionFromName(guildName);
            if (g == null) { Reply(client, Result("Guild not found.")); return; }

            Reply(client, new PKT_GuildHall
            {
                CurrentStep = PKT_GuildHall.StepMode.Snapshot,
                Guild = g
            });
        }

        public static void BroadcastSnapshotToGuild(string guildName)
        {
            if (string.IsNullOrEmpty(guildName)) return;
            GuildFile g = GuildManagerH.GetFactionFromName(guildName);
            if (g == null) return;

            PKT_GuildHall snap = new PKT_GuildHall { CurrentStep = PKT_GuildHall.StepMode.Snapshot, Guild = g };
            foreach (ServerClient sc in ServerNetwork.GetConnectedClients())
            {
                if (string.Equals(sc.UserFile?.GuildName, guildName, System.StringComparison.OrdinalIgnoreCase))
                    sc.Listener.EnqueuePacket(PacketHeader.GuildHallManager, snap);
            }
        }

        private static void HandleUpdateSettings(ServerClient client, PKT_GuildHall data)
        {
            string username = client.UserFile?.Username;
            string guildName = client.UserFile?.GuildName;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(guildName)) return;

            var (ok, note) = GuildManager.UpdateSettings(username, guildName, data.UpdatedSettings);
            Reply(client, Result(note));
            if (ok) BroadcastSnapshotToGuild(guildName);
        }

        private static void HandlePurchasePerk(ServerClient client, PKT_GuildHall data)
        {
            string username = client.UserFile?.Username;
            string guildName = client.UserFile?.GuildName;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(guildName)) return;

            var (ok, note) = GuildManager.PurchasePerk(username, guildName, (GuildManager.PerkKind)data.PerkKind);
            Reply(client, Result(note));
            if (ok) BroadcastSnapshotToGuild(guildName);
        }

        private static void HandleSetRank(ServerClient client, PKT_GuildHall data)
        {
            string username = client.UserFile?.Username;
            string guildName = client.UserFile?.GuildName;
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(guildName)) return;

            var (ok, note) = GuildManager.SetMemberRank(username, guildName, data.TargetUsername, (GuildMember.GuildRanks)data.NewRank);
            Reply(client, Result(note));
            if (ok) BroadcastSnapshotToGuild(guildName);
        }

        // -- helpers --

        private static PKT_GuildHall Result(string note) =>
            new PKT_GuildHall { CurrentStep = PKT_GuildHall.StepMode.Result, Note = note ?? string.Empty };

        private static void Reply(ServerClient client, PKT_GuildHall packet)
        {
            client.Listener.EnqueuePacket(PacketHeader.GuildHallManager, packet);
        }
    }
}
