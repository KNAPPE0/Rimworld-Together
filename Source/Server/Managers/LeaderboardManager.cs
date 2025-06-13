using Shared;
using Shared.Packets.Data;
using GameServer.TCP;
using static Shared.CommonEnumerators;

namespace GameServer.Managers
{
    public static class LeaderboardManager
    {
        [HandlesPacket(PacketHeader.LeaderboardRequest)]
        private static void HandleLeaderboardRequest(ServerClient client, byte[] bytes)
        {
            var req = Serializer.ConvertBytesToObject<LeaderboardRequestData>(bytes);
            var rows = StatsManager.GetLiveTop(req.TopN);
            var resp = new LeaderboardResponseData {
                Rows = rows
            };

            client.Listener.EnqueuePacket(PacketHeader.LeaderboardResponse, resp);
        }
    }
}