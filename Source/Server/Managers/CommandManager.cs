using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class CommandManager
    {
        public static void ParseCommand(Packet packet)
        {
            // Deserialize command data from the packet
            var commandData = Serializer.ConvertBytesToObject<CommandData>(packet.Contents);
            if (commandData == null)
            {
                Logger.Warning("[CommandManager] > Failed to deserialize command data.");
                return;
            }

            switch (commandData.CommandMode)
            {
                case CommandMode.Op:
                    HandleOpCommand(commandData);
                    break;

                case CommandMode.Deop:
                    HandleDeopCommand(commandData);
                    break;

                case CommandMode.Broadcast:
                    HandleBroadcastCommand(commandData);
                    break;

                case CommandMode.ForceSave:
                    HandleForceSaveCommand(commandData);
                    break;

                default:
                    Logger.Warning($"[CommandManager] > Unrecognized command mode: {commandData.CommandMode}");
                    break;
            }
        }

        private static void HandleOpCommand(CommandData commandData)
        {
            // Implement logic for Op command
            Logger.Message("[CommandManager] > Handling Op Command.");
        }

        private static void HandleDeopCommand(CommandData commandData)
        {
            // Implement logic for Deop command
            Logger.Message("[CommandManager] > Handling Deop Command.");
        }

        private static void HandleBroadcastCommand(CommandData commandData)
        {
            // Implement logic for Broadcast command
            Logger.Message($"[CommandManager] > Broadcasting message: {commandData.CommandDetails}");
        }

        private static void HandleForceSaveCommand(CommandData commandData)
        {
            // Implement logic for ForceSave command
            Logger.Message("[CommandManager] > Handling ForceSave Command.");
        }

        public static void SendOpCommand(ServerClient client)
        {
            var commandData = new CommandData
            {
                CommandMode = CommandMode.Op
            };

            SendCommandPacket(client, commandData);
        }

        public static void SendDeOpCommand(ServerClient client)
        {
            var commandData = new CommandData
            {
                CommandMode = CommandMode.Deop
            };

            SendCommandPacket(client, commandData);
        }

        public static void SendEventCommand(ServerClient client, int eventID)
        {
            var eventData = new EventData
            {
                EventStepMode = EventStepMode.Receive,
                EventId = eventID
            };

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.EventPacket), eventData);
            client.Listener.EnqueuePacket(packet);
        }

        public static void SendBroadcastCommand(string message)
        {
            var commandData = new CommandData
            {
                CommandMode = CommandMode.Broadcast,
                CommandDetails = message
            };

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.CommandPacket), commandData);
            NetworkHelper.SendPacketToAllClients(packet);
        }

        public static void SendForceSaveCommand(ServerClient client)
        {
            var commandData = new CommandData
            {
                CommandMode = CommandMode.ForceSave
            };

            SendCommandPacket(client, commandData);
        }

        private static void SendCommandPacket(ServerClient client, CommandData commandData)
        {
            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.CommandPacket), commandData);
            client.Listener.EnqueuePacket(packet);
        }
    }
}