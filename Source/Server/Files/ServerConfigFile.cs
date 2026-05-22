using Shared.Files;

namespace GameServer.Files
{
    public class ServerConfigFile : BaseFile
    {
        public static string SavePath { get; set; } = string.Empty;

        public string Name { get; set; } = "RimWorld-Together-Server";

        public string Description { get; set; } = "My new Rimworld Together Server!";

        public string IP { get; set; } = "0.0.0.0";

        public int Port { get; set; } = 25555;

        public int MaxPlayers { get; set; } = 100;

        public int Verbosity { get; set; } = 0;

        public bool DisplayChatInConsole { get; set; } = false;

        public bool UseUPnP { get; set; } = false;

        public bool SyncLocalSave { get; set; } = true;

        public bool EnableServerBrowser { get; set; } = true;

        public bool EnableServerTelemetry { get; set; } = true;

        // KMH 26.5.22.1: Ported from upstream RWT (Apr 2026). Published
        // alongside the server's name/description in the browser listing
        // — clients render these as one-click buttons in DLG_ServerBrowser
        // so players can join the server's community without separately
        // tracking down the URLs. Both default to empty (no button shown);
        // operators fill them in ServerConfig.json.
        public string SteamWorkshopURL { get; set; } = string.Empty;

        public string DiscordURL { get; set; } = string.Empty;

        // KMH: Discord integration
        public bool EnableDiscordBridge { get; set; } = false;

        public string DiscordBotToken { get; set; } = string.Empty;

        public string DiscordChatChannelId { get; set; } = string.Empty;

        public string DiscordAdminChannelId { get; set; } = string.Empty;

        public string DiscordCommandPrefix { get; set; } = "!";

        public string DiscordAdminRoleIdsCsv { get; set; } = string.Empty;

        // KMH: Multi-server / multi-bot Discord features.

        /// <summary>
        /// Short tag used to label messages from this server in the shared
        /// Discord channel. Examples: "VAN", "PVP", "S1". Keep it 1–6 chars
        /// of ASCII for readability. Falls back to "S{Port}" if blank.
        /// </summary>
        public string DiscordServerTag { get; set; } = string.Empty;

        /// <summary>
        /// When true, in-game chat from OTHER servers' bots in the same
        /// Discord channel gets re-broadcast to this server's players. Set
        /// independently per server — typically all servers in a cluster
        /// flip this on.
        /// </summary>
        public bool EnableCrossServerChatBridge { get; set; } = false;

        /// <summary>
        /// Minimum console-line severity to relay to the admin channel.
        /// 0 = all (matches mirror-window behaviour), 1 = warning+, 2 = error only.
        /// </summary>
        public int DiscordConsoleMinSeverity { get; set; } = 0;

        /// <summary>
        /// When true, console warnings and errors are sent as embed cards
        /// (cleaner formatting, severity colour). Plain log lines stay plain.
        /// </summary>
        public bool DiscordUseEmbedsForConsole { get; set; } = true;

        /// <summary>
        /// Optional base URL for item icon images shown in Discord marketplace
        /// embeds. The bot fetches "{base}/{ItemDefName}.png". Leave empty to
        /// fall back to category emojis. Example: "https://cdn.example.com/rw/icons".
        /// </summary>
        public string MarketplaceIconBaseUrl { get; set; } = string.Empty;

        // KMH: Discord leaderboard auto-poster.

        /// <summary>
        /// How often to refresh the live leaderboard embed.
        /// KMH 2.7: Default lowered to 5 min — the poster edits-in-place,
        /// so the channel doesn't fill up.
        /// </summary>
        public int DiscordLeaderboardIntervalMinutes { get; set; } = 5;

        /// <summary>
        /// When true, the leaderboard poster reuses the same Discord message
        /// (editing it in place) instead of creating a new embed every tick.
        /// Stops the leaderboard from spamming the channel.
        /// </summary>
        public bool DiscordLeaderboardEditExisting { get; set; } = true;

        /// <summary>
        /// KMH 2.7: After this many hours, the current "live" embed is
        /// finalised (footer flips to "End of Live Updates") and a new live
        /// embed is posted underneath — produces a daily archive trail.
        /// Default 24h. Set 0 to disable rollover (single forever-edited embed).
        /// </summary>
        public int DiscordLeaderboardRolloverHours { get; set; } = 24;

        /// <summary>How many entries (per board) to show. Default 10.</summary>
        public int DiscordLeaderboardTopCount { get; set; } = 10;

        /// <summary>
        /// Discord channel ID where the auto-leaderboard posts.
        /// Leave blank to fall back to the chat channel (then admin).
        /// Example: "123456789012345678".
        /// </summary>
        public string DiscordLeaderboardChannelId { get; set; } = string.Empty;

        /// <summary>
        /// KMH 2.7: Discord channel ID where linked players publish their
        /// personal sell showcases via the `!showcase` command. Two modes:
        /// <list type="bullet">
        /// <item>If this is a regular text channel: the bot posts ONE
        /// edit-in-place embed per user (subsequent `!showcase` calls update
        /// the existing message).</item>
        /// <item>If this is a forum channel: the bot creates ONE thread per
        /// user (named after the player) and edits the first post in that
        /// thread on subsequent calls.</item>
        /// </list>
        /// Leave blank to disable the `!showcase` command entirely.
        /// Example: "123456789012345678".
        /// </summary>
        public string DiscordMarketplaceForumChannelId { get; set; } = string.Empty;

        /// <summary>
        /// KMH 26.5.20: Discord channel ID where linked players publish
        /// their Want-To-Buy lists via the `!wtb` command. Same rules as
        /// <see cref="DiscordMarketplaceForumChannelId"/> — text channel or
        /// forum channel both work; the bot creates one thread per user in
        /// forum mode and one edit-in-place message in text mode.
        ///
        /// Typically a SEPARATE channel from the sells showcase so buyers
        /// browsing for WTBs aren't mixed in with sells. Leave blank to
        /// fall back to <see cref="DiscordMarketplaceForumChannelId"/>.
        /// Example: "123456789012345678".
        /// </summary>
        public string DiscordWtbForumChannelId { get; set; } = string.Empty;

        /// <summary>
        /// Output verbosity for paginated Discord command embeds.
        /// 0 = Compact (10/page), 1 = Normal (25/page), 2 = Verbose (50/page).
        /// </summary>
        public int DiscordOutputMode { get; set; } = 1;

        /// <summary>
        /// When true, every Discord command (admin + marketplace + treasury +
        /// quest + leaderboard) must be addressed to this server's bot via an
        /// @-mention before the command word.
        ///
        /// Example with the flag on:
        ///   @MyServerBot !market 2
        ///
        /// Designed for multi-bot channels where multiple RWT servers share
        /// one Discord guild — only the pinged bot responds. Default true.
        /// </summary>
        public bool DiscordRequireBotMention { get; set; } = true;
    }
}
