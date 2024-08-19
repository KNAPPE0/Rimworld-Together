using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class WhitelistManager
    {
        public static void AddUserToWhitelist(string Username)
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                Logger.Error("Attempted to add an empty or null Username to the whitelist.");
                return;
            }

            if (Master.whitelist.WhitelistedUsers.Contains(Username))
            {
                Logger.Warning($"User '{Username}' is already whitelisted.");
                return;
            }

            Master.whitelist.WhitelistedUsers.Add(Username);
            Master.SaveValueFile(ServerFileMode.Whitelist);

            Logger.Warning($"User '{Username}' has been whitelisted.");
        }

        public static void RemoveUserFromWhitelist(string Username)
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                Logger.Error("Attempted to remove an empty or null Username from the whitelist.");
                return;
            }

            if (!Master.whitelist.WhitelistedUsers.Contains(Username))
            {
                Logger.Warning($"User '{Username}' is not in the whitelist.");
                return;
            }

            Master.whitelist.WhitelistedUsers.Remove(Username);
            Master.SaveValueFile(ServerFileMode.Whitelist);

            Logger.Warning($"User '{Username}' has been removed from the whitelist.");
        }

        public static void ToggleWhitelist()
        {
            Master.whitelist.UseWhitelist = !Master.whitelist.UseWhitelist;
            Master.SaveValueFile(ServerFileMode.Whitelist);

            string status = Master.whitelist.UseWhitelist ? "ON" : "OFF";
            Logger.Warning($"Whitelist is now {status}.");
        }
    }
}