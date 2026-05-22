using GameServer.Core;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Details.Planet;
using Shared.Files.Configs;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using static TCPNetwork.Packets.PKT_Road;

namespace GameServer.PacketManager
{
    public class PM_Roads : PM_Base
    {
        [HandlesPacket(PacketHeader.RoadManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            // Two-layer guard. First the global feature
            // toggle (disabled features should be cheap to deny — no
            // packet deserialisation cost). Second the per-player
            // cooldown so a connected client can't spam Add/Remove
            // requests faster than the configured interval. Ported from
            // upstream's "Security checks for sites, settlements, roads
            // and pollution" (May 2026), adapted to KMH's epoch-based
            // cooldown convention.
            if (!Master.ActionConfigs.RoadsAction.IsEnabled)
            {
                ResponseShortcutManager.SendIllegalPacket(client, "Tried to use disabled feature!");
                return;
            }

            if (!PlayerCooldown.CheckIfCanRoad(client.UserFile,
                    Master.ActionConfigs.RoadsAction.IsEnabled,
                    Master.ActionConfigs.RoadsAction.Cooldown))
            {
                ResponseShortcutManager.SendUnavailablePacket(client);
                return;
            }

            PKT_Road data = Serializer.ConvertBytesToObject<PKT_Road>(bytes);
            if (data == null) return;

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

            // Stamp the cooldown only AFTER a successful
            // Save+broadcast. If save throws or validation rejects, the
            // user shouldn't be locked out — the action didn't happen.
            client.UserFile.Cooldowns.SetRoadTimer(
                Shared.TimeConverter.GetCurrentTimeToEpoch(),
                client.UserFile);
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

            Master.WorldValues.Roads = currentRoads;
            PlanetConfigFile.Save(PlanetConfigFile.SavePath, Master.WorldValues);

            InformationDisplayer.DisplayAddRoad(details.FromTile.ToString(), details.ToTile.ToString());
        }

        private static void DeleteRoad(RoadDetail details, ServerClient client = null)
        {
            List<RoadDetail> currentRoads = Master.WorldValues.Roads.ToList();
            currentRoads.Remove(details);

            Master.WorldValues.Roads = currentRoads;
            PlanetConfigFile.Save(PlanetConfigFile.SavePath, Master.WorldValues);

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
