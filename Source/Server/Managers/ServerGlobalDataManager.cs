using Shared;

namespace GameServer
{
    public static class ServerGlobalDataManager
    {
        public static void SendServerGlobalData(ServerClient client)
        {
            ServerGlobalData globalData = new ServerGlobalData();

            globalData = GetServerConfigs(globalData);
            globalData = GetClientValues(client, globalData);
            globalData = GetServerValues(globalData);
            globalData = GetServerSettlements(client, globalData);
            globalData = GetServerSites(client, globalData);
            globalData = GetServerCaravans(client, globalData);
            globalData = GetServerRoads(globalData);
            globalData = GetServerPollution(globalData);

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.ServerValuesPacket), globalData);
            client.Listener.EnqueuePacket(packet);
        }

        private static ServerGlobalData GetServerConfigs(ServerGlobalData globalData)
        {
            ServerConfigFile scf = Master.serverConfig;
            globalData.AllowCustomScenarios = scf?.AllowCustomScenarios ?? false; // null check for serverConfig
            return globalData;
        }

        private static ServerGlobalData GetClientValues(ServerClient client, ServerGlobalData globalData)
        {
            globalData.IsClientAdmin = client.userFile.IsAdmin;
            globalData.IsClientFactionMember = client.userFile.HasFaction;
            return globalData;
        }

        private static ServerGlobalData GetServerValues(ServerGlobalData globalData)
        {
            globalData.EventValues = Master.eventValues;
            globalData.SiteValues = Master.siteValues;
            globalData.DifficultyValues = Master.difficultyValues;
            globalData.ActionValues = Master.actionValues;
            globalData.RoadValues = Master.roadValues;
            return globalData;
        }

        private static ServerGlobalData GetServerSettlements(ServerClient client, ServerGlobalData globalData)
        {
            List<OnlineSettlementFile> tempList = new List<OnlineSettlementFile>();
            SettlementFile[] settlements = SettlementManager.GetAllSettlements();

            foreach (SettlementFile settlement in settlements)
            {
                if (settlement.owner == client.userFile.Username) continue;

                OnlineSettlementFile file = new OnlineSettlementFile
                {
                    Tile = settlement.tile,
                    Owner = settlement.owner,
                    Goodwill = GoodwillManager.GetSettlementGoodwill(client, settlement)
                };

                tempList.Add(file);
            }

            globalData.PlayerSettlements = tempList.ToArray();
            if (Master.worldValues != null)
            {
                globalData.NpcSettlements = Master.worldValues.NPCSettlements;
            }

            return globalData;
        }

        private static ServerGlobalData GetServerSites(ServerClient client, ServerGlobalData globalData)
        {
            List<OnlineSiteFile> tempList = new List<OnlineSiteFile>();
            SiteFile[] sites = SiteManager.GetAllSites();

            foreach (SiteFile site in sites)
            {
                OnlineSiteFile file = new OnlineSiteFile
                {
                    Tile = site.tile,
                    Owner = site.owner,
                    Goodwill = GoodwillManager.GetSiteGoodwill(client, site),
                    Type = site.type,
                    FromFaction = site.isFromFaction
                };

                tempList.Add(file);
            }

            globalData.PlayerSites = tempList.ToArray();

            return globalData;
        }

        private static ServerGlobalData GetServerCaravans(ServerClient client, ServerGlobalData globalData)
        {
            globalData.PlayerCaravans = CaravanManager.GetActiveCaravans();
            return globalData;
        }

        private static ServerGlobalData GetServerRoads(ServerGlobalData globalData)
        {
            if (Master.worldValues != null)
            {
                globalData.Roads = Master.worldValues.Roads;
            }
            return globalData;
        }

        private static ServerGlobalData GetServerPollution(ServerGlobalData globalData)
        {
            if (Master.worldValues != null)
            {
                globalData.PollutedTiles = Master.worldValues.PollutedTiles;
            }
            return globalData;
        }
    }
}