using GameServer.Core;
using GameServer.Managers;
using GameServer.Misc;
using Shared;
using Shared.Files;
using Shared.Files.Sites;
using Shared.Misc;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;
using static TCPNetwork.Packets.PKT_Save;

namespace GameServer.PacketManager
{
    public class PM_Saves : PM_Base
    {
        [HandlesPacket(PacketHeader.SaveManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header)
        {
            PKT_Save data = Serializer.ConvertBytesToObject<PKT_Save>(bytes);
            if (data == null) return;

            switch (data._stepMode)
            {
                case SaveStepMode.Receive:
                    ReceiveSaveFromClient(client, data);
                    break;

                case SaveStepMode.Reset:
                    ResetClientSave(client);
                    break;
            }
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

            client.Listener.MarkForDisconnect();
            ResetPlayerData(client, client.UserFile.Username);
        }

        public static void ResetPlayerData(ServerClient client, string username)
        {
            BackupManager.BackupUser(username);

            if (client != null) client.Listener.MarkForDisconnect();

            // Delete save file
            string path = Path.Combine(Master.SavesPath, username + CommonValues.DefaultSaveFormat);
            if (File.Exists(path)) File.Delete(path);

            // Delete site files
            SiteFile[] playerSites = SiteManagerHelper.GetAllSitesFromUsername(username);
            foreach (SiteFile site in playerSites) PM_Sites.DestroySiteFromFile(site);

            // Delete settlement files
            SettlementFile[] playerSettlements = PM_Settlements.GetAllSettlementsFromUsername(username);
            foreach (SettlementFile settlement in playerSettlements)
            {
                PKT_PlayerSettlement settlementData = new PKT_PlayerSettlement();
                settlementData._settlementFile.Tile = settlement.Tile;
                settlementData._settlementFile.Username = settlement.Username;

                PM_Settlements.RemoveSettlement(client, settlementData);
            }

            InformationDisplayer.DisplayResetPlayer(username);
        }

        public static void SendSaveToClient(ServerClient client)
        {
            string savePath = Path.Combine(Master.SavesPath, client.UserFile.Username + CommonValues.DefaultSaveFormat);

            PKT_Save data = new PKT_Save();
            data._stepMode = SaveStepMode.Receive;
            data._fileBytes = File.ReadAllBytes(savePath);
            if (!Master.ServerConfig.SyncLocalSave) data._forceUseSave = true;

            client.Listener.EnqueuePacket(PacketHeader.SaveManager, data);
        }
        
        public static void ReceiveSaveFromClient(ServerClient client, PKT_Save data)
        {
            string savePath = Path.Combine(Master.SavesPath, client.UserFile.Username + CommonValues.DefaultSaveFormat);
            File.WriteAllBytes(savePath, data._fileBytes);

            InformationDisplayer.DisplaySaveGame(client);
            if (data._forceDisconnect) client.Listener.MarkForDisconnect();
        }
    }
}
