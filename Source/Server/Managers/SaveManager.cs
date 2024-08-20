using Shared;
using static Shared.CommonEnumerators;
using System.IO;

namespace GameServer
{
    public static class SaveManager
    {
        public readonly static string fileExtension = ".mpsave";
        private readonly static string tempFileExtension = ".mpsavetemp";
        private static readonly object saveLock = new object();

        public static void ReceiveSavePartFromClient(ServerClient client, Packet packet)
        {
            string baseClientSavePath = Path.Combine(Master.savesPath, client.userFile.Username + fileExtension);
            string tempClientSavePath = Path.Combine(Master.savesPath, client.userFile.Username + tempFileExtension);

            FileTransferData fileTransferData = Serializer.ConvertBytesToObject<FileTransferData>(packet.Contents);

            lock (saveLock)
            {
                if (client.Listener.downloadManager == null)
                {
                    client.Listener.downloadManager = new DownloadManager();
                    client.Listener.downloadManager.PrepareDownload(tempClientSavePath, fileTransferData.FileParts);
                }

                client.Listener.downloadManager.WriteFilePart(fileTransferData.FileBytes);

                if (fileTransferData.IsLastPart)
                {
                    client.Listener.downloadManager.FinishFileWrite();
                    client.Listener.downloadManager = null;

                    byte[] completedSave = File.ReadAllBytes(tempClientSavePath);
                    File.WriteAllBytes(baseClientSavePath, completedSave);
                    File.Delete(tempClientSavePath);

                    OnUserSave(client, fileTransferData);
                }
                else
                {
                    Packet rPacket = Packet.CreatePacketFromObject(nameof(PacketHandler.RequestSavePartPacket));
                    client.Listener.EnqueuePacket(rPacket);
                }
            }
        }

        public static void SendSavePartToClient(ServerClient client)
        {
            string baseClientSavePath = Path.Combine(Master.savesPath, client.userFile.Username + fileExtension);

            lock (saveLock)
            {
                if (client.Listener.uploadManager == null)
                {
                    Logger.Message($"[Load save] > {client.userFile.Username} | {client.userFile.SavedIP}");

                    client.Listener.uploadManager = new UploadManager();
                    client.Listener.uploadManager.PrepareUpload(baseClientSavePath);
                }

                FileTransferData fileTransferData = new FileTransferData
                {
                    FileSize = client.Listener.uploadManager.FileSize,
                    FileParts = client.Listener.uploadManager.FileParts,
                    FileBytes = client.Listener.uploadManager.ReadFilePart(),
                    IsLastPart = client.Listener.uploadManager.IsLastPart
                };

                if (!Master.serverConfig.SyncLocalSave)
                {
                    fileTransferData.Instructions = (int)SaveMode.Strict;
                }

                Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.ReceiveSavePartPacket), fileTransferData);
                client.Listener.EnqueuePacket(packet);

