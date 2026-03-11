using GameClient.Misc;
using Shared;
using System.Collections.Generic;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameClient.PacketManagers
{

    public class PM_Recount : PM_Base
    {
        public static int CurrentPlayers { get; private set; }

        public static List<string> CurrentPlayerNames { get; private set; }

        [HandlesPacket(PacketHeader.RecountManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header) { SetServerPlayers(bytes); }

        public static void SetServerPlayers(byte[] bytes)
        {
            PlayerRecountData data = Serializer.ConvertBytesToObject<PlayerRecountData>(bytes);

            CurrentPlayers = data._currentPlayerCount;
            CurrentPlayerNames = data._currentPlayerNames;
        }
    }
}