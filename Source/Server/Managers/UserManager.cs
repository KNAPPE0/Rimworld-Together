using GameServer.Core;
using TCPNetwork.Packets;
using Shared;
using Shared.Files;
using TCPNetwork.Files.Client;
using Shared.Files.Sites;
using Shared.Misc;
using GameServer.Hooks.TCPNetwork;
using GameServer.PacketManager;
using static TCPNetwork.Packets.PKT_Login;

namespace GameServer.Managers
{

    public static class UserManager
    {
        public static void SendPlayerRecount()
        {
            PKT_PlayerRecount playerRecountData = new PKT_PlayerRecount();
            playerRecountData.CurrentPlayerCount = ServerNetwork.GetConnectedClients().Count();
            ServerNetwork.SendPacketToAllClients(PacketHeader.RecountManager, playerRecountData);
        }

        public static void BanPlayerFromName(string username)
        {
            UserFile userFile = UserManagerH.GetUserFileFromName(username);
            if (userFile == null)
            {
                Printer.Warning($"User '{username}' was not found");
                return;
            }

            if (userFile.IsBanned)
            {
                Printer.Warning($"User '{userFile.Username}' is already banned from the server");
                return;
            }

            userFile.UpdateBan(true);
            Printer.Warning($"User '{userFile.Username}' has been banned from the server (IP: {userFile.LatestIP})");

            // Kick if currently online
            ServerClient client = ServerNetwork.GetConnectedClientFromUsername(username);
            if (client != null)
            {
                client.Listener.MarkForDisconnect();
                Printer.Warning($"User '{userFile.Username}' was also kicked (was online)");
            }
        }

        public static void PardonPlayerFromName(string username)
        {
            UserFile userFile = UserManagerH.GetUserFileFromName(username);
            if (userFile == null) Printer.Warning($"User '{CMD_Base.CommandParameters[0]}' was not found");
            else
            {
                if (!userFile.IsBanned) Printer.Warning($"User '{userFile.Username}' is not banned from the server");
                else
                {
                    userFile.UpdateBan(false);
                    Printer.Warning($"User '{userFile.Username}' has been pardoned from the server");
                }
            }
        }
    }

    public static class UserManagerH
    {
        public static UserFile GetUserFile(ServerClient client)
        {
            string[] userFiles = Directory.GetFiles(Master.UsersPath);

            foreach (string userFile in userFiles)
            {
                UserFile file = Serializer.SerializeFromFile<UserFile>(userFile);
                if (file.Username == client.UserFile.Username) return file;
            }

            return null;
        }

        public static UserFile GetUserFileFromName(string username)
        {
            string[] userFiles = Directory.GetFiles(Master.UsersPath);

            foreach (string userFile in userFiles)
            {
                UserFile file = Serializer.SerializeFromFile<UserFile>(userFile);
                if (file.Username == username) return file;
            }

            return null;
        }

        public static UserFile[] GetAllUserFiles()
        {
            List<UserFile> userFiles = new List<UserFile>();

            string[] existingUsers = Directory.GetFiles(Master.UsersPath);
            foreach (string user in existingUsers) userFiles.Add(Serializer.SerializeFromFile<UserFile>(user));
            return userFiles.ToArray();
        }

        public static bool CheckIfUserIsConnected(string username)
        {
            ServerClient toGet = ServerNetwork.GetConnectedClientFromUsername(username);
            if (toGet != null) return true;
            else return false;
        }

        public static bool CheckIfUserExists(ServerClient client, PKT_Login data)
        {
            UserFile toFind = GetAllUserFiles().FirstOrDefault(fetch => fetch.Username.ToLower() == data._username.ToLower());
            if (toFind != null) return true;
            else return false;
        }

        public static bool CheckIfUserAuthCorrect(ServerClient client, PKT_Login data)
        {
            UserFile toFind = GetAllUserFiles().FirstOrDefault(fetch => fetch.Username == data._username && fetch.Password == data._password);
            if (toFind != null) return true;
            else
            {
                PM_Logins.DenyConnectionWithReason(client, LoginResponse.Invalid);
                return false;

            }
        }

        public static bool CheckIfUserBanned(ServerClient client)
        {
            if (!client.UserFile.IsBanned) return false;
            else
            {
                Printer.Message($"Banned user '{client.UserFile.Username}' tried to join the server");
                PM_Logins.DenyConnectionWithReason(client, LoginResponse.Ban);
                return true;
            }
        }

        public static bool CheckWhitelist(ServerClient client)
        {
            if (!Master.Whitelist.UseWhitelist) return true;
            else if (Master.Whitelist.WhitelistedUsers.ToArray().FirstOrDefault(fetch => fetch == client.UserFile.Username) != null) return true;
            else
            {
                PM_Logins.DenyConnectionWithReason(client, LoginResponse.Whitelist);
                return false;
            }
        }

        public static int[] GetUserStructuresTilesFromUsername(string username)
        {
            SettlementFile[] settlements = PM_Settlements.GetAllSettlements().ToList().FindAll(x => x.Username == username).ToArray();
            SiteFile[] sites = SiteManagerHelper.GetAllSites().ToList().FindAll(x => x.Username == username).ToArray();

            List<int> tilesToExclude = new List<int>();
            foreach (SettlementFile settlement in settlements) tilesToExclude.Add(settlement.Tile);
            foreach (SiteFile site in sites) tilesToExclude.Add(site.Tile);

            return tilesToExclude.ToArray();
        }
    }
}
