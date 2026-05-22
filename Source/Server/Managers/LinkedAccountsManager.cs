using GameServer.Hooks.TCPNetwork;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.Misc.Printer;

namespace GameServer.Managers
{
    /// <summary>
    /// KMH: Server-wide cache of (in-game username → Discord display name) for
    /// every linked player. The map is pushed to clients on login and
    /// re-broadcast whenever a UserFile saves with a Discord-related change.
    ///
    /// Client dialogs use this so a linked username renders as
    /// "Knappe <color=#7289DA>(@knappe)</color>" everywhere — treasury logs,
    /// marketplace listings, quest postings, guild members, etc.
    /// </summary>
    public static class LinkedAccountsManager
    {
        private static readonly object Lock = new object();
        private static Dictionary<string, string> _byUsername =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        static LinkedAccountsManager()
        {
            // Auto-update + broadcast whenever any UserFile saves.
            UserFile.OnUserFileSaved += OnUserFileSaved;
        }

        /// <summary>Read-only snapshot of the current map. Caller may copy into a packet.</summary>
        public static Dictionary<string, string> Snapshot()
        {
            lock (Lock)
            {
                return new Dictionary<string, string>(_byUsername, StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// Rebuilds the cache from disk. Cheap because UserManagerH already
        /// caches user files; this just walks them and extracts the linked
        /// Discord names.
        /// </summary>
        public static void RebuildCache()
        {
            lock (Lock)
            {
                _byUsername.Clear();
                try
                {
                    foreach (UserFile uf in UserManagerH.GetAllUserFiles())
                    {
                        if (uf == null || string.IsNullOrEmpty(uf.Username)) continue;
                        if (!string.IsNullOrEmpty(uf.DiscordUsername))
                            _byUsername[uf.Username] = uf.DiscordUsername;
                    }
                }
                catch (Exception e) { Printer.Warning($"[LinkedAccounts] Rebuild failed: {e}"); }
            }
        }

        public static void Initialize()
        {
            RebuildCache();
            Printer.Warning($"[LinkedAccounts] Loaded {_byUsername.Count} linked Discord accounts.", LogImportanceMode.Verbose);
        }

        // -- packet send --

        public static void SendSnapshot(ServerClient client)
        {
            if (client?.Listener == null) return;
            try
            {
                PKT_LinkedAccounts pkt = new PKT_LinkedAccounts
                {
                    LinkedDiscordNamesByUsername = Snapshot()
                };
                client.Listener.EnqueuePacket(PacketHeader.LinkedAccountsManager, pkt);
            }
            catch (Exception e) { Printer.Warning($"[LinkedAccounts] Send snapshot failed: {e}"); }
        }

        public static void BroadcastSnapshot()
        {
            try
            {
                PKT_LinkedAccounts pkt = new PKT_LinkedAccounts
                {
                    LinkedDiscordNamesByUsername = Snapshot()
                };
                foreach (ServerClient sc in ServerNetwork.GetConnectedClients())
                {
                    if (sc?.Listener == null) continue;
                    sc.Listener.EnqueuePacket(PacketHeader.LinkedAccountsManager, pkt);
                }
            }
            catch (Exception e) { Printer.Warning($"[LinkedAccounts] Broadcast failed: {e}"); }
        }

        // -- event handler: track changes from UserFile saves --

        private static void OnUserFileSaved(UserFile uf)
        {
            if (uf == null || string.IsNullOrEmpty(uf.Username)) return;

            bool changed;
            lock (Lock)
            {
                _byUsername.TryGetValue(uf.Username, out string oldName);
                string newName = uf.DiscordUsername ?? string.Empty;

                if (string.IsNullOrEmpty(newName))
                {
                    changed = !string.IsNullOrEmpty(oldName);
                    if (changed) _byUsername.Remove(uf.Username);
                }
                else
                {
                    changed = !string.Equals(oldName, newName, StringComparison.Ordinal);
                    if (changed) _byUsername[uf.Username] = newName;
                }
            }

            if (changed)
                BroadcastSnapshot();
        }
    }
}
