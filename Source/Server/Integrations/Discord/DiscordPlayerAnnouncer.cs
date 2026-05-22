using GameServer.Hooks.TCPNetwork;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork.Files.Client;
using static Shared.Misc.Printer;

namespace GameServer.Integrations.Discord
{
    public static class DiscordPlayerAnnouncer
    {
        // KMH: Cap stale-entry growth: if a client crashes/drops without OnDisconnect
        // firing AnnounceLeft, the username would otherwise live forever.
        private const int PruneIntervalMs = 60_000;

        private static readonly object LockObj = new object();
        private static readonly HashSet<string> JoinedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static long LastPruneUtcTicks;

        public static void AnnounceFullyJoined(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            bool shouldSend = false;

            lock (LockObj)
            {
                PruneStaleEntriesLocked();

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

            bool wasTracked;
            lock (LockObj)
            {
                wasTracked = JoinedUsers.Remove(username);
            }

            // Only relay if this username was actually announced as joined,
            // so duplicate disconnect events do not produce duplicate messages.
            if (!wasTracked) return;

            string discordInfo = GetDiscordInfoForUser(username);
            string displayName = string.IsNullOrEmpty(discordInfo) ? username : $"{username} ({discordInfo})";
            string msg = $"⚫ **{displayName}** has left the server!";

            DiscordBridge.TryRelayServerConsoleLine($"SERVER: {displayName} left", LogMode.Warning);
            DiscordBridge.TryRelayServerNoticeToDiscordChat(msg);
        }

        // Caller must hold LockObj. Drops anyone in JoinedUsers who is not
        // currently in the connected-clients list.
        private static void PruneStaleEntriesLocked()
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            long pruneIntervalTicks = TimeSpan.FromMilliseconds(PruneIntervalMs).Ticks;

            if (LastPruneUtcTicks != 0 && nowTicks - LastPruneUtcTicks < pruneIntervalTicks)
                return;

            LastPruneUtcTicks = nowTicks;

            HashSet<string> connected;
            try
            {
                connected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (ServerClient sc in ServerNetwork.GetConnectedClients())
                {
                    string u = sc?.UserFile?.Username;
                    if (!string.IsNullOrEmpty(u)) connected.Add(u);
                }
            }
            catch { return; }

            List<string> stale = null;
            foreach (string user in JoinedUsers)
            {
                if (!connected.Contains(user))
                {
                    stale ??= new List<string>();
                    stale.Add(user);
                }
            }

            if (stale != null)
            {
                for (int i = 0; i < stale.Count; i++) JoinedUsers.Remove(stale[i]);
            }
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
                LastPruneUtcTicks = 0;
            }
        }
    }
}
