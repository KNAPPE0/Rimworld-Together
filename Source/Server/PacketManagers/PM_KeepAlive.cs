using GameServer.Misc;
using Shared;
using TCPNetwork;
using TCPNetwork.Files.Client;

namespace GameServer.PacketManager
{
    public class PM_KeepAlive : PM_Base
    {
        [HandlesPacket(PacketHeader.KeepAliveManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            client.Listener.LastKAPacket = DateTime.Now;
        }
    }
}