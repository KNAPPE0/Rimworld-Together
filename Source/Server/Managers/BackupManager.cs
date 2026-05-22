using GameServer.Core;
using GameServer.Misc;
using GameServer.PacketManager;
using Shared;
using Shared.Files;
using Shared.Files.Sites;
using Shared.Misc;
using System.IO.Compression;
using static Shared.Misc.Printer;

namespace GameServer.Managers
{
    public static class BackupManager
    {
        private static readonly Semaphore savingSemaphore = new Semaphore(1, 1);

        public static void BackupServer()
        {
            savingSemaphore.WaitOne();

            try
            {
                // Single DateTime.Now read — was sampling 6 times in the original format string.
                DateTime now = DateTime.Now;
                string backupName = $"Server_{now:yyyy-M-d_H-m-s}";
                string backupPath = Path.Combine(Master.BackupServerPath, backupName + CommonValues.CompressedSaveFormat);

                List<string> toArchive = new List<string>();
                toArchive.AddRange(Directory.GetFiles(Master.AssetsPath, "*.*", SearchOption.AllDirectories));
                toArchive.AddRange(Directory.GetFiles(Master.ConfigsPath, "*.*", SearchOption.AllDirectories));
                toArchive.AddRange(Directory.GetFiles(Master.LogsPath, "*.*", SearchOption.AllDirectories));

                CreateArchive(toArchive, backupPath);

                if (Master.BackupConfig.AutomaticDeletion && Directory.GetFiles(Master.BackupServerPath).Length > Master.BackupConfig.Amount)
                {
                    DeleteOldestArchive();
                }

                InformationDisplayer.DisplayServerBackup(backupPath);
            }
            catch (Exception ex) { Printer.Error(ex.ToString()); }

            savingSemaphore.Release();
        }

        public static void BackupUser(string username, bool persistent = false)
        {
            savingSemaphore.WaitOne();

            try
            {
                string playerArchivedSavePath = Path.Combine(Master.BackupUsersPath, username);
                if (persistent) playerArchivedSavePath += " - persistent";
                playerArchivedSavePath += CommonValues.CompressedSaveFormat;

                if (File.Exists(playerArchivedSavePath))
                {
                    if (persistent)
                    {
                        Printer.Error($"Could not backup user {username} because the file {playerArchivedSavePath} already exist. Consider running a non-persistent backup if you want to overwrite it.");
                        savingSemaphore.Release();
                        return;
                    }

                    File.Delete(playerArchivedSavePath);
                    Printer.Warning($"Deleting backup of {username} because he already had one.", LogImportanceMode.Verbose);
                }

                List<string> toArchive = new List<string>();

                string userFilePath = Path.Combine(Master.UsersPath, username + CommonValues.DefaultSaveFormat);
                if (File.Exists(userFilePath)) toArchive.Add(userFilePath);

                string userSavePath = Path.Combine(Master.SavesPath, username + CommonValues.DefaultSaveFormat);
                if (File.Exists(userSavePath)) toArchive.Add(userSavePath);

                SiteFile[] playerSites = SiteManagerHelper.GetAllSitesFromUsername(username);
                foreach (SiteFile site in playerSites) toArchive.Add(Path.Combine(Master.SitesPath, site.Tile + CommonValues.DefaultSaveFormat));

                SettlementFile[] playerSettlements = PM_Settlements.GetAllSettlementsFromUsername(username);
                foreach (SettlementFile settlementFile in playerSettlements) toArchive.Add(Path.Combine(Master.SettlementsPath, settlementFile.Tile + CommonValues.DefaultSaveFormat));

                CreateArchive(toArchive, playerArchivedSavePath);

                InformationDisplayer.DisplayUserBackup(playerArchivedSavePath);
            }
            catch (Exception ex) { Printer.Error(ex.ToString()); }

            savingSemaphore.Release();
        }

        private static void CreateArchive(List<string> files, string toPath)
        {
            using FileStream zip = new FileStream(toPath, FileMode.CreateNew);
            using ZipArchive archive = new ZipArchive(zip, ZipArchiveMode.Create);

            foreach (string file in files)
            {
                if (File.Exists(file))
                {
                    string relativePath = Path.GetRelativePath(Master.MainPath, file);
                    archive.CreateEntryFromFile(file, relativePath);
                }
            }
        }

        private static void DeleteOldestArchive()
        {
            DirectoryInfo dir = new DirectoryInfo(Master.BackupServerPath);
            while (Directory.GetFiles(Master.BackupServerPath).Length > Master.BackupConfig.Amount)
            {
                FileSystemInfo fileInfo = dir.GetFileSystemInfos().OrderBy(f => f.CreationTime).FirstOrDefault();
                if (fileInfo == null) break;
                Printer.Warning($"Deleting backup {fileInfo.Name} because we've reached the limit of {Master.BackupConfig.Amount}", LogImportanceMode.Verbose);
                fileInfo.Delete();
            }
        }

        public static void StartFeature()
        {
            if (!Master.BackupConfig.AutomaticBackups) return;
            while (true)
            {
                Thread.Sleep(TimeSpan.FromHours(Master.BackupConfig.IntervalHours));
                BackupServer();
            }
        }
    }
}
