using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using Shared;
using Shared.Files;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;

namespace GameServer.PacketManager
{
    public class PM_Information : PM_Base
    {
        [HandlesPacket(PacketHeader.InformationManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Information data = Serializer.ConvertBytesToObject<PKT_Information>(bytes);
            if (data == null) return;

            switch (data._stepMode)
            {
                case PKT_Information.InfoStepMode.Connection:
                    SendInformation(client, data);
                    break;

                case PKT_Information.InfoStepMode.Wealth:
                    SendWealth(client, data);
                    break;

                // KMH: Colony stats feature
                // KMH: Colony stats feature
                case PKT_Information.InfoStepMode.Stats:
                    PM_Statistical.HandleStatsRequest(client, data);
                    break;

                // KMH: Rich leaderboard with sort/pagination
                case PKT_Information.InfoStepMode.Leaderboard:
                    PM_Leaderboard.HandleLeaderboardRequest(client, data);
                    break;
            }
        }

        private static void SendInformation(ServerClient client, PKT_Information data)
        {
            // KMH 26.5.22.1: Guard against the settlement file being missing
            // — a malicious or stale client could ask for a tile that the
            // server never registered, and the old code would NRE on
            // .Username. Mirrors the upstream wealth fix for symmetry.
            SettlementFile settlementToFind = PM_Settlements.GetSettlementFileFromTile(data._settlementTile);
            if (settlementToFind == null)
            {
                ResponseShortcutManager.SendUnavailablePacket(client);
                return;
            }

            ServerClient clientToFind = ServerNetwork.GetConnectedClientFromUsername(settlementToFind.Username);

            data._isPlayerOnline = clientToFind != null ? true : false;

            client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
        }

        private static void SendWealth(ServerClient client, PKT_Information data)
        {
            // KMH 26.5.22.1: Ported from upstream "Fixed issue relating
            // wealth on unsaved maps" (Apr 2026). Without this guard, a
            // client asking for wealth on a tile that the server hasn't
            // saved a map for would NullReferenceException on .Wealth and
            // crash the packet handler. We respond with the standard
            // "unavailable" packet so the client UI can show "—" instead
            // of hanging waiting for a reply.
            if (!PM_Maps.CheckIfMapExists(data._settlementTile))
            {
                ResponseShortcutManager.SendUnavailablePacket(client);
                return;
            }

            data._settlementWealth = PM_Maps.GetMapFromTile(data._settlementTile).Wealth;
            client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
        }
    }
}
