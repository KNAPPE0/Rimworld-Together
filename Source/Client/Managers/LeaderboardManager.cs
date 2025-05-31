using System;
using System.Collections.Generic;
using Shared;
using Shared.Packets.Data;
using GameClient.TCP;
namespace GameClient.Managers
{
    public static class LeaderboardManager
    {
        public static List<StatisticsData> Rows { get; private set; } = new List<StatisticsData>();
        public static event Action OnLeaderboardReceived;

        static LeaderboardManager()
        {
            // Register handler for server response
            Network.Listener.RegisterHandler(PacketHeader.LeaderboardResponse, HandleResponse);
        }

        public static void Request(int topN)
        {
            var req = new LeaderboardRequestData { TopN = topN };
            byte[] bytes = Serializer.ConvertObjectToBytes(req);
            Network.Listener.EnqueuePacket(PacketHeader.LeaderboardRequest, bytes);
        }

        private static void HandleResponse(byte[] bytes)
        {
            try
            {
                var data = Serializer.ConvertBytesToObject<LeaderboardData>(bytes);
                Rows = data.Rows ?? new List<StatisticsData>();
                OnLeaderboardReceived?.Invoke();
            }
            catch (Exception ex)
            {
                Log.Error($"[LeaderboardManager] Error parsing response: {ex.Message}");
            }
        }
    }
}