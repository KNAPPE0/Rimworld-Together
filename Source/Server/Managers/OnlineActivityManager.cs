using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class OnlineActivityManager
    {
        public static void ParseOnlineActivityPacket(ServerClient client, Packet packet)
        {
            OnlineActivityData visitData = Serializer.ConvertBytesToObject<OnlineActivityData>(packet.Contents);

            switch (visitData.ActivityStepMode)
            {
                case OnlineActivityStepMode.Request:
                    SendVisitRequest(client, visitData);
                    break;

                case OnlineActivityStepMode.Accept:
                    AcceptVisitRequest(client, visitData);
                    break;

                case OnlineActivityStepMode.Reject:
                    RejectVisitRequest(client, visitData);
                    break;

                case OnlineActivityStepMode.Action:
                    SendVisitActions(client, visitData);
                    break;

                case OnlineActivityStepMode.Create:
                    SendVisitActions(client, visitData);
                    break;

                case OnlineActivityStepMode.Destroy:
                    SendVisitActions(client, visitData);
                    break;

                case OnlineActivityStepMode.Damage:
                    SendVisitActions(client, visitData);
                    break;

                case OnlineActivityStepMode.Hediff:
                    SendVisitActions(client, visitData);
                    break;

                case OnlineActivityStepMode.TimeSpeed:
                    SendVisitActions(client, visitData);
                    break;

                case OnlineActivityStepMode.GameCondition:
                    SendVisitActions(client, visitData);
                    break;

                case OnlineActivityStepMode.Weather:
                    SendVisitActions(client, visitData);
                    break;

                case OnlineActivityStepMode.Kill:
                    SendVisitActions(client, visitData);
                    break;

                case OnlineActivityStepMode.Stop:
                    SendVisitStop(client);
                    break;
            }
        }

        private static void SendVisitRequest(ServerClient client, OnlineActivityData data)
        {
            SettlementFile settlementFile = SettlementManager.GetSettlementFileFromTile(data.TargetTile);
            if (settlementFile == null) ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.userFile.Username} tried to visit a settlement at tile {data.TargetTile}, but no settlement could be found");
            else
            {
                ServerClient toGet = UserManager.GetConnectedClientFromUsername(settlementFile.owner);
                if (toGet == null)
                {
                    data.ActivityStepMode = OnlineActivityStepMode.Unavailable;
                    Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OnlineActivityPacket), data);
                    client.Listener.EnqueuePacket(packet);
                }

                else
                {
                    if (toGet.InVisitWith != null)
                    {
                        data.ActivityStepMode = OnlineActivityStepMode.Unavailable;
                        Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OnlineActivityPacket), data);
                        client.Listener.EnqueuePacket(packet);
                    }

                    else
                    {
                        data.OtherPlayerName = client.userFile.Username;
                        Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OnlineActivityPacket), data);
                        toGet.Listener.EnqueuePacket(packet);
                    }
                }
            }
        }

        private static void AcceptVisitRequest(ServerClient client, OnlineActivityData data)
        {
            SettlementFile settlementFile = SettlementManager.GetSettlementFileFromTile(data.FromTile);
            if (settlementFile == null) return;
            else
            {
                ServerClient toGet = UserManager.GetConnectedClientFromUsername(settlementFile.owner);
                if (toGet == null) return;
                else
                {
                    client.InVisitWith = toGet;
                    toGet.InVisitWith = client;

                    Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OnlineActivityPacket), data);
                    toGet.Listener.EnqueuePacket(packet);
                }
            }
        }

        private static void RejectVisitRequest(ServerClient client, OnlineActivityData data)
        {
            SettlementFile settlementFile = SettlementManager.GetSettlementFileFromTile(data.FromTile);
            if (settlementFile == null) return;
            else
            {
                ServerClient toGet = UserManager.GetConnectedClientFromUsername(settlementFile.owner);
                if (toGet == null) return;
                else
                {
                    Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OnlineActivityPacket), data);
                    toGet.Listener.EnqueuePacket(packet);
                }
            }
        }

        private static void SendVisitActions(ServerClient client, OnlineActivityData data)
        {
            if (client.InVisitWith == null)
            {
                data.ActivityStepMode = OnlineActivityStepMode.Stop;
                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OnlineActivityPacket), data);
                client.Listener.EnqueuePacket(packet);
            }

            else
            {
                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OnlineActivityPacket), data);
                client.InVisitWith.Listener.EnqueuePacket(packet);
            }
        }

        public static void SendVisitStop(ServerClient client)
        {
            OnlineActivityData visitData = new OnlineActivityData();
            visitData.ActivityStepMode = OnlineActivityStepMode.Stop;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.OnlineActivityPacket), visitData);
            client.Listener.EnqueuePacket(packet);

            ServerClient otherPlayer = client.InVisitWith;
            if (otherPlayer != null)
            {
                otherPlayer.Listener.EnqueuePacket(packet);
                otherPlayer.InVisitWith = null;
                client.InVisitWith = null;
            }
        }
    }
}
