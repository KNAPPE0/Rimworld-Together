using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class EventManager
    {
        private static readonly double BaseMaxTimer = 3600000;

        public static void ParseEventPacket(ServerClient client, Packet packet)
        {
            // Deserialize the event data from the packet
            var eventData = Serializer.ConvertBytesToObject<EventData>(packet.Contents);
            if (eventData == null)
            {
                Logger.Warning("[EventManager] > Failed to deserialize event data.");
                return;
            }

            // Process the event based on its step mode
            switch (eventData.EventStepMode)
            {
                case EventStepMode.Send:
                    HandleSendEvent(client, eventData);
                    break;

                case EventStepMode.Receive:
                case EventStepMode.Recover:
                    // These modes are handled by other functions or are no-ops
                    break;
            }
        }

        private static void HandleSendEvent(ServerClient client, EventData eventData)
        {
            if (!SettlementManager.CheckIfTileIsInUse(eventData.ToTile))
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.userFile.Username} attempted to send an event to a non-existent settlement at tile {eventData.ToTile}.");
                return;
            }

            var settlement = SettlementManager.GetSettlementFileFromTile(eventData.ToTile);
            if (!UserManager.CheckIfUserIsConnected(settlement.owner))
            {
                RecoverEvent(client, eventData);
            }
            else
            {
                ProcessEventForTarget(client, settlement.owner, eventData);
            }
        }

        private static void RecoverEvent(ServerClient client, EventData eventData)
        {
            eventData.EventStepMode = EventStepMode.Recover;
            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.EventPacket), eventData);
            client.Listener.EnqueuePacket(packet);
        }

        private static void ProcessEventForTarget(ServerClient client, string targetUsername, EventData eventData)
        {
            var target = UserManager.GetConnectedClientFromUsername(targetUsername);

            if (Master.serverConfig.TemporalEventProtection && !TimeConverter.CheckForEpochTimer(target.userFile.EventProtectionTime, BaseMaxTimer))
            {
                RecoverEvent(client, eventData);
            }
            else
            {
                // Notify the sender
                var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.EventPacket), eventData);
                client.Listener.EnqueuePacket(packet);

                // Notify the receiver
                eventData.EventStepMode = EventStepMode.Receive;
                target.userFile.UpdateEventTime();
                packet = Packet.CreatePacketFromObject(nameof(PacketHandler.EventPacket), eventData);
                target.Listener.EnqueuePacket(packet);
            }
        }
    }
}