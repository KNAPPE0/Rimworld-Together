using Shared;
using Shared.Files;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;

namespace GameServer.Managers
{
    public static class StatisticalManager
    {
        public static void SendStats(ServerClient client, InformationData data)
        {
            try
            {
                string username = string.Empty;
                string settlementName = string.Empty;

                SettlementFile settlementToFind = SettlementManager.GetSettlementFileFromTile(data._settlementTile);
                if (settlementToFind != null)
                {
                    username = settlementToFind.Username ?? string.Empty;
                    settlementName = settlementToFind.Name ?? string.Empty;

                    ServerClient clientToFind = ServerNetwork.Instance.GetConnectedClientFromUsername(username);
                    data._isPlayerOnline = clientToFind != null;
                }
                else
                {
                    data._isPlayerOnline = false;
                }

                MapStatsFile stats = null;

                try
                {
                    stats = MapManager.GetOrCreateMapStatsFromTile(data._settlementTile);
                }
                catch
                {
                }

                if (stats == null)
                {
                    stats = new MapStatsFile();
                    stats.Tile = data._settlementTile;
                    stats.Username = username;
                    stats.SettlementName = settlementName;
                }
                else
                {
                    if (stats.Tile < 0) stats.Tile = data._settlementTile;

                    if (string.IsNullOrWhiteSpace(stats.Username))
                        stats.Username = username;

                    if (!string.IsNullOrWhiteSpace(settlementName))
                        stats.SettlementName = settlementName;
                }

                data._settlementStats = stats;

                client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
            }
            catch
            {
                try
                {
                    data._isPlayerOnline = false;
                    MapStatsFile stats = new MapStatsFile();
                    stats.Tile = data._settlementTile;
                    data._settlementStats = stats;
                    client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
                }
                catch { }
            }
        }
    }
}