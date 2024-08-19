using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class FactionManager
    {
        public readonly static string fileExtension = ".mpfaction";

        public static void ParseFactionPacket(ServerClient client, Packet packet)
        {
            PlayerFactionData factionManifest = Serializer.ConvertBytesToObject<PlayerFactionData>(packet.Contents);

            switch(factionManifest.manifestMode)
            {
                case FactionManifestMode.Create:
                    CreateFaction(client, factionManifest);
                    break;
                case FactionManifestMode.Delete:
                    DeleteFaction(client, factionManifest);
                    break;
                case FactionManifestMode.AddMember:
                    AddMemberToFaction(client, factionManifest);
                    break;
                case FactionManifestMode.RemoveMember:
                    RemoveMemberFromFaction(client, factionManifest);
                    break;
                case FactionManifestMode.AcceptInvite:
                    ConfirmAddMemberToFaction(client, factionManifest);
                    break;
                case FactionManifestMode.Promote:
                    PromoteMember(client, factionManifest);
                    break;
                case FactionManifestMode.Demote:
                    DemoteMember(client, factionManifest);
                    break;
                case FactionManifestMode.MemberList:
                    SendFactionMemberList(client, factionManifest);
                    break;
            }
        }

        private static FactionFile[] GetAllFactions()
        {
            List<FactionFile> factionFiles = new List<FactionFile>();

            string[] factions = Directory.GetFiles(Master.factionsPath);
            foreach(string faction in factions)
            {
                if (!faction.EndsWith(fileExtension)) continue;
                factionFiles.Add(Serializer.SerializeFromFile<FactionFile>(faction));
            }

            return factionFiles.ToArray();
        }

        public static FactionFile GetFactionFromClient(ServerClient client)
        {
            string[] factions = Directory.GetFiles(Master.factionsPath);
            foreach (string faction in factions)
            {
                if (!faction.EndsWith(fileExtension)) continue;

                FactionFile factionFile = Serializer.SerializeFromFile<FactionFile>(faction);
                if (factionFile.factionName == client.userFile.FactionName) return factionFile;
            }

            return null;
        }

        public static FactionFile GetFactionFromFactionName(string factionName)
        {
            string[] factions = Directory.GetFiles(Master.factionsPath);
            foreach (string faction in factions)
            {
                if (!faction.EndsWith(fileExtension)) continue;

                FactionFile factionFile = Serializer.SerializeFromFile<FactionFile>(faction);
                if (factionFile.factionName == factionName) return factionFile;
            }

            return null;
        }

        public static bool CheckIfUserIsInFaction(FactionFile factionFile, string UsernameToCheck)
        {
            foreach(string str in factionFile.factionMembers)
            {
                if (str == UsernameToCheck) return true;
            }

            return false;
        }

        public static FactionRanks GetMemberRank(FactionFile factionFile, string UsernameToCheck)
        {
            for(int i = 0; i < factionFile.factionMembers.Count(); i++)
            {
                if (factionFile.factionMembers[i] == UsernameToCheck)
                {
                    return (FactionRanks)int.Parse(factionFile.factionMemberRanks[i]);
                }
            }

            return FactionRanks.Member;
        }
        private static readonly object factionLock = new object();
        public static void SaveFactionFile(FactionFile factionFile)
        {
            lock (factionLock)
            {
                try
                {
                    string savePath = Path.Combine(Master.factionsPath, factionFile.factionName + fileExtension);
                    Serializer.SerializeToFile(savePath, factionFile);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to save faction file for {factionFile.factionName}: {ex.Message}");
                    throw; // Optional: rethrow or handle specific exceptions
                }

            }
        }
        private static bool CheckIfFactionExistsByName(string nameToCheck)
        {
            FactionFile[] factions = GetAllFactions();
            foreach(FactionFile faction in factions)
            {
                if (faction.factionName == nameToCheck) return true;
            }

            return false;
        }

        private static void CreateFaction(ServerClient client, PlayerFactionData factionManifest)
        {
            if (CheckIfFactionExistsByName(factionManifest.manifestDataString))
            {
                factionManifest.manifestMode = FactionManifestMode.NameInUse;

                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), factionManifest);
                client.Listener.EnqueuePacket(packet);
            }

            else
            {
                factionManifest.manifestMode = FactionManifestMode.Create;

                FactionFile factionFile = new FactionFile();
                factionFile.factionName = factionManifest.manifestDataString;
                factionFile.factionMembers.Add(client.userFile.Username);
                factionFile.factionMemberRanks.Add(((int)FactionRanks.Admin).ToString());
                SaveFactionFile(factionFile);

                client.userFile.UpdateFaction(factionFile.factionName);

                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), factionManifest);
                client.Listener.EnqueuePacket(packet);

                Logger.Warning($"[Created faction] > {client.userFile.Username} > {factionFile.factionName}");
            }
        }
        private static void DeleteFaction(ServerClient client, PlayerFactionData factionManifest)
        {
            if (!CheckIfFactionExistsByName(client.userFile.FactionName)) return;
            else
            {
                FactionFile factionFile = GetFactionFromClient(client);

                if (factionFile == null)
                {
                    Logger.Error($"Faction not found for client: {client.userFile.Username}");
                    return;
                }

                if (GetMemberRank(factionFile, client.userFile.Username) != FactionRanks.Admin)
                {
                    ResponseShortcutManager.SendNoPowerPacket(client, factionManifest);
                }

                else
                {
                    factionManifest.manifestMode = FactionManifestMode.Delete;

                    UserFile[] userFiles = UserManager.GetAllUserFiles();
                    foreach (UserFile userFile in userFiles)
                    {
                        if (userFile.FactionName == client.userFile.FactionName) userFile.UpdateFaction(null);
                    }

                    Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), factionManifest);
                    foreach (string str in factionFile.factionMembers)
                    {
                        ServerClient cClient = Network.connectedClients.ToList().Find(x => x.userFile.Username == str);
                        if (cClient != null)
                        {
                            cClient.userFile.UpdateFaction(null);
                            cClient.Listener.EnqueuePacket(packet);
                            GoodwillManager.UpdateClientGoodwills(cClient);
                        }
                    }

                    SiteFile[] factionSites = GetFactionSites(factionFile);
                    foreach(SiteFile site in factionSites) SiteManager.DestroySiteFromFile(site);

                    DeleteFactionFile(factionFile.factionName); // Updated to use DeleteFactionFile method
                }
            }
        }

        private static void DeleteFactionFile(string factionName)
        {
            try
            {
                string filePath = Path.Combine(Master.factionsPath, factionName + fileExtension);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.Warning($"Successfully deleted faction file: {factionName}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to delete faction file for {factionName}: {ex.Message}");
            }
        }
        private static void AddMemberToFaction(ServerClient client, PlayerFactionData factionManifest)
        {
            FactionFile factionFile = GetFactionFromClient(client);
            if (factionFile == null)
            {   
                Logger.Error($"Faction not found for client: {client.userFile.Username}");
                return;
            }
            SettlementFile settlementFile = SettlementManager.GetSettlementFileFromTile(factionManifest.manifestDataInt);
            ServerClient toAdd = UserManager.GetConnectedClientFromUsername(settlementFile.owner);

            if (factionFile == null) return;
            if (toAdd == null) return;

            if (GetMemberRank(factionFile, client.userFile.Username) == FactionRanks.Member) ResponseShortcutManager.SendNoPowerPacket(client, factionManifest);
            else
            {
                if (toAdd.userFile.HasFaction) return;
                else
                {
                    if (factionFile.factionMembers.Contains(toAdd.userFile.Username)) return;
                    else
                    {
                        factionManifest.manifestDataString = factionFile.factionName;
                        Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), factionManifest);
                        toAdd.Listener.EnqueuePacket(packet);
                    }
                }
            }
        }

        private static void ConfirmAddMemberToFaction(ServerClient client, PlayerFactionData factionManifest)
        {
            FactionFile factionFile = GetFactionFromFactionName(factionManifest.manifestDataString);

            if (factionFile == null) return;
            else
            {
                if (!factionFile.factionMembers.Contains(client.userFile.Username))
                {
                    factionFile.factionMembers.Add(client.userFile.Username);
                    factionFile.factionMemberRanks.Add(((int)FactionRanks.Member).ToString());
                    SaveFactionFile(factionFile);

                    client.userFile.UpdateFaction(factionFile.factionName);

                    GoodwillManager.ClearAllFactionMemberGoodwills(factionFile);

                    ServerClient[] members = GetAllConnectedFactionMembers(factionFile);
                    foreach (ServerClient member in members) GoodwillManager.UpdateClientGoodwills(member);
                }
            }
        }

        private static void RemoveMemberFromFaction(ServerClient client, PlayerFactionData factionManifest)
        {
            FactionFile factionFile = GetFactionFromClient(client);
            SettlementFile settlementFile = SettlementManager.GetSettlementFileFromTile(factionManifest.manifestDataInt);
            UserFile toRemoveLocal = UserManager.GetUserFileFromName(settlementFile.owner);
            ServerClient toRemove = UserManager.GetConnectedClientFromUsername(settlementFile.owner);

            if (GetMemberRank(factionFile, client.userFile.Username) == FactionRanks.Member)
            {
                if (settlementFile.owner == client.userFile.Username) RemoveFromFaction();
                else ResponseShortcutManager.SendNoPowerPacket(client, factionManifest);
            }

            else if (GetMemberRank(factionFile, client.userFile.Username) == FactionRanks.Moderator)
            {
                if (settlementFile.owner == client.userFile.Username) RemoveFromFaction();
                else
                {
                    if (GetMemberRank(factionFile, settlementFile.owner) != FactionRanks.Member)
                        ResponseShortcutManager.SendNoPowerPacket(client, factionManifest);

                    else RemoveFromFaction();
                }
            }

            else if (GetMemberRank(factionFile, client.userFile.Username) == FactionRanks.Admin)
            {
                if (settlementFile.owner == client.userFile.Username)
                {
                    factionManifest.manifestMode = FactionManifestMode.AdminProtection;
                    Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), factionManifest);
                    client.Listener.EnqueuePacket(packet);
                }
                else RemoveFromFaction();
            }

            void RemoveFromFaction()
            {
                if (!factionFile.factionMembers.Contains(settlementFile.owner)) return;
                else
                {
                    if (toRemove != null)
                    {
                        toRemove.userFile.UpdateFaction(null);

                        Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), factionManifest);
                        toRemove.Listener.EnqueuePacket(packet);
                        GoodwillManager.UpdateClientGoodwills(toRemove);
                    }

                    if (toRemoveLocal == null) return;
                    else
                    {
                        toRemoveLocal.UpdateFaction(null);

                        for (int i = 0; i < factionFile.factionMembers.Count(); i++)
                        {
                            if (factionFile.factionMembers[i] == toRemoveLocal.Username)
                            {
                                factionFile.factionMembers.RemoveAt(i);
                                factionFile.factionMemberRanks.RemoveAt(i);
                                SaveFactionFile(factionFile);
                                break;
                            }
                        }
                    }

                    ServerClient[] members = GetAllConnectedFactionMembers(factionFile);
                    foreach (ServerClient member in members) GoodwillManager.UpdateClientGoodwills(member);
                }
            }
        }

        private static void PromoteMember(ServerClient client, PlayerFactionData factionManifest)
        {
            SettlementFile settlementFile = SettlementManager.GetSettlementFileFromTile(factionManifest.manifestDataInt);
            UserFile userFile = UserManager.GetUserFileFromName(settlementFile.owner);
            FactionFile factionFile = GetFactionFromClient(client);

            if (GetMemberRank(factionFile, client.userFile.Username) == FactionRanks.Member)
            {
                ResponseShortcutManager.SendNoPowerPacket(client, factionManifest);
            }

            else
            {
                if (!factionFile.factionMembers.Contains(userFile.Username)) return;
                else
                {
                    if (GetMemberRank(factionFile, settlementFile.owner) == FactionRanks.Admin)
                    {
                        ResponseShortcutManager.SendNoPowerPacket(client, factionManifest);
                    }

                    else
                    {
                        for (int i = 0; i < factionFile.factionMembers.Count(); i++)
                        {
                            if (factionFile.factionMembers[i] == userFile.Username)
                            {
                                factionFile.factionMemberRanks[i] = "1";
                                SaveFactionFile(factionFile);
                                break;
                            }
                        }
                    }
                }
            }
        }

        private static void DemoteMember(ServerClient client, PlayerFactionData factionManifest)
        {
            SettlementFile settlementFile = SettlementManager.GetSettlementFileFromTile(factionManifest.manifestDataInt);
            UserFile userFile = UserManager.GetUserFileFromName(settlementFile.owner);
            FactionFile factionFile = GetFactionFromClient(client);

            if (GetMemberRank(factionFile, client.userFile.Username) != FactionRanks.Admin)
            {
                ResponseShortcutManager.SendNoPowerPacket(client, factionManifest);
            }

            else
            {
                if (!factionFile.factionMembers.Contains(userFile.Username)) return;
                else
                {
                    for (int i = 0; i < factionFile.factionMembers.Count(); i++)
                    {
                        if (factionFile.factionMembers[i] == userFile.Username)
                        {
                            factionFile.factionMemberRanks[i] = "0";
                            SaveFactionFile(factionFile);
                            break;
                        }
                    }
                }
            }
        }

        private static SiteFile[] GetFactionSites(FactionFile factionFile)
        {
            SiteFile[] sites = SiteManager.GetAllSites();
            List<SiteFile> factionSites = new List<SiteFile>();
            foreach(SiteFile site in sites)
            {
                if (site.isFromFaction && site.factionName == factionFile.factionName)
                {
                    factionSites.Add(site);
                }
            }

            return factionSites.ToArray();
        }

        private static ServerClient[] GetAllConnectedFactionMembers(FactionFile factionFile)
        {
            List<ServerClient> connectedFactionMembers = new List<ServerClient>();
            foreach (ServerClient client in Network.connectedClients.ToArray())
            {
                if (factionFile.factionMembers.Contains(client.userFile.Username))
                {
                    connectedFactionMembers.Add(client);
                }
            }

            return connectedFactionMembers.ToArray();
        }

        private static void SendFactionMemberList(ServerClient client, PlayerFactionData factionManifest)
        {
            FactionFile factionFile = GetFactionFromClient(client);

            foreach(string str in factionFile.factionMembers)
            {
                factionManifest.manifestComplexData.Add(str);
                factionManifest.manifestSecondaryComplexData.Add(((int)GetMemberRank(factionFile, str)).ToString());
            }

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.FactionPacket), factionManifest);
            client.Listener.EnqueuePacket(packet);
        }
    }
}