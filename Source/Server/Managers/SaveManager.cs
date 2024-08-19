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
                // Initialize the download manager if this is the first packet
                if (client.Listener.downloadManager == null)
                {
                    client.Listener.downloadManager = new DownloadManager();
                    client.Listener.downloadManager.PrepareDownload(tempClientSavePath, fileTransferData.FileParts);
                }

                client.Listener.downloadManager.WriteFilePart(fileTransferData.FileBytes);

                // Finalize the download if this is the last packet
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
                // Initialize the upload manager if this is the first packet
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

                // Finalize the upload if this is the last packet
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

            DeletePlayerData(client, client.userFile.Username);
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
            foreach (SettlementFile settlementFile in playerSettlements)
            {
                File.Copy(Path.Combine(Master.settlementsPath, settlementFile.tile + SettlementManager.fileExtension), Path.Combine(settlementsArchivePath, settlementFile.tile + SettlementManager.fileExtension));
            }
        }

        public static void DeletePlayerData(ServerClient client, string Username)
        {
            if (client != null) client.Listener.disconnectFlag = true;

            try { File.Delete(Path.Combine(Master.savesPath, Username + fileExtension)); }
            catch { Logger.Warning($"Failed to find {Username}'s save"); }

            MapManager.DeleteAllMapsFromUsername(Username);
            SiteManager.DeleteAllSitesFromUsername(Username);
            SettlementManager.DeleteAllSettlementsFromUsername(Username);

            Logger.Warning($"[Deleted player data] > {Username}");
        }
    }
}