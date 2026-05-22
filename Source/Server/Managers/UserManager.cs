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
            // .Count on the concurrent dict beats allocating an array just to count it.
            playerRecountData.CurrentPlayerCount = TCPNetwork.Network.ServerClients.Count;
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
            UserManagerH.InvalidateUserCache();
            Printer.Warning($"User '{userFile.Username}' has been banned from the server (IP: {userFile.LatestIP})");

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
                    UserManagerH.InvalidateUserCache();
                    Printer.Warning($"User '{userFile.Username}' has been pardoned from the server");
                }
            }
        }
    }

    public static class UserManagerH
    {
        // In-memory cache keyed case-insensitively. Disk reads only on miss / invalidate.
        private static readonly object UserCacheLock = new object();
        private static Dictionary<string, UserFile> UserCache;

        static UserManagerH()
        {
            UserFile.OnUserFileSaved += saved =>
            {
                if (saved == null || string.IsNullOrEmpty(saved.Username)) return;
                lock (UserCacheLock)
                {
                    if (UserCache != null) UserCache[saved.Username] = saved;
                }
            };
        }

        public static void InvalidateUserCache()
        {
            lock (UserCacheLock) { UserCache = null; }
        }

        // Caller MUST hold UserCacheLock.
        private static Dictionary<string, UserFile> LoadCacheFromDiskLocked()
        {
            Dictionary<string, UserFile> cache = new Dictionary<string, UserFile>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (string userPath in Directory.GetFiles(Master.UsersPath))
                {
                    try
                    {
                        UserFile file = Serializer.SerializeFromFile<UserFile>(userPath);
                        if (file != null && !string.IsNullOrEmpty(file.Username))
                            cache[file.Username] = file;
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Printer.Error($"Users could not be loaded: {ex.Message}"); }
            UserCache = cache;
            return cache;
        }

        private static Dictionary<string, UserFile> GetUserCache()
        {
            lock (UserCacheLock)
            {
                return UserCache ?? LoadCacheFromDiskLocked();
            }
        }

        public static UserFile GetUserFile(ServerClient client)
        {
            if (client?.UserFile?.Username == null) return null;
            GetUserCache().TryGetValue(client.UserFile.Username, out UserFile file);
            return file;
        }

        public static UserFile GetUserFileFromName(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;
            GetUserCache().TryGetValue(username, out UserFile file);
            return file;
        }

        public static UserFile[] GetAllUserFiles()
        {
            // Copy under the writer's lock — Dictionary enumerator throws if cache mutates mid-iteration.
            lock (UserCacheLock)
            {
                Dictionary<string, UserFile> cache = UserCache ?? LoadCacheFromDiskLocked();
                UserFile[] result = new UserFile[cache.Count];
                int i = 0;
                foreach (UserFile uf in cache.Values) result[i++] = uf;
                return result;
            }
        }

        public static bool CheckIfUserIsConnected(string username)
        {
            return ServerNetwork.GetConnectedClientFromUsername(username) != null;
        }

        public static bool CheckIfUserExists(ServerClient client, PKT_Login data)
        {
            return GetUserFileFromName(data._username) != null;
        }

        public static bool CheckIfUserAuthCorrect(ServerClient client, PKT_Login data)
        {
            UserFile toFind = GetUserFileFromName(data._username);
            if (toFind != null && toFind.Password == data._password) return true;

            PM_Logins.DenyConnectionWithReason(client, LoginResponse.Invalid);
            return false;
        }

        public static bool CheckIfUserBanned(ServerClient client)
        {
            if (!client.UserFile.IsBanned) return false;

            Printer.Message($"Banned user '{client.UserFile.Username}' tried to join the server");
            PM_Logins.DenyConnectionWithReason(client, LoginResponse.Ban);
            return true;
        }

        public static bool CheckWhitelist(ServerClient client)
        {
            if (!Master.Whitelist.UseWhitelist) return true;

            // Was: .ToArray().FirstOrDefault(...) — double alloc + linear scan. Contains is one pass.
            string name = client?.UserFile?.Username;
            if (!string.IsNullOrEmpty(name)
                && Master.Whitelist.WhitelistedUsers != null
                && Master.Whitelist.WhitelistedUsers.Contains(name))
                return true;

            PM_Logins.DenyConnectionWithReason(client, LoginResponse.Whitelist);
            return false;
        }

        public static int[] GetUserStructuresTilesFromUsername(string username)
        {
            if (string.IsNullOrEmpty(username)) return Array.Empty<int>();
            List<int> tiles = new List<int>();
            foreach (SettlementFile s in PM_Settlements.GetAllSettlements())
            {
                if (s != null && string.Equals(s.Username, username, StringComparison.OrdinalIgnoreCase))
                    tiles.Add(s.Tile);
            }
            foreach (SiteFile site in SiteManagerHelper.GetAllSites())
            {
                if (site != null && string.Equals(site.Username, username, StringComparison.OrdinalIgnoreCase))
                    tiles.Add(site.Tile);
            }
            return tiles.ToArray();
        }
    }
}
