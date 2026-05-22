using Shared;
using Shared.Misc;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;

namespace GameClient.PacketManagers
{
    /// <summary>
    /// Client-side player-leaderboard packet handler. Caches the
    /// snapshot into <c>DLG_PlayerLeaderboard.CachedRows</c> for the dialog
    /// to render.
    /// </summary>
    public class PM_PlayerStats : PM_Base
    {
        /// <summary>
        /// Fires the moment a fresh leaderboard snapshot has been
        /// applied to <c>DLG_PlayerLeaderboard.CachedRows</c>. The dialog
        /// subscribes to this so it can invalidate its filter/sort cache and
        /// flip the "● live · just now" indicator immediately, instead of
        /// waiting for the next poll tick. Broadcasts are pushed by the
        /// server on stat changes AND on save/autosave receive.
        /// </summary>
        public static event System.Action OnLeaderboardSnapshotUpdated;

        [HandlesPacket(PacketHeader.PlayerStatsManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_PlayerStats data = Serializer.ConvertBytesToObject<PKT_PlayerStats>(bytes);
            if (data == null) return;

            switch (data.CurrentStep)
            {
                case PKT_PlayerStats.StepMode.LeaderboardSnapshot:
                    GameClient.Dialogs.Economy.DLG_PlayerLeaderboard.CachedRows = data.Players
                        ?? new System.Collections.Generic.List<PlayerLeaderboardEntry>();
                    // Notify any open dialogs that a fresh snapshot
                    // landed — wrapped in try/catch so a misbehaving listener
                    // can't poison the packet-receive thread.
                    try { OnLeaderboardSnapshotUpdated?.Invoke(); } catch { }
                    break;
            }
        }

        public static void RequestLeaderboard()
        {
            try
            {
                Network.ServerEndpoint.EnqueuePacket(PacketHeader.PlayerStatsManager,
                    new PKT_PlayerStats { CurrentStep = PKT_PlayerStats.StepMode.RequestLeaderboard });
            }
            catch { }
        }
    }
}
