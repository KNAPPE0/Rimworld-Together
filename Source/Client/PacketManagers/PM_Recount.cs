using System.Collections.Generic;
using GameClient.Misc;
using TCPNetwork.Packets;
using Shared;
using static Shared.CommonEnumerators;

namespace GameClient.PacketManagers
{

    public static class PM_Recount
    {
        public static int CurrentPlayers { get; private set; }

        public static List<string> CurrentPlayerNames { get; private set; }

        [HandlesPacket(PacketHeader.RecountManager)]
        private static void ParsePacket(byte[] bytes) { SetServerPlayers(bytes); }

        public static void SetServerPlayers(byte[] bytes)
        {
            PlayerRecountData data = Serializer.ConvertBytesToObject<PlayerRecountData>(bytes);

            CurrentPlayers = data._currentPlayerCount;
            CurrentPlayerNames = data._currentPlayerNames;
        }
    }
}