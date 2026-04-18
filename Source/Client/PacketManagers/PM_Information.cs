using GameClient.Dialogs;
using TCPNetwork.Packets;
using Shared;
using GameClient.Misc;
using TCPNetwork;
using TCPNetwork.Files.Client;
using Shared.Files;
using GameClient.Dialogs.Default;
using TCPNetwork.PacketManagers;

namespace GameClient.PacketManagers
{
    public class PM_Information : PM_Base
    {
        [HandlesPacket(PacketHeader.InformationManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Information data = Serializer.ConvertBytesToObject<PKT_Information>(bytes);

            switch (data._stepMode)
            {
                case PKT_Information.InfoStepMode.Connection:
                    ReceiveInformation(data);
                    break;

                case PKT_Information.InfoStepMode.Wealth:
                    ReceiveWealth(data);
                    break;

                // KMH: Colony stats feature
                case PKT_Information.InfoStepMode.Stats:
                    PM_Statistical.ReceiveStats(data);
                    break;

                // KMH: Rich leaderboard (via InfoStepMode)
                case PKT_Information.InfoStepMode.Leaderboard:
                    PM_Leaderboard.ReceiveLeaderboard(data);
                    break;
            }
        }

        public static void AskForInformation()
        {
            DLG_Base.PushNewDialog(new DLG_Wait());

            PKT_Information data = new PKT_Information();
            data._stepMode = PKT_Information.InfoStepMode.Connection;
            data._settlementTile = SessionHandler.ChosenSettlement.Tile;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.InformationManager, data);
        }

        public static void AskForWealth()
        {
            DLG_Base.PushNewDialog(new DLG_Wait());

            PKT_Information data = new PKT_Information();
            data._stepMode = PKT_Information.InfoStepMode.Wealth;
            data._settlementTile = SessionHandler.ChosenSettlement.Tile;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.InformationManager, data);
        }

        public static void ReceiveInformation(PKT_Information data)
        {
            DLG_Wait.Instance.Close();

            string connectionString = data._isPlayerOnline ? "connected" : "not connected";

            string title = "Information";
            string[] messages = new string[] { $"The player is {connectionString}" };
            DLG_Base.PushNewDialog(new DLG_Message(title, messages));
        }

        public static void ReceiveWealth(PKT_Information data)
        {
            DLG_Wait.Instance.Close();

            string title = "Information";
            string[] messages = new string[] { $"The wealth of the map is {data._settlementWealth}" };
            DLG_Base.PushNewDialog(new DLG_Message(title, messages));
        }
    }
}
