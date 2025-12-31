using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;

namespace GameServer.Managers
{
    public static class SynchronousManager
    {
        [HandlesPacket(PacketHeader.SPlayerDraft)]
        private static void SPlayerDraft(ServerClient client, byte[] bytes, PacketHeader header)
        {
            client.Listener.EnqueuePacket(header, bytes);
        }

        [HandlesPacket(PacketHeader.SPlayerWeather)]
        private static void SPlayerWeather(ServerClient client, byte[] bytes, PacketHeader header)
        {
            client.Listener.EnqueuePacket(header, bytes);
        }

        [HandlesPacket(PacketHeader.SPlayerMentalState)]
        private static void SPlayerMentalState(ServerClient client, byte[] bytes, PacketHeader header)
        {
            client.Listener.EnqueuePacket(header, bytes);
        }

        [HandlesPacket(PacketHeader.SPlayerGameSpeed)]
        private static void SPlayerGameSpeed(ServerClient client, byte[] bytes, PacketHeader header)
        {
            client.Listener.EnqueuePacket(header, bytes);
        }
    }
}
