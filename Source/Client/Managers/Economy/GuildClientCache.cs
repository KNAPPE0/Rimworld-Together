using Shared.Files.Guilds;

namespace GameClient.Managers
{
    /// <summary>
    /// Last server snapshot of the player's current guild — including
    /// settings, perks, members + contributions.
    /// </summary>
    public static class GuildClientCache
    {
        public static GuildFile Guild { get; set; }
        public static bool HasSnapshot { get; set; }

        public static System.Action OnSnapshotUpdated;

        public static void Apply(TCPNetwork.Packets.PKT_GuildHall snap)
        {
            if (snap?.Guild == null) return;
            Guild = snap.Guild;
            HasSnapshot = true;
            try { OnSnapshotUpdated?.Invoke(); } catch { }
        }
    }
}
