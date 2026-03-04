using System;
using GameClient.Hooks.TCPNetwork;
using GameClient.Misc;
using Shared;
using TCPNetwork;

namespace GameClient.Managers
{
    public static class KeepAliveManager
    {
        [HandlesPacket(PacketHeader.KeepAliveManager)]
        private static void ParsePacket(byte[] bytes)
        {
            Network.ServerEndpoint.LastKAPacket = DateTime.Now;
        }
    }
}