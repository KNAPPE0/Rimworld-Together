using Shared;
using TCPNetwork.Files.Client;

namespace GameServer.PacketManager
{

    public static class PM_Recount
    {
        [HandlesPacket(PacketHeader.RecountManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes, PacketHeader header)
        {

        }
    }
}