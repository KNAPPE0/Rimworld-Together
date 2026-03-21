using GameServer.Hooks.TCPNetwork;
using GameServer.PacketManagers;
using Shared;
using Shared.Files;
using TCPNetwork;
using TCPNetwork.Files.Client;
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

                case PKT_Information.InfoStepMode.Stats:
                    PM_Statistical.SendStats(client, data);
                    break;

                case PKT_Information.InfoStepMode.Leaderboard:
                    PM_Leaderboard.SendLeaderboard(client, data);
                    break;
            }
        }

        private static void SendInformation(ServerClient client, PKT_Information data)
        {
            SettlementFile settlementToFind = PM_Settlements.GetSettlementFileFromTile(data._settlementTile);
            if (settlementToFind == null)
            {
                data._isPlayerOnline = false;
                client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
                return;
            }

            ServerClient clientToFind = ServerNetwork.GetConnectedClientFromUsername(settlementToFind.Username);
            data._isPlayerOnline = clientToFind != null;

            client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
        }

        private static void SendWealth(ServerClient client, PKT_Information data)
        {
            data._settlementRawData = PM_Maps.GetMapBytesFromTile(data._settlementTile);

            MapStatsFile stats = PM_Maps.GetOrCreateMapStatsFromTile(data._settlementTile);
            data._settlementStats = stats;
            data._settlementWealth = stats != null ? stats.Wealth : -1;

            client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
        }
    }
}