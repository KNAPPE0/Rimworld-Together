using Shared;
using static Shared.CommonEnumerators;
using System.IO;

namespace GameServer
{
    public static class WorldManager
    {
        private static readonly string worldFileName = "WorldValues.json";
        private static readonly string worldFilePath = Path.Combine(Master.corePath, worldFileName);

        public static void ParseWorldPacket(ServerClient client, Packet packet)
        {
            WorldData worldData = Serializer.ConvertBytesToObject<WorldData>(packet.Contents);

            switch (worldData.WorldStepMode)
            {
                case WorldStepMode.Required:
                    if (worldData.WorldValuesFile != null)
                    {
                        Master.worldValues = worldData.WorldValuesFile;
                        Master.SaveValueFile(ServerFileMode.World);
                    }
                    else
                    {
                        Logger.Error("Received null WorldValuesFile in WorldStepMode.Required");
                    }
                    break;

                case WorldStepMode.Existing:
                    // Do nothing, as the client is simply acknowledging the existing world
                    break;

                default:
                    Logger.Warning($"Unknown WorldStepMode received: {worldData.WorldStepMode}");
                    break;
            }
        }

        public static bool CheckIfWorldExists() => File.Exists(worldFilePath);

        public static void RequestWorldFile(ServerClient client)
        {
            WorldData worldData = new WorldData
            {
                WorldStepMode = WorldStepMode.Required
            };

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.WorldPacket), worldData);
            client.Listener?.EnqueuePacket(packet); // Added null check for Listener
        }

        public static void SendWorldFile(ServerClient client)
        {
            if (Master.worldValues == null)
            {
                Logger.Error("World values are null, cannot send world file.");
                return;
            }

            WorldData worldData = new WorldData
            {
                WorldStepMode = WorldStepMode.Existing,
                WorldValuesFile = Master.worldValues
            };

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.WorldPacket), worldData);
            client.Listener?.EnqueuePacket(packet); // Added null check for Listener
        }
    }
}