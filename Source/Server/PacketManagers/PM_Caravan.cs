using GameServer.Hooks.TCPNetwork;
using GameServer.Misc;
using Shared;
using Shared.Files;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameServer.PacketManager
{
    public class PM_Caravan : PM_Base
    {
        [HandlesPacket(PacketHeader.CaravanManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Caravan data = Serializer.ConvertBytesToObject<PKT_Caravan>(bytes);

            switch (data._stepMode)
            {
                case CaravanStepMode.Add:
                    AddCaravan(client, data._caravanFile);
                    break;

                case CaravanStepMode.Remove:
                    RemoveCaravan(client, data._caravanFile);
                    break;

                case CaravanStepMode.Move:
                    MoveCaravan(client, data._caravanFile);
                    break;
            }
        }

        private static void AddCaravan(ServerClient client, CaravanFile file)
        {
            PKT_Caravan data = new PKT_Caravan();
            data._stepMode = CaravanStepMode.Add;
            data._caravanFile = file;

            ServerNetwork.SendPacketToAllClients(PacketHeader.CaravanManager, data, client);

            InformationDisplayer.DisplayAddCaravan(client.UserFile.Username);
        }

        public static void RemoveCaravan(ServerClient client, CaravanFile file)
        {
            PKT_Caravan data = new PKT_Caravan();
            data._stepMode = CaravanStepMode.Remove;
            data._caravanFile = file;

            ServerNetwork.SendPacketToAllClients(PacketHeader.CaravanManager, data, client);

            InformationDisplayer.DisplayRemoveCaravan(client.UserFile.Username);
        }

        private static void MoveCaravan(ServerClient client, CaravanFile file)
        {
            PKT_Caravan data = new PKT_Caravan();
            data._stepMode = CaravanStepMode.Move;
            data._caravanFile = file;

            ServerNetwork.SendPacketToAllClients(PacketHeader.CaravanManager, data, client);

            InformationDisplayer.DisplayMoveCaravan(client.UserFile.Username);
        }
    }
}
