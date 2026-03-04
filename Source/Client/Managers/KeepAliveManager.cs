using GameClient.Hooks.TCPNetwork;
using GameClient.Misc;
using Shared;
using Shared.Misc;
using System;
using System.Diagnostics;
using TCPNetwork;

namespace GameClient.Managers
{
    public static class KeepAliveManager
    {
        private static Stopwatch LatencyStopwatch { get; set; } = new Stopwatch();

        public static int RawPing { get; set; } = int.MaxValue;

        public static int CurrentPing { get; set; } = int.MaxValue;

        [HandlesPacket(PacketHeader.KeepAliveManager)]
        private static void ParsePacket(byte[] bytes)
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