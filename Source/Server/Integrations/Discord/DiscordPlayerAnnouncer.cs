using Shared.Misc;
using System;
using System.Collections.Generic;
using static Shared.Misc.Printer;

namespace GameServer.Integrations.Discord
{
    public static class DiscordPlayerAnnouncer
    {
        private static readonly object LockObj = new object();
        private static readonly HashSet<string> JoinedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void AnnounceFullyJoined(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            bool shouldSend = false;

            lock (LockObj)
            {
                if (!JoinedUsers.Contains(username))
                {
                    JoinedUsers.Add(username);
                    shouldSend = true;
                }
            }

            if (!shouldSend)
                return;

            // KMH: Show linked Discord name if available
            string discordInfo = GetDiscordInfoForUser(username);
            string displayName = string.IsNullOrEmpty(discordInfo) ? username : $"{username} ({discordInfo})";
            string msg = $"🟢 **{displayName}** has joined the server!";

            DiscordBridge.TryRelayServerConsoleLine($"SERVER: {displayName} joined", LogMode.Title);
            DiscordBridge.TryRelayServerNoticeToDiscordChat(msg);
        }

        public static void AnnounceLeft(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            lock (LockObj)
            {
                JoinedUsers.Remove(username);
            }

            string discordInfo = GetDiscordInfoForUser(username);
            string displayName = string.IsNullOrEmpty(discordInfo) ? username : $"{username} ({discordInfo})";
            string msg = $"⚫ **{displayName}** has left the server!";

            DiscordBridge.TryRelayServerConsoleLine($"SERVER: {displayName} left", LogMode.Warning);
            DiscordBridge.TryRelayServerNoticeToDiscordChat(msg);
        }

        /// <summary>Get Discord username for a linked player, or null.</summary>
        private static string GetDiscordInfoForUser(string username)
        {
            try
            {
                if (string.IsNullOrEmpty(username)) return null;
                var userFile = GameServer.Managers.UserManagerH.GetUserFileFromName(username);
                if (userFile != null && !string.IsNullOrEmpty(userFile.DiscordUsername))
                    return userFile.DiscordUsername;
            }
            catch { }
            return null;
        }

        public static void Clear()
        {
            lock (LockObj)
            {
                JoinedUsers.Clear();
            }
        }
    }
}