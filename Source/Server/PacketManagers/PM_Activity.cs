using GameServer.Core;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;

namespace GameServer.PacketManager
{
    public class PM_Activity : PM_Base
    {
        [HandlesPacket(PacketHeader.ActivityManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if (client == null || bytes == null || bytes.Length == 0) return;

            if (!Master.ActionConfigs.ActivityAction.IsEnabled)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to use disabled feature!");
                return;
            }

            PKT_Activity data = Serializer.ConvertBytesToObject<PKT_Activity>(bytes);
            if (data == null) return;

            switch (data._stepMode)
            {
                case ActivityStepMode.Request:
                    SendRequestedMap(client, data);
                    break;
            }
        }

        private static void SendRequestedMap(ServerClient client, PKT_Activity data)
        {
            if (client == null || data == null) return;

            if (data._targetTile < 0 || !PM_Maps.CheckIfMapExists(data._targetTile))
            {
                data._stepMode = ActivityStepMode.Deny;
                data._mapRawData = null;
                client.Listener.EnqueuePacket(PacketHeader.ActivityManager, data);
                return;
            }

            byte[] mapBytes = PM_Maps.GetMapBytesFromTile(data._targetTile);

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