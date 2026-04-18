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

        // KMH: Discord integration
        public bool EnableDiscordBridge { get; set; } = false;

        public string DiscordBotToken { get; set; } = string.Empty;

        public string DiscordChatChannelId { get; set; } = string.Empty;

        public string DiscordAdminChannelId { get; set; } = string.Empty;

        public string DiscordCommandPrefix { get; set; } = "!";

        public string DiscordAdminRoleIdsCsv { get; set; } = string.Empty;
    }
}
