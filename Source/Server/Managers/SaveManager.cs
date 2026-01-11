using GameServer.Core;
using GameServer.Misc;
using MessagePack;
using Shared;
using Shared.Files;
using Shared.Files.Sites;
using Shared.Misc;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace GameServer.Managers
{
    public static class SaveManager
    {
        [HandlesPacket(PacketHeader.SaveManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes, PacketHeader header)
        {
            int read = SaveData.ParseSavePacket(bytes, out SaveDataHeader saveHeader);
            var save = bytes.AsSpan(read);
            switch (saveHeader._stepMode)
            {
                case SaveStepMode.Receive:
                    ReceiveSaveFromClient(client, saveHeader, save);
                    break;
                case SaveStepMode.Send:
                    SendSaveToClient(client);
                    break;
                case SaveStepMode.Reset:
                    ResetClientSave(client);
                    break;
                default:
                    ResponseShortcutManager.SendIllegalPacket(client, "Received invalid step mode");
                    break;
            }
        }
        
        public static void OnUserSave(ServerClient client, SaveDataHeader fileTransferData)
        {
            if (fileTransferData._forceDisconnect) client.Listener.DisconnectNow();
                    ResetClientSave(client);
        }

        public static bool CheckIfUserHasSave(ServerClient client)
        {
            string[] saves = Directory.GetFiles(Master.SavesPath);
            foreach (string save in saves)
            {
                if (Path.GetFileNameWithoutExtension(save) == client.UserFile.Username) return true;
            }

            return false;
        }

        public static void ResetClientSave(ServerClient client)
        {
            if (!CheckIfUserHasSave(client))
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.UserFile.Username}'s save was attempted to be reset while the player doesn't have a save");
                return;
            }

            client.Listener.DisconnectNow();
            ResetPlayerData(client, client.UserFile.Username);
        }

        public static void ResetPlayerData(ServerClient client, string username)
        {
            BackupManager.BackupUser(username);

            if (client != null) client.Listener.DisconnectNow();

            // Delete save file
            try { File.Delete(Path.Combine(Master.SavesPath, username + CommonValues.DefaultSaveFormat)); }
            catch { Printer.Warning($"Failed to find {client.UserFile.Username}'s save"); }

            // Delete site files
            SiteFile[] playerSites = SiteManagerHelper.GetAllSitesFromUsername(username);
            foreach (SiteFile site in playerSites) SiteManager.DestroySiteFromFile(site);

            // Delete settlement files
            SettlementFile[] playerSettlements = SettlementManager.GetAllSettlementsFromUsername(username);
            foreach (SettlementFile settlement in playerSettlements)
            {
                PlayerSettlementData settlementData = new PlayerSettlementData();
                settlementData._settlementFile.Tile = settlement.Tile;
                settlementData._settlementFile.Username = settlement.Username;

                SettlementManager.RemoveSettlement(client, settlementData);
            }

            InformationDisplayer.DisplayResetPlayer(username);
        }

        public static void SendSaveToClient(ServerClient client)
        {
            string savePath = Path.Combine(Master.SavesPath, client.UserFile.Username + CommonValues.DefaultSaveFormat);

            SaveData data = new SaveData();
            data._header = new SaveDataHeader(SaveStepMode.Receive);
            data._fileBytes = File.ReadAllBytes(savePath);
            if (!Master.ServerConfig.SyncLocalSave) data._header._forceUseSave = true;

            client.Listener.EnqueuePacket(PacketHeader.SaveManager, data.SerializeSavePacket());
        }
        
        public static void ReceiveSaveFromClient(ServerClient client, SaveDataHeader header, Span<byte> data)
        {
            string savePath = Path.Combine(Master.SavesPath, client.UserFile.Username + CommonValues.DefaultSaveFormat);
            using (var stream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
            {
                stream.Write(data);
            }

            InformationDisplayer.DisplaySaveGame(client);
            if (header._forceDisconnect) client.Listener.DisconnectNow();
        }
    }
}
