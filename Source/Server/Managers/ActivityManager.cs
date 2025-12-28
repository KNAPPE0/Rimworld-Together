using GameServer.Core;
using GameServer.Misc;
using Shared;
using static Shared.CommonEnumerators;
using TCPNetwork.Packets;
using TCPNetwork.Files.Client;

namespace GameServer.Managers
{
    public static class ActivityManager
    {
        [HandlesPacket(PacketHeader.ActivityManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if (client == null || bytes == null || bytes.Length == 0) return;

            if (!Master.ActionConfigs.ActivityAction.IsEnabled)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to use disabled feature!");
                return;
            }

            ActivityData data;
            try
            {
                data = Serializer.ConvertBytesToObject<ActivityData>(bytes);
            }
            catch
            {
                return;
            }

            if (data == null) return;

            switch (data._stepMode)
            {
                case ActivityStepMode.Request:
                    SendRequestedMap(client, data);
                    break;

                default:
                    break;
            }
        }

        private static void SendRequestedMap(ServerClient client, ActivityData data)
        {
            if (client == null || data == null) return;

            if (data._targetTile < 0 || !MapManager.CheckIfMapExists(data._targetTile))
            {
                data._stepMode = ActivityStepMode.Deny;
                data._mapRawData = null;
                client.Listener.EnqueuePacket(PacketHeader.ActivityManager, data);
                return;
            }

            byte[] mapBytes = MapManager.GetMapBytesFromTile(data._targetTile);

            if (mapBytes == null || mapBytes.Length == 0)
            {
                data._stepMode = ActivityStepMode.Deny;
                data._mapRawData = null;
                client.Listener.EnqueuePacket(PacketHeader.ActivityManager, data);
                return;
            }

            data._stepMode = ActivityStepMode.Request;
            data._mapRawData = mapBytes;

            client.Listener.EnqueuePacket(PacketHeader.ActivityManager, data);
        }
    }
}