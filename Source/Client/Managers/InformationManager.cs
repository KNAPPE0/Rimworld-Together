using GameClient.Dialogs;
using TCPNetwork.Packets;
using Shared;
using GameClient.Misc;

namespace GameClient.Managers
{
    public static class InformationManager
    {
        [HandlesPacket(PacketHeader.InformationManager)]
        private static void ParsePacket(byte[] bytes)
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
            }
        }

        public static void AskForInformation()
        {
            StatisticalManager.AskForStats();
        }

        public static void AskForWealth()
        {
            StatisticalManager.AskForStats();
        }

        public static void ReceiveInformation(InformationData data)
        {
            RT_Dialog_Wait.Instance?.Close();

            string connectionString = data._isPlayerOnline ? "connected" : "not connected";

            string title = "Information";
            string[] messages = new string[] { $"The player is {connectionString}" };
            RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message(title, messages));
        }

        public static void ReceiveWealth(InformationData data)
        {
            RT_Dialog_Wait.Instance?.Close();

            string title = "Information";
            string[] messages = new string[] { $"The wealth of the map is {data._settlementWealth}" };
            RT_Dialog_Base.PushNewDialog(new RT_Dialog_Message(title, messages));
        }
    }
}