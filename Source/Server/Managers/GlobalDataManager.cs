using GameServer.Core;
using GameServer.PacketManager;
using Shared;
using Shared.Files;
using Shared.Files.Sites;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;

namespace GameServer.Managers
{
    public class GlobalDataManager
    {
        public static void SendServerGlobalData(ServerClient client)
        {
            ServerGlobalData globalData = new ServerGlobalData();

            globalData._serverName = Master.ServerConfig.Name;
            globalData._isClientAdmin = client.UserFile.IsAdmin;
            globalData._isClientFactionMember = GuildManagerH.GetFactionFromName(client.UserFile.GuildName) != null;

            globalData._eventValues = EventManagerH.LoadedEvents;
            globalData._difficultyValues = Master.DifficultyValues;
            globalData._scenarioValues = Master.ScenarioValues;
            globalData._storytellerValues = Master.StorytellerValues;
            globalData._actionValues = Master.ActionConfigs;
            globalData._roadValues = Master.ActionConfigs.RoadsAction.RoadValues;
            globalData._modConfigs = Master.ModConfig;
            globalData._siteValues = Master.ActionConfigs.SiteAction.SiteTypes;

            if (Master.WorldValues != null)
            {
                globalData._roads = Master.WorldValues.Roads;
                globalData._pollutedTiles = Master.WorldValues.PollutedTiles;
                globalData._playerSettlements = GlobalDataManagerHelper.GetServerSettlements(client);
                globalData._npcSettlements = Master.WorldValues.NPCSettlements;
                globalData._playerSites = GlobalDataManagerHelper.GetServerSites(client);
            }

            client.Listener.EnqueuePacket(PacketHeader.GlobalDataManager, globalData);
        }
    }

    public class GlobalDataManagerHelper
    {
        public static SettlementFile[] GetServerSettlements(ServerClient client)
        {
            List<SettlementFile> tempList = new List<SettlementFile>();
            foreach (SettlementFile settlement in PM_Settlements.GetAllSettlements())
            {
                SettlementFile file = new SettlementFile();

                if (settlement.Username == client.UserFile.Username) continue;
                else
                {
                    file.Tile = settlement.Tile;
                    file.Username = settlement.Username;
                    file.Username = settlement.Username;
                    file.Goodwill = PM_Goodwills.GetSettlementGoodwill(client, settlement);

                    tempList.Add(file);
                }
            }

            return tempList.ToArray();
        }

        public static SiteFile[] GetServerSites(ServerClient client)
        {
            List<SiteFile> tempList = new List<SiteFile>();
            foreach (SiteFile site in SiteManagerHelper.GetAllSites())
            {
                SiteFile file = new SiteFile();

                file.Tile = site.Tile;
                file.Username = site.Username;
                file.Goodwill = PM_Goodwills.GetSiteGoodwill(client, site);
                file.Type = site.Type;
                file.GuildName = site.GuildName;

                tempList.Add(file);
            }

            return tempList.ToArray();
        }
    }
}
