using Shared;
using TCPNetwork;
using TCPNetwork.Files.Client;

namespace GameServer.PacketManager
{
    public class PM_Recount : PM_Base
    {
        [HandlesPacket(PacketHeader.RecountManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {

        }
    }
}