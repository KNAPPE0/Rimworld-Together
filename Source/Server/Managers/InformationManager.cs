using TCPNetwork.Packets;
using Shared;
using Shared.Files;
using TCPNetwork.Files.Client;
using Shared.Files.Maps;

namespace GameServer.Managers
{
    public static class InformationManager
    {
        [HandlesPacket(PacketHeader.InformationManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes, PacketHeader header)
        {
            InformationData data = Serializer.ConvertBytesToObject<InformationData>(bytes);
            if (data == null) return;

            switch (data._stepMode)
            {
                case InformationData.InfoStepMode.Connection:
                    SendInformation(client, data);
                    break;

                case InformationData.InfoStepMode.Wealth:
                    SendWealth(client, data);
                    break;

                case InformationData.InfoStepMode.Stats:
                    StatisticalManager.SendStats(client, data);
                    break;

                case InformationData.InfoStepMode.Leaderboard:
                    LeaderboardManager.SendLeaderboard(client, data);
                    break;
            }
        }

        private static void SendInformation(ServerClient client, InformationData data)
        {
            SettlementFile settlementToFind = SettlementManager.GetSettlementFileFromTile(data._settlementTile);
            if (settlementToFind == null)
            {
                data._isPlayerOnline = false;
                client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
                return;
            }

            ServerClient clientToFind = ServerNetwork.Instance.GetConnectedClientFromUsername(settlementToFind.Username);
            data._isPlayerOnline = clientToFind != null;

            client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
        }

        private static void SendWealth(ServerClient client, InformationData data)
        {
            data._settlementRawData = MapManager.GetMapBytesFromTile(data._settlementTile);

            MapStatsFile stats = MapManager.GetOrCreateMapStatsFromTile(data._settlementTile);
            data._settlementStats = stats;
            data._settlementWealth = stats != null ? stats.Wealth : -1;

            client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
        }
    }
}