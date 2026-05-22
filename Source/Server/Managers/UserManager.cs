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
            UserManagerH.InvalidateUserCache();
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
                    UserManagerH.InvalidateUserCache();
                    Printer.Warning($"User '{userFile.Username}' has been pardoned from the server");
                }
            }
        }
    }

    public static class UserManagerH
    {
        // KMH: In-memory cache of user files keyed by case-insensitive username.
        // The previous implementation read+deserialized every user file on disk
        // for every CheckIfUserExists / CheckIfUserAuthCorrect / GetAllUserFiles
        // call, which fired twice per login attempt.
        private static readonly object UserCacheLock = new object();
        private static Dictionary<string, UserFile> UserCache;

        static UserManagerH()
        {
            // Keep cache fresh whenever any UserFile is saved.
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

        private static Dictionary<string, UserFile> GetUserCache()
        {
            lock (UserCacheLock)
            {
                if (UserCache != null) return UserCache;

                Dictionary<string, UserFile> cache = new Dictionary<string, UserFile>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    string[] userFiles = Directory.GetFiles(Master.UsersPath);
                    foreach (string userPath in userFiles)
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
            // KMH 26.5.20: The previous implementation enumerated cache.Values
            // outside the lock. The OnUserFileSaved hook mutates the cache
            // (cache[username] = saved) under UserCacheLock, but C# Dictionary
            // enumerators throw InvalidOperationException if the dictionary
            // is structurally modified mid-iteration. On a busy server a stat
            // tick during a !showcase sweep could nuke the enumerator.
            //
            // Fix: acquire the same lock that the writer uses while we copy
            // the values into an array. The result array is then a true
            // snapshot — callers can iterate it freely without risk.
            lock (UserCacheLock)
            {
                Dictionary<string, UserFile> cache = GetUserCacheLocked();
                UserFile[] result = new UserFile[cache.Count];
                int i = 0;
                foreach (UserFile uf in cache.Values) result[i++] = uf;
                return result;
            }
        }

        /// <summary>
        /// KMH 26.5.20: Lock-free cache accessor for callers that already
        /// hold <see cref="UserCacheLock"/>. The public <see cref="GetUserCache"/>
        /// path remains identical for callers that don't.
        /// </summary>
        private static Dictionary<string, UserFile> GetUserCacheLocked()
        {
            // Caller must hold UserCacheLock.
            if (UserCache != null) return UserCache;

            Dictionary<string, UserFile> cache = new Dictionary<string, UserFile>(StringComparer.OrdinalIgnoreCase);
            try
            {
                string[] userFiles = Directory.GetFiles(Master.UsersPath);
                foreach (string userPath in userFiles)
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

        public static bool CheckIfUserIsConnected(string username)
        {
            ServerClient toGet = ServerNetwork.GetConnectedClientFromUsername(username);
            return toGet != null;
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
            // KMH 2.7: Single-pass collection rather than double-materialising
            // each list (ToList → FindAll → ToArray was building 3 collections
            // per call to find the same data). Now: stream both sources into
            // one tile list.
            if (string.IsNullOrEmpty(username)) return Array.Empty<int>();
            List<int> tilesToExclude = new List<int>();
            foreach (SettlementFile s in PM_Settlements.GetAllSettlements())
            {
                if (s != null && s.Username == username) tilesToExclude.Add(s.Tile);
            }
            foreach (SiteFile site in SiteManagerHelper.GetAllSites())
            {
                if (site != null && site.Username == username) tilesToExclude.Add(site.Tile);
            }
            return tilesToExclude.ToArray();
        }
    }
}
