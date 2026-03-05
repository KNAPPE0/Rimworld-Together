using GameServer.Core;
using GameServer.Misc;
using Shared.Files.Configs;
using Shared.Misc;

namespace GameServer.Managers
{
    public static class WhitelistManager
    {
        public static void AddUserToWhitelist(string username)
        {
            Master.Whitelist.WhitelistedUsers.Add(username);

            WhitelistConfigFile.Save(WhitelistConfigFile.SavePath, Master.Whitelist);

            Printer.Warning($"User '{ConsoleManager.commandParameters[0]}' has been whitelisted");
        }

        public static void RemoveUserFromWhitelist(string username)
        {
            Master.Whitelist.WhitelistedUsers.Remove(username);

            WhitelistConfigFile.Save(WhitelistConfigFile.SavePath, Master.Whitelist);

            Printer.Warning($"User '{ConsoleManager.commandParameters[0]}' is no longer whitelisted");
        }
    }
}
