using System;
using System.Collections.Generic;
using Shared;
using Shared.Packets.Data;
using GameClient.TCP;
using GameClient.Misc;

namespace GameClient.Managers
{
    public static class LeaderboardManager
    {
        public static List<StatisticsData> Rows { get; private set; } = new List<StatisticsData>();
        public static event Action OnLeaderboardReceived;

        public static void Request(int topN)
        {
            var req = new LeaderboardRequestData { TopN = topN };
            Network.Listener.EnqueuePacket(PacketHeader.LeaderboardRequest, req);
        }

        [HandlesPacket(PacketHeader.LeaderboardResponse)]
        private static void HandleResponse(byte[] bytes)
        {
            try
            {
                var data = Serializer.ConvertBytesToObject<LeaderboardResponseData>(bytes);
                Rows = data.Rows ?? new List<StatisticsData>();
                OnLeaderboardReceived?.Invoke();
            }
            catch (Exception ex)
            {
                Printer.Error($"[LeaderboardManager] Error parsing response: {ex.Message}");
            }
        }
    }
}