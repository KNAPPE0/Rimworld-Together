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

        // Empty = button hidden in DLG_ServerBrowser.
        public string SteamWorkshopURL { get; set; } = string.Empty;

        public string DiscordURL { get; set; } = string.Empty;

        // KMH: Discord integration
        public bool EnableDiscordBridge { get; set; } = false;

        public string DiscordBotToken { get; set; } = string.Empty;

        public string DiscordChatChannelId { get; set; } = string.Empty;

        public string DiscordAdminChannelId { get; set; } = string.Empty;

        public string DiscordCommandPrefix { get; set; } = "!";

        public string DiscordAdminRoleIdsCsv { get; set; } = string.Empty;

        // Short label (1-6 chars) for this server in shared Discord channels. Blank → "S{Port}".
        public string DiscordServerTag { get; set; } = string.Empty;

        // Rebroadcast chat from other servers' bots in the same channel.
        public bool EnableCrossServerChatBridge { get; set; } = false;

        // 0=all, 1=warning+, 2=error only.
        public int DiscordConsoleMinSeverity { get; set; } = 0;

        public bool DiscordUseEmbedsForConsole { get; set; } = true;

        // Base URL for "{base}/{ItemDefName}.png". Empty → category emoji fallback.
        public string MarketplaceIconBaseUrl { get; set; } = string.Empty;

        public int DiscordLeaderboardIntervalMinutes { get; set; } = 5;

        // True = edit-in-place; false = post new embed every tick.
        public bool DiscordLeaderboardEditExisting { get; set; } = true;

        // 0 disables rollover (single forever-edited embed).
        public int DiscordLeaderboardRolloverHours { get; set; } = 24;

        public int DiscordLeaderboardTopCount { get; set; } = 10;

        // Blank → falls back to chat channel, then admin.
        public string DiscordLeaderboardChannelId { get; set; } = string.Empty;

        // Text channel → one edit-in-place embed per user; forum channel → one thread per user.
        // Blank disables `!showcase`.
        public string DiscordMarketplaceForumChannelId { get; set; } = string.Empty;

        // Same rules as DiscordMarketplaceForumChannelId. Blank → falls back to that field.
        public string DiscordWtbForumChannelId { get; set; } = string.Empty;

        // 0=Compact (10/page), 1=Normal (25/page), 2=Verbose (50/page).
        public int DiscordOutputMode { get; set; } = 1;

        // When true, every Discord command must @-mention this server's bot.
        // Needed for multi-bot channels where multiple servers share a guild.
        public bool DiscordRequireBotMention { get; set; } = true;
    }
}
