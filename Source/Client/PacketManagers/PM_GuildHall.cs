using GameClient.Managers;
using Shared;
using Shared.Files.Guilds;
using Shared.Misc;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using static Shared.Misc.Printer;

namespace GameClient.PacketManagers
{
    /// <summary>
    /// Client-side guild-hall packet handler. Snapshots refresh the cache;
    /// results print verbose text so the player has a feedback trail.
    /// </summary>
    public class PM_GuildHall : PM_Base
    {
        /// <summary>
        /// Mirrors <see cref="PM_PlayerStats.OnLeaderboardSnapshotUpdated"/>
        /// for the guild leaderboard. Fires when a fresh leaderboard snapshot
        /// has been written to <c>DLG_GuildLeaderboard.CachedRows</c>.
        /// </summary>
        public static event System.Action OnLeaderboardSnapshotUpdated;

        [HandlesPacket(PacketHeader.GuildHallManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_GuildHall data = Serializer.ConvertBytesToObject<PKT_GuildHall>(bytes);
            if (data == null) return;

            switch (data.CurrentStep)
            {
                case PKT_GuildHall.StepMode.Snapshot:
                    GuildClientCache.Apply(data);
                    break;

                case PKT_GuildHall.StepMode.LeaderboardSnapshot:
                    GameClient.Dialogs.Economy.DLG_GuildLeaderboard.CachedRows = data.Leaderboard
                        ?? new System.Collections.Generic.List<GuildLeaderboardEntry>();
                    // Push-notify open dialogs so the "● live" badge
                    // updates immediately and the filter cache rebuilds with
                    // the fresh data, not on next polled refresh.
                    try { OnLeaderboardSnapshotUpdated?.Invoke(); } catch { }
                    break;

                case PKT_GuildHall.StepMode.Result:
                    if (!string.IsNullOrEmpty(data.Note))
                        Printer.Message(data.Note, LogImportanceMode.Verbose);
                    break;
            }
        }

        // -- senders --

        public static void RequestSnapshot()
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildHallManager,
                    new PKT_GuildHall { CurrentStep = PKT_GuildHall.StepMode.RequestSnapshot });
            }
            catch { }
        }

        public static void UpdateSettings(GuildSettings settings)
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildHallManager,
                    new PKT_GuildHall { CurrentStep = PKT_GuildHall.StepMode.UpdateSettings, UpdatedSettings = settings });
            }
            catch { }
        }

        public static void PurchasePerk(int perkKind)
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildHallManager,
                    new PKT_GuildHall { CurrentStep = PKT_GuildHall.StepMode.PurchasePerk, PerkKind = perkKind });
            }
            catch { }
        }

        public static void SetRank(string targetUsername, GuildMember.GuildRanks newRank)
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildHallManager,
                    new PKT_GuildHall
                    {
                        CurrentStep = PKT_GuildHall.StepMode.SetRank,
                        TargetUsername = targetUsername,
                        NewRank = (int)newRank
                    });
            }
            catch { }
        }

        public static void ProposeAlliance(string otherGuildName) =>
            SendDiplomacy(PKT_GuildHall.StepMode.ProposeAlliance, otherGuildName);

        public static void BreakAlliance(string otherGuildName) =>
            SendDiplomacy(PKT_GuildHall.StepMode.BreakAlliance, otherGuildName);

        public static void DeclareHostile(string otherGuildName) =>
            SendDiplomacy(PKT_GuildHall.StepMode.DeclareHostile, otherGuildName);

        private static void SendDiplomacy(PKT_GuildHall.StepMode step, string otherGuildName)
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildHallManager,
                    new PKT_GuildHall { CurrentStep = step, OtherGuildName = otherGuildName ?? string.Empty });
            }
            catch { }
        }

        public static void DistributeBonus(int silverTotal)
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildHallManager,
                    new PKT_GuildHall { CurrentStep = PKT_GuildHall.StepMode.DistributeBonus, BonusSilverTotal = silverTotal });
            }
            catch { }
        }

        public static void RequestLeaderboard()
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.GuildHallManager,
                    new PKT_GuildHall { CurrentStep = PKT_GuildHall.StepMode.RequestLeaderboard });
            }
            catch { }
        }
    }
}
