using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class RoadManager
    {
        public readonly static string fileExtension = ".mproad";

        public static void ParsePacket(ServerClient client, Packet packet)
        {
            RoadData data = Serializer.ConvertBytesToObject<RoadData>(packet.Contents);

            switch (data.StepMode)
            {
                case RoadStepMode.Add:
                    AddRoad(client, data);
                    break;

                case RoadStepMode.Remove:
                    RemoveRoad(client, data);
                    break;
            }
        }

        private static void AddRoad(ServerClient client, RoadData data)
        {
            if (RoadManagerHelper.CheckIfRoadExists(data.Details))
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to add a road that already existed");
                return;
            }

            SaveRoad(data.Details, client);

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.RoadPacket), data);
            foreach(ServerClient cClient in Network.connectedClients.ToArray())
            {
                cClient.Listener.EnqueuePacket(packet);
            }
        }

        private static void RemoveRoad(ServerClient client, RoadData data)
        {
            if (!RoadManagerHelper.CheckIfRoadExists(data.Details))
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to remove a road that didn't exist");
                return;
            }

            foreach (RoadDetails existingRoad in Master.worldValues.Roads)
            {
                if (existingRoad.TileA == data.Details.TileA && existingRoad.TileB == data.Details.TileB)
                {
                    DeleteRoad(existingRoad, client);
                    BroadcastDeletion(existingRoad);
                    return;
                }

                else if (existingRoad.TileA == data.Details.TileB && existingRoad.TileB == data.Details.TileA)
                {
                    DeleteRoad(existingRoad, client);
                    BroadcastDeletion(existingRoad);
                    return;
                }

                else continue;
            }

            void BroadcastDeletion(RoadDetails toRemove)
            {
                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.RoadPacket), data);
                foreach (ServerClient cClient in Network.connectedClients.ToArray())
                {
                    cClient.Listener.EnqueuePacket(packet);
                }
            }
        }

        private static void SaveRoad(RoadDetails details, ServerClient client = null)
        {
            List<RoadDetails> currentRoads = Master.worldValues.Roads.ToList();
            currentRoads.Add(details);

            Master.worldValues.Roads = currentRoads.ToArray();
            Master.SaveValueFile(ServerFileMode.World);

            if (client != null) Logger.Warning($"[Added road from tiles '{details.TileA}' to '{details.TileB}'] > {client.userFile.Username}");
            else Logger.Warning($"[Added road from tiles '{details.TileA}' to '{details.TileB}']");
        }

        private static void DeleteRoad(RoadDetails details, ServerClient client = null)
        {
            List<RoadDetails> currentRoads = Master.worldValues.Roads.ToList();
            currentRoads.Remove(details);

            Master.worldValues.Roads = currentRoads.ToArray();
            Master.SaveValueFile(ServerFileMode.World);

            if (client != null) Logger.Warning($"[Removed road from tiles '{details.TileA}' to '{details.TileB}'] > {client.userFile.Username}");
            else Logger.Warning($"[Removed road from tiles '{details.TileA}' to '{details.TileB}']");
        }
    }

    public static class RoadManagerHelper
    {
        public static bool CheckIfRoadExists(RoadDetails details)
        {
            foreach (RoadDetails existingRoad in Master.worldValues.Roads)
            {
                if (existingRoad.TileA == details.TileA && existingRoad.TileB == details.TileB) return true;
                else if (existingRoad.TileA == details.TileB && existingRoad.TileB == details.TileA) return true;
            }

            return false;
        }
    }
}
