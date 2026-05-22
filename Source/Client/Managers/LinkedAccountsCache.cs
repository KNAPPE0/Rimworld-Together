using System;
using System.Collections.Generic;

namespace GameClient.Managers
{
    /// <summary>
    /// KMH: Client-side mirror of the server's linked-accounts map.
    ///
    /// Every dialog that displays a username should call
    /// <see cref="Format(string)"/> instead of rendering the raw name —
    /// linked players appear as "Knappe <color=#7289DA>(@knappe)</color>"
    /// using Discord blurple, matching the in-game [Discord] chat color.
    /// </summary>
    public static class LinkedAccountsCache
    {
        // Discord brand "blurple" — also matches PKT_Chat.ChatColor.Discord (#7289DA).
        public const string DiscordColorHex = "#7289DA";

        private static readonly object Lock = new object();
        private static Dictionary<string, string> _byUsername =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Fired after a snapshot lands so dialogs can refresh.</summary>
        public static Action OnSnapshotUpdated;

        public static void Apply(TCPNetwork.Packets.PKT_LinkedAccounts packet)
        {
            if (packet?.LinkedDiscordNamesByUsername == null) return;

            lock (Lock)
            {
                _byUsername = new Dictionary<string, string>(
                    packet.LinkedDiscordNamesByUsername,
                    StringComparer.OrdinalIgnoreCase);
            }

            try { OnSnapshotUpdated?.Invoke(); }
            catch { }
        }

        /// <summary>Returns the linked Discord display name, or null if unlinked.</summary>
        public static string GetDiscordName(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;
            lock (Lock)
            {
                return _byUsername.TryGetValue(username, out string name) ? name : null;
            }
        }

        public static bool IsLinked(string username) => !string.IsNullOrEmpty(GetDiscordName(username));

        /// <summary>
        /// Returns the username with a coloured Discord-handle suffix when
        /// linked; otherwise returns the username unchanged.
        ///
        /// Output format:  Knappe <color=#7289DA>(@knappe)</color>
        /// </summary>
        public static string Format(string username)
        {
            if (string.IsNullOrEmpty(username)) return username;
            string discord = GetDiscordName(username);
            if (string.IsNullOrEmpty(discord)) return username;
            return $"{username} <color={DiscordColorHex}>(@{discord})</color>";
        }

        /// <summary>
        /// Compact variant for tight rows (treasury logs, etc.). Renders
        /// just the Discord handle in blurple if linked, otherwise the username.
        /// </summary>
        public static string FormatCompact(string username)
        {
            if (string.IsNullOrEmpty(username)) return username;
            string discord = GetDiscordName(username);
            if (string.IsNullOrEmpty(discord)) return username;
            return $"<color={DiscordColorHex}>@{discord}</color>";
        }
    }
}
