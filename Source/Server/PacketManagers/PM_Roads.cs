using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Details.Planet;
using Shared.Files;
using Shared.Files.Configs;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameServer.PacketManager
{
    public class PM_Roads : PM_Base
    {
        [HandlesPacket(PacketHeader.RoadManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            if (!Master.ActionConfigs.RoadsAction.IsEnabled)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to use disabled feature!");
                return;
            }

            PKT_Road data = Serializer.ConvertBytesToObject<PKT_Road>(bytes);

            switch (data._stepMode)
            {
                case RoadStepMode.Add:
                    AddRoad(client, data);
                    break;

                case RoadStepMode.Remove:
                    RemoveRoad(client, data);
                    break;
            }
        }

        private static void AddRoad(ServerClient client, PKT_Road data)
        {
            if (RoadManagerHelper.CheckIfRoadExists(data._details))
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to add a road that already existed");
                return;
            }

            SaveRoad(data._details, client);

            ServerNetwork.SendPacketToAllClients(PacketHeader.RoadManager, data);
        }

        private static void RemoveRoad(ServerClient client, PKT_Road data)
        {
            if (!RoadManagerHelper.CheckIfRoadExists(data._details))
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to remove a road that didn't exist");
                return;
            }

            foreach (RoadDetail existingRoad in Master.WorldValues.Roads)
            {
                if (existingRoad.FromTile == data._details.FromTile && existingRoad.ToTile == data._details.ToTile)
                {
                    DeleteRoad(existingRoad, client);
                    BroadcastDeletion(existingRoad);
                    return;
                }

                else if (existingRoad.FromTile == data._details.ToTile && existingRoad.ToTile == data._details.FromTile)
                {
                    DeleteRoad(existingRoad, client);
                    BroadcastDeletion(existingRoad);
                    return;
                }

                else continue;
            }

            void BroadcastDeletion(RoadDetail toRemove)
            {
                ServerNetwork.SendPacketToAllClients(PacketHeader.RoadManager, data);
            }
        }

        private static void SaveRoad(RoadDetail details, ServerClient client = null)
        {
            List<RoadDetail> currentRoads = Master.WorldValues.Roads.ToList();
            currentRoads.Add(details);

            Master.WorldValues.Roads = currentRoads.ToArray();
            PlanetConfigFile.Save(PlanetConfigFile.SavePath, Master.WorldValues, true);

            InformationDisplayer.DisplayAddRoad(details.FromTile.ToString(), details.ToTile.ToString());
        }

        private static void DeleteRoad(RoadDetail details, ServerClient client = null)
        {
            List<RoadDetail> currentRoads = Master.WorldValues.Roads.ToList();
            currentRoads.Remove(details);

            Master.WorldValues.Roads = currentRoads.ToArray();
            PlanetConfigFile.Save(PlanetConfigFile.SavePath, Master.WorldValues, true);

            InformationDisplayer.DisplayRemoveRoad(details.FromTile.ToString(), details.ToTile.ToString());
        }
    }

    public class RoadManagerHelper
    {
        public static bool CheckIfRoadExists(RoadDetail details)
        {
            foreach (RoadDetail existingRoad in Master.WorldValues.Roads)
            {
                if (existingRoad.FromTile == details.FromTile && existingRoad.ToTile == details.ToTile) return true;
                else if (existingRoad.FromTile == details.ToTile && existingRoad.ToTile == details.FromTile) return true;
            }

            return false;
        }
    }
}
