using GameClient.Dialogs;
using TCPNetwork.Packets;
using Shared;
using GameClient.Misc;
using Shared.Files.Maps;
using GameClient.Hooks.TCPNetwork;
using TCPNetwork;
using TCPNetwork.Files.Client;

namespace GameClient.PacketManagers
{
    public class PM_Information : PM_Base
    {
        [HandlesPacket(PacketHeader.InformationManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            InformationData data = Serializer.ConvertBytesToObject<InformationData>(bytes);

            switch (data._stepMode)
            {
                case InformationData.InfoStepMode.Connection:
                    ReceiveInformation(data);
                    break;

                case InformationData.InfoStepMode.Wealth:
                    ReceiveWealth(data);
                    break;

                case InformationData.InfoStepMode.Stats:
                    StatisticalManager.ReceiveStats(data);
                    break;

                case InformationData.InfoStepMode.Leaderboard:
                    LeaderboardManager.ReceiveLeaderboard(data);
                    break;
            }
        }

        public static void AskForInformation()
        {
            DLG_Base.PushNewDialog(new DLG_Wait("Waiting for server"));

            InformationData data = new InformationData();
            data._stepMode = InformationData.InfoStepMode.Connection;
            data._settlementTile = SessionHandler.ChosenSettlement.Tile;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.InformationManager, data);
        }

        public static void AskForWealth()
        {
            DLG_Base.PushNewDialog(new DLG_Wait("Waiting for server"));

            InformationData data = new InformationData();
            data._stepMode = InformationData.InfoStepMode.Wealth;
            data._settlementTile = SessionHandler.ChosenSettlement.Tile;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.InformationManager, data);
        }

        public static void ReceiveInformation(InformationData data)
        {
            DLG_Wait.Instance.Close();

            string connectionString = data._isPlayerOnline ? "connected" : "not connected";

            string title = "Information";
            string[] messages = new string[] { $"The player is {connectionString}" };
            DLG_Base.PushNewDialog(new DLG_Message(title, messages));
        }

        public static void ReceiveWealth(InformationData data)
        {
            DLG_Wait.Instance.Close();

            MapFile file = Serializer.ConvertBytesToObject<MapFile>(data._settlementRawData);

            string title = "Information";
            string[] messages = new string[] { $"The wealth of the map is {file.Wealth}" };
            DLG_Base.PushNewDialog(new DLG_Message(title, messages));
        }
    }
}