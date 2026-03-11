using GameClient.Hooks.TCPNetwork;
using GameClient.Misc;
using Shared;
using Shared.Misc;
using System;
using System.Diagnostics;
using TCPNetwork;
using TCPNetwork.Files.Client;

namespace GameClient.PacketManagers
{
    public class PM_KeepAlive : PM_Base
    {
        private static Stopwatch LatencyStopwatch { get; set; } = new Stopwatch();

        public static int RawPing { get; set; } = int.MaxValue;

        public static int CurrentPing { get; set; } = int.MaxValue;

        [HandlesPacket(PacketHeader.KeepAliveManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            Network.ServerEndpoint.LastKAPacket = DateTime.Now;

            ComparePing();
        }

        [OnSessionStart]
        private static void StartTimer() { LatencyStopwatch.Restart(); }

        [OnSessionEnd]
        private static void EndTimer() { LatencyStopwatch.Stop(); }

        private static void ComparePing()
        {
            LatencyStopwatch.Stop();

            RawPing = (int)LatencyStopwatch.ElapsedMilliseconds;
            CurrentPing = (int)(RawPing - Network.KeepAliveInterval.TotalMilliseconds);

            LatencyStopwatch.Restart();
        }
    }
}