using GameServer.Core;
using GameServer.Misc;
using GameServer.TCP;
using Shared;
using System;
using System.IO;
using static Shared.CommonEnumerators;

namespace GameServer.Managers
{
    public static class SaveManager
    {
        public readonly static string fileExtension      = ".mpsave";
        public readonly static string tempFileExtension  = ".mpsavetemp";

        // Handles both sending a save TO the client and receiving a save FROM the client.
        [HandlesPacket(PacketHeader.SaveManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes)
        {
            SaveData data = Serializer.ConvertBytesToObject<SaveData>(bytes);

            if (data._stepMode == SaveStepMode.Receive)
                SaveReceiverManager.ReceiveSaveFromClient(client, data);
            else if (data._stepMode == SaveStepMode.Send)
                SaveSenderManager.SendSaveToClient(client);
            else if (data._stepMode == SaveStepMode.Reset)
                ResetClientSave(client);
            else
                ResponseShortcutManager.SendIllegalPacket(client, "Received invalid step mode");
        }

        public static void OnUserSave(ServerClient client, SaveData fileTransferData)
        {
            if (fileTransferData._instructions == (int)SaveMode.Disconnect)
                client.Listener.DisconnectFlag = true;

            InformationDisplayer.DisplaySaveGame(client);
        }

        public static bool CheckIfUserHasSave(ServerClient client)
        {
            EnsureDirectoriesExist();

            string[] saves = Directory.GetFiles(Master.SavesPath);
            foreach (string save in saves)
            {
                if (!save.EndsWith(fileExtension)) continue;
                if (Path.GetFileNameWithoutExtension(save) == client.UserFile.Uid) return true;
            }

            return false;
        }

        public static byte[] GetUserSaveFromUsername(string username)
        {
            EnsureDirectoriesExist();

            string[] saves = Directory.GetFiles(Master.SavesPath);
            foreach (string save in saves)
            {
                if (!save.EndsWith(fileExtension)) continue;
                if (Path.GetFileNameWithoutExtension(save) == username)
                    return File.ReadAllBytes(save);
            }

            return null;
        }

        public static void ResetClientSave(ServerClient client)
        {
            EnsureDirectoriesExist();
            if (!CheckIfUserHasSave(client))
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.UserFile.Uid} has no save to reset.");
                return;
            }
            client.Listener.DisconnectFlag = true;
            ResetPlayerData(client, client.UserFile.Uid);
        }

        public static void ResetPlayerData(ServerClient client, string uid)
        {
            EnsureDirectoriesExist();
            BackupManager.BackupUser(uid);

            if (client != null)
                client.Listener.DisconnectFlag = true;

            // Delete the user’s save file
            try
            {
                File.Delete(Path.Combine(Master.SavesPath, uid + fileExtension));
            }
            catch
            {
                Printer.Warning($"Failed to delete save for {uid}");
            }

            // Delete site files, settlement files, etc.
            SiteFile[] playerSites = SiteManagerHelper.GetAllSitesFromUID(uid);
            foreach (SiteFile site in playerSites)
                SiteManager.DestroySiteFromFile(site);

            SettlementFile[] playerSettlements = SettlementManager.GetAllSettlementsFromUsername(uid);
            foreach (SettlementFile settlement in playerSettlements)
            {
                var settlementData = new PlayerSettlementData
                {
                    _settlementFile = { Tile = settlement.Tile, UID = settlement.UID }
                };
                SettlementManager.RemoveSettlement(client, settlementData);
            }

            InformationDisplayer.DisplayResetPlayer(uid);
        }

        private static void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(Master.SavesPath))
                Directory.CreateDirectory(Master.SavesPath);

            if (!Directory.Exists(Master.TempPath))
                Directory.CreateDirectory(Master.TempPath);
        }
    }

    public static class SaveSenderManager
    {
        public static void SendSaveToClient(ServerClient client)
        {
            EnsureDirectoriesExist();

            string baseClientSavePath = Path.Combine(Master.SavesPath, client.UserFile.Uid + SaveManager.fileExtension);

            InformationDisplayer.DisplayLoadGame(client);

            var data = new SaveData
            {
                _fileBytes = File.ReadAllBytes(baseClientSavePath),
                _stepMode  = SaveStepMode.Receive,
                _instructions = (!Master.ServerConfig.SyncLocalSave) ? SaveMode.Strict : SaveMode.Autosave
            };

            client.Listener.EnqueuePacket(PacketHeader.SaveManager, data);
        }

        private static void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(Master.SavesPath))
                Directory.CreateDirectory(Master.SavesPath);
        }
    }

    public static class SaveReceiverManager
    {
        public static void ReceiveSaveFromClient(ServerClient client, SaveData data)
        {
            EnsureDirectoriesExist();

            string baseClientSavePath = Path.Combine(Master.SavesPath, client.UserFile.Uid + SaveManager.fileExtension);
            string tempClientSavePath = Path.Combine(Master.TempPath, client.UserFile.Uid + SaveManager.tempFileExtension);

            File.WriteAllBytes(tempClientSavePath, data._fileBytes);
            OnSaveReceived(client, data, baseClientSavePath, tempClientSavePath);
        }

        private static void OnSaveReceived(ServerClient client, SaveData data, string baseClientSavePath, string tempClientSavePath)
        {
            byte[] completedSave = File.ReadAllBytes(tempClientSavePath);
            File.WriteAllBytes(baseClientSavePath, completedSave);
            File.Delete(tempClientSavePath);
            SaveManager.OnUserSave(client, data);
        }

        private static void EnsureDirectoriesExist()
        {
            if (!Directory.Exists(Master.SavesPath))
                Directory.CreateDirectory(Master.SavesPath);
            if (!Directory.Exists(Master.TempPath))
                Directory.CreateDirectory(Master.TempPath);
        }
    }
}