using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class OfflineActivityManager
    {
        private static readonly double baseActivityTimer = 3600000;

        public static void ParseOfflineActivityPacket(ServerClient client, Packet packet)
        {
            OfflineActivityData data = Serializer.ConvertBytesToObject<OfflineActivityData>(packet.Contents);

            switch (data.ActivityStepMode)
            {
                case OfflineActivityStepMode.Request:
                    SendRequestedMap(client, data);
                    break;

                case OfflineActivityStepMode.Deny:
                    //Nothing goes here
                    break;
            }
        }

        private static void SendRequestedMap(ServerClient client, OfflineActivityData data)
        {
            if (!MapManager.CheckIfMapExists(data.TargetTile))
            {
                data.ActivityStepMode = OfflineActivityStepMode.Unavailable;
                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OfflineActivityPacket), data);
                client.Listener.EnqueuePacket(packet);
            }

            else
            {
                SettlementFile settlementFile = SettlementManager.GetSettlementFileFromTile(data.TargetTile);

                if (UserManager.CheckIfUserIsConnected(settlementFile.owner))
                {
                    data.ActivityStepMode = OfflineActivityStepMode.Deny;
                    Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OfflineActivityPacket), data);
                    client.Listener.EnqueuePacket(packet);
                }

                else
                {
                    UserFile userFile = UserManager.GetUserFileFromName(settlementFile.owner);

                    if (Master.serverConfig.TemporalActivityProtection && !TimeConverter.CheckForEpochTimer(userFile.ActivityProtectionTime, baseActivityTimer))
                    {
                        data.ActivityStepMode = OfflineActivityStepMode.Deny;
                        Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OfflineActivityPacket), data);
                        client.Listener.EnqueuePacket(packet);
                    }

                    else
                    {
                        userFile.UpdateActivityTime();

                        data.MapData = MapManager.GetUserMapFromTile(data.TargetTile).MapData;
                        Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OfflineActivityPacket), data);
                        client.Listener.EnqueuePacket(packet);
                    }
                }
            }
        }
    }
}
