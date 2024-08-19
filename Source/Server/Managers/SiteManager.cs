using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class SiteManager
    {
        //Variables

        public readonly static string fileExtension = ".mpsite";

        public static void ParseSitePacket(ServerClient client, Packet packet)
        {
            SiteData siteData = Serializer.ConvertBytesToObject<SiteData>(packet.Contents);

            switch(siteData.SiteStepMode)
            {
                case SiteStepMode.Build:
                    AddNewSite(client, siteData);
                    break;

                case SiteStepMode.Destroy:
                    DestroySite(client, siteData);
                    break;

                case SiteStepMode.Info:
                    GetSiteInfo(client, siteData);
                    break;

                case SiteStepMode.Deposit:
                    DepositWorkerToSite(client, siteData);
                    break;

                case SiteStepMode.Retrieve:
                    RetrieveWorkerFromSite(client, siteData);
                    break;
            }
        }

        public static bool CheckIfTileIsInUse(int tileToCheck)
        {
            string[] sites = Directory.GetFiles(Master.sitesPath);
            foreach (string site in sites)
            {
                if (!site.EndsWith(fileExtension)) continue;

                SiteFile siteFile = Serializer.SerializeFromFile<SiteFile>(site);
                if (siteFile.tile == tileToCheck) return true;
            }

            return false;
        }

        public static void ConfirmNewSite(ServerClient client, SiteFile siteFile)
        {
            SaveSite(siteFile);

            SiteData siteData = new SiteData();
            siteData.SiteStepMode = SiteStepMode.Build;
            siteData.Tile = siteFile.tile;
            siteData.Owner = client.userFile.Username;
            siteData.Type = siteFile.type;
            siteData.IsFromFaction = siteFile.isFromFaction;

            foreach (ServerClient cClient in Network.connectedClients.ToArray())
            {
                siteData.Goodwill = GoodwillManager.GetSiteGoodwill(cClient, siteFile);
                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.SitePacket), siteData);

                cClient.Listener.EnqueuePacket(packet);
            }

            siteData.SiteStepMode = SiteStepMode.Accept;
            Packet rPacket = Packet.CreatePacketFromObject(nameof(PacketHandler.SitePacket), siteData);
            client.Listener.EnqueuePacket(rPacket);

            Logger.Warning($"[Created site] > {client.userFile.Username}");
        }

        public static void SaveSite(SiteFile siteFile)
        {
            Serializer.SerializeToFile(Path.Combine(Master.sitesPath, siteFile.tile + fileExtension), siteFile);
        }

        public static SiteFile[] GetAllSites()
        {
            List<SiteFile> sitesList = new List<SiteFile>();

            string[] sites = Directory.GetFiles(Master.sitesPath);
            foreach (string site in sites)
            {
                if (!site.EndsWith(fileExtension)) continue;
                sitesList.Add(Serializer.SerializeFromFile<SiteFile>(site));
            }

            return sitesList.ToArray();
        }

        public static SiteFile[] GetAllSitesFromUsername(string Username)
        {
            List<SiteFile> sitesList = new List<SiteFile>();

            string[] sites = Directory.GetFiles(Master.sitesPath);
            foreach (string site in sites)
            {
                if (!site.EndsWith(fileExtension)) continue;

                SiteFile siteFile = Serializer.SerializeFromFile<SiteFile>(site);
                if (!siteFile.isFromFaction && siteFile.owner == Username) sitesList.Add(siteFile);
            }

            return sitesList.ToArray();
        }

        public static SiteFile GetSiteFileFromTile(int tileToGet)
        {
            string[] sites = Directory.GetFiles(Master.sitesPath);
            foreach (string site in sites)
            {
                if (!site.EndsWith(fileExtension)) continue;

                SiteFile siteFile = Serializer.SerializeFromFile<SiteFile>(site);
                if (siteFile.tile == tileToGet) return siteFile;
            }

            return null;
        }

        public static void DeleteAllSitesFromUsername(string username)
        {
            SiteFile[] sites = GetAllSitesFromUsername(username);
            foreach (SiteFile site in sites)
            {
                DestroySiteFromFile(site);
            }
        }

        private static void AddNewSite(ServerClient client, SiteData siteData)
        {
            if (SettlementManager.CheckIfTileIsInUse(siteData.Tile)) ResponseShortcutManager.SendIllegalPacket(client, $"A site tried to be added to tile {siteData.Tile}, but that tile already has a settlement");
            else if (CheckIfTileIsInUse(siteData.Tile)) ResponseShortcutManager.SendIllegalPacket(client, $"A site tried to be added to tile {siteData.Tile}, but that tile already has a site");
            else
            {
                SiteFile siteFile = null;

                if (siteData.IsFromFaction)
                {
                    FactionFile factionFile = FactionManager.GetFactionFromClient(client);

                    if (FactionManager.GetMemberRank(factionFile, client.userFile.Username) == FactionRanks.Member)
                    {
                        ResponseShortcutManager.SendNoPowerPacket(client, new PlayerFactionData());
                        return;
                    }

                    else
                    {
                        siteFile = new SiteFile();
                        siteFile.tile = siteData.Tile;
                        siteFile.owner = client.userFile.Username;
                        siteFile.type = siteData.Type;
                        siteFile.isFromFaction = true;
                        siteFile.factionName = client.userFile.FactionName;
                    }
                }

                else
                {
                    siteFile = new SiteFile();
                    siteFile.tile = siteData.Tile;
                    siteFile.owner = client.userFile.Username;
                    siteFile.type = siteData.Type;
                    siteFile.isFromFaction = false;
                }

                ConfirmNewSite(client, siteFile);
            }
        }

        private static void DestroySite(ServerClient client, SiteData siteData)
        {
            SiteFile siteFile = GetSiteFileFromTile(siteData.Tile);

            if (siteFile.isFromFaction)
            {
                if (siteFile.factionName != client.userFile.FactionName) ResponseShortcutManager.SendIllegalPacket(client, $"The site at tile {siteData.Tile} was attempted to be destroyed by {client.userFile.Username}, but player wasn't a part of faction {siteFile.factionName}");
                else
                {
                    FactionFile factionFile = FactionManager.GetFactionFromClient(client);

                    if (FactionManager.GetMemberRank(factionFile, client.userFile.Username) !=
                        FactionRanks.Member) DestroySiteFromFile(siteFile);

                    else ResponseShortcutManager.SendNoPowerPacket(client, new PlayerFactionData());
                }
            }

            else
            {
                if (siteFile.owner != client.userFile.Username) ResponseShortcutManager.SendIllegalPacket(client, $"The site at tile {siteData.Tile} was attempted to be destroyed by {client.userFile.Username}, but the player {siteFile.owner} owns it");
                else if (siteFile.workerData != null) ResponseShortcutManager.SendWorkerInsidePacket(client);
                else DestroySiteFromFile(siteFile);
            }
        }

        public static void DestroySiteFromFile(SiteFile siteFile)
        {
            SiteData siteData = new SiteData();
            siteData.SiteStepMode = SiteStepMode.Destroy;
            siteData.Tile = siteFile.tile;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.SitePacket), siteData);
            foreach (ServerClient client in Network.connectedClients.ToArray()) client.Listener.EnqueuePacket(packet);

            File.Delete(Path.Combine(Master.sitesPath, siteFile.tile + fileExtension));
            Logger.Warning($"[Remove site] > {siteFile.tile}");
        }

        private static void GetSiteInfo(ServerClient client, SiteData siteData)
        {
            SiteFile siteFile = GetSiteFileFromTile(siteData.Tile);

            siteData.Type = siteFile.type;
            siteData.WorkerData = siteFile.workerData;
            siteData.IsFromFaction = siteFile.isFromFaction;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.SitePacket), siteData);
            client.Listener.EnqueuePacket(packet);
        }

        private static void DepositWorkerToSite(ServerClient client, SiteData siteData)
        {
            SiteFile siteFile = GetSiteFileFromTile(siteData.Tile);

            if (siteFile.owner != client.userFile.Username && FactionManager.GetFactionFromClient(client).factionMembers.Contains(siteFile.owner))
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.userFile.Username} tried to deposit a worker in the site at tile {siteData.Tile}, but the player {siteFile.owner} owns it");
            }

            else if (siteFile.workerData != null)
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.userFile.Username} tried to deposit a worker in the site at tile {siteData.Tile}, but the site already has a worker");
            }

            else
            {
                siteFile.workerData = siteData.WorkerData;
                SaveSite(siteFile);
            }
        }

        private static void RetrieveWorkerFromSite(ServerClient client, SiteData siteData)
        {
            SiteFile siteFile = GetSiteFileFromTile(siteData.Tile);

            if (siteFile.owner != client.userFile.Username && FactionManager.GetFactionFromClient(client).factionMembers.Contains(siteFile.owner))
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.userFile.Username} attempted to retrieve a worker from the site at tile {siteData.Tile}, but the player {siteFile.owner} of faction {siteFile.factionName} owns it");
            }

            else if (siteFile.workerData == null)
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.userFile.Username} attempted to retrieve a worker from the site at tile {siteData.Tile}, but it has no workers");
            }

            else
            {
                siteData.WorkerData = siteFile.workerData;
                siteFile.workerData = null;
                SaveSite(siteFile);

                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.SitePacket), siteData);
                client.Listener.EnqueuePacket(packet);
            }
        }

        public static void StartSiteTicker()
        {
            while (true)
            {
                Thread.Sleep(1800000);

                try { SiteRewardTick(); }
                catch (Exception e) { Logger.Error($"Site tick failed, this should never happen. Exception > {e}"); }
            }
        }

        public static void SiteRewardTick()
        {
            SiteFile[] sites = GetAllSites();

            SiteData siteData = new SiteData();
            siteData.SiteStepMode = SiteStepMode.Reward;

            foreach (ServerClient client in Network.connectedClients.ToArray())
            {
                siteData.SitesWithRewards.Clear();

                List<SiteFile> playerSites = sites.ToList().FindAll(x => x.owner == client.userFile.Username);
                foreach (SiteFile site in playerSites)
                {
                    if (site.workerData != null && !site.isFromFaction)
                    {
                        siteData.SitesWithRewards.Add(site.tile);
                    }
                }

                if (client.userFile.HasFaction)
                {
                    List<SiteFile> factionSites = sites.ToList().FindAll(x => x.factionName == client.userFile.FactionName);
                    foreach (SiteFile site in factionSites)
                    {
                        if (site.isFromFaction) siteData.SitesWithRewards.Add(site.tile);
                    }
                }

                if (siteData.SitesWithRewards.Count() > 0)
                {
                    Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.SitePacket), siteData);
                    client.Listener.EnqueuePacket(packet);
                }
            }

            Logger.Message($"[Site tick]");
        }
    }
}