                if (client.Listener.uploadManager.IsLastPart)
                {
                    client.Listener.uploadManager = null;
                }
            }
        }

        private static void OnUserSave(ServerClient client, FileTransferData fileTransferData)
        {
            if (fileTransferData.Instructions == (int)SaveMode.Disconnect)
            {
                client.Listener.disconnectFlag = true;
                Logger.Message($"[Save game] > {client.userFile.Username} > Disconnect");
            }
            else
            {
                Logger.Message($"[Save game] > {client.userFile.Username} > Autosave");
            }
        }

        public static bool CheckIfUserHasSave(ServerClient client)
        {
            string[] saves = Directory.GetFiles(Master.savesPath);
            foreach (string save in saves)
            {
                if (save.EndsWith(fileExtension) && Path.GetFileNameWithoutExtension(save) == client.userFile.Username)
                {
                    return true;
                }
            }

            return false;
        }

        public static byte[] GetUserSaveFromUsername(string Username)
        {
            string savePath = Path.Combine(Master.savesPath, Username + fileExtension);
            return File.Exists(savePath) ? File.ReadAllBytes(savePath) : null;
        }

        public static void ResetClientSave(ServerClient client)
        {
            if (!CheckIfUserHasSave(client))
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.userFile.Username}'s save was attempted to be reset while the player doesn't have a save");
                return;
            }

            client.Listener.disconnectFlag = true;

            string playerArchivedSavePath = Path.Combine(Master.backupUsersPath, client.userFile.Username);
            if (Directory.Exists(playerArchivedSavePath)) Directory.Delete(playerArchivedSavePath, true);
            Directory.CreateDirectory(playerArchivedSavePath);

            ArchivePlayerData(client, playerArchivedSavePath);
            DeletePlayerData(client.userFile.Username);
        }

        private static void ArchivePlayerData(ServerClient client, string playerArchivedSavePath)
        {
            string mapsArchivePath = Path.Combine(playerArchivedSavePath, "Maps");
            string savesArchivePath = Path.Combine(playerArchivedSavePath, "Saves");
            string sitesArchivePath = Path.Combine(playerArchivedSavePath, "Sites");
            string settlementsArchivePath = Path.Combine(playerArchivedSavePath, "Settlements");

            Directory.CreateDirectory(mapsArchivePath);
            Directory.CreateDirectory(savesArchivePath);
            Directory.CreateDirectory(sitesArchivePath);
            Directory.CreateDirectory(settlementsArchivePath);

            try
            {
                File.Copy(Path.Combine(Master.savesPath, client.userFile.Username + fileExtension), Path.Combine(savesArchivePath, client.userFile.Username + fileExtension));
            }
            catch
            {
                Logger.Warning($"Failed to find {client.userFile.Username}'s save");
            }

            ArchiveMaps(client.userFile.Username, mapsArchivePath);
            ArchiveSites(client.userFile.Username, sitesArchivePath);
            ArchiveSettlements(client.userFile.Username, settlementsArchivePath);
        }

        private static void ArchiveMaps(string Username, string mapsArchivePath)
        {
            MapFileData[] userMaps = MapManager.GetAllMapsFromUsername(Username);
            foreach (MapFileData map in userMaps)
            {
                File.Copy(Path.Combine(Master.mapsPath, map.MapTile + MapManager.fileExtension), Path.Combine(mapsArchivePath, map.MapTile + MapManager.fileExtension));
            }
        }

        private static void ArchiveSites(string Username, string sitesArchivePath)
        {
            SiteFile[] playerSites = SiteManager.GetAllSitesFromUsername(Username);
            foreach (SiteFile site in playerSites)
            {
                File.Copy(Path.Combine(Master.sitesPath, site.tile + SiteManager.fileExtension), Path.Combine(sitesArchivePath, site.tile + SiteManager.fileExtension));
            }
        }

        private static void ArchiveSettlements(string Username, string settlementsArchivePath)
        {
            SettlementFile[] playerSettlements = SettlementManager.GetAllSettlementsFromUsername(Username);
            foreach (SettlementFile settlement in playerSettlements)
            {
                File.Copy(Path.Combine(Master.settlementsPath, settlement.tile + SettlementManager.fileExtension), Path.Combine(settlementsArchivePath, settlement.tile + SettlementManager.fileExtension));
            }
        }

        private static void DeletePlayerData(string username)
        {
            string savePath = Path.Combine(Master.savesPath, username + fileExtension);
            if (File.Exists(savePath)) File.Delete(savePath);

            MapFileData[] maps = MapManager.GetAllMapsFromUsername(username);
            foreach (MapFileData map in maps)
            {
                string mapPath = Path.Combine(Master.mapsPath, map.MapTile + MapManager.fileExtension);
                if (File.Exists(mapPath)) File.Delete(mapPath);
            }

            SiteFile[] sites = SiteManager.GetAllSitesFromUsername(username);
            foreach (SiteFile site in sites)
            {
                string sitePath = Path.Combine(Master.sitesPath, site.tile + SiteManager.fileExtension);
                if (File.Exists(sitePath)) File.Delete(sitePath);
            }

            SettlementFile[] settlements = SettlementManager.GetAllSettlementsFromUsername(username);
            foreach (SettlementFile settlement in settlements)
            {
                string settlementPath = Path.Combine(Master.settlementsPath, settlement.tile + SettlementManager.fileExtension);
                if (File.Exists(settlementPath)) File.Delete(settlementPath);
            }

            Logger.Warning($"[Reset player data] > {username}");
        }

        public static void ResetPlayerData(ServerClient client, string username)
        {
            ArchivePlayerData(client, Path.Combine(Master.backupUsersPath, username));
            DeletePlayerData(username);
        }
    }
}