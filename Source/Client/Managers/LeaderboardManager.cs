using System;
using System.Collections.Generic;
using Shared;
using Shared.Packets.Data;
using GameClient.TCP;
<<<<<<< HEAD
using GameClient.Misc;

=======
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
namespace GameClient.Managers
{
    public static class LeaderboardManager
    {
        public static List<StatisticsData> Rows { get; private set; } = new List<StatisticsData>();
        public static event Action OnLeaderboardReceived;

<<<<<<< HEAD
        public static void Request(int topN)
        {
            var req = new LeaderboardRequestData { TopN = topN };
            Network.Listener.EnqueuePacket(PacketHeader.LeaderboardRequest, req);
        }

        [HandlesPacket(PacketHeader.LeaderboardResponse)]
=======
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

>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
        private static void HandleResponse(byte[] bytes)
        {
            try
            {
<<<<<<< HEAD
                var data = Serializer.ConvertBytesToObject<LeaderboardResponseData>(bytes);
=======
                var data = Serializer.ConvertBytesToObject<LeaderboardData>(bytes);
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
                Rows = data.Rows ?? new List<StatisticsData>();
                OnLeaderboardReceived?.Invoke();
            }
            catch (Exception ex)
            {
<<<<<<< HEAD
                Printer.Error($"[LeaderboardManager] Error parsing response: {ex.Message}");
=======
                Log.Error($"[LeaderboardManager] Error parsing response: {ex.Message}");
>>>>>>> 8122f745b7e5c1dd1f5d4e47aa7442f948a0eab5
            }
        }
    }
}