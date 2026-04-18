using GameServer.Hooks.TCPNetwork;
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
            SettlementFile settlementToFind = PM_Settlements.GetSettlementFileFromTile(data._settlementTile);
            ServerClient clientToFind = ServerNetwork.GetConnectedClientFromUsername(settlementToFind.Username);

            data._isPlayerOnline = clientToFind != null ? true : false;

            client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
        }

        private static void SendWealth(ServerClient client, PKT_Information data)
        {
            data._settlementWealth = PM_Maps.GetMapFromTile(data._settlementTile).Wealth;
            client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
        }
    }
}
