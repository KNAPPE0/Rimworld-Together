using Shared;
using Shared.Files;
using Shared.Files.Actions;
using Shared.Files.Configs;
using Shared.Files.Configs.Mods;

namespace GameServer.Core
{
    public static class Master
    {
        public static string MainPath { get; set; } = Directory.GetCurrentDirectory();

        public static string AssetsPath { get; set; } = Path.Combine(Master.MainPath, "Assets");

        public static string BackupsPath { get; set; } = Path.Combine(Master.MainPath, "Backups");

        public static string BackupServerPath { get; set; } = Path.Combine(Master.BackupsPath, "Servers");

        public static string BackupUsersPath { get; set; } = Path.Combine(Master.BackupsPath, "Users");

        public static string ConfigsPath { get; set; } = Path.Combine(Master.MainPath, "Configs");

        public static string LogsPath { get; set; } = Path.Combine(Master.MainPath, "Logs");

        public static string SystemLogsPath { get; set; } = Path.Combine(Master.LogsPath, "System");

        public static string ChatLogsPath { get; set; } = Path.Combine(Master.LogsPath, "Chat");

        public static string TempPath { get; set; } = Path.Combine(Master.MainPath, "Temp");

        public static string MapsPath { get; set; } = Path.Combine(Master.AssetsPath, "Maps");

        public static string UsersPath { get; set; } = Path.Combine(Master.AssetsPath, "Users");

        public static string SavesPath { get; set; } = Path.Combine(Master.AssetsPath, "Saves");

        public static string SitesPath { get; set; } = Path.Combine(Master.AssetsPath, "Sites");

        public static string GuildsPath { get; set; } = Path.Combine(Master.AssetsPath, "Guilds");

        public static string SettlementsPath { get; set; } = Path.Combine(Master.AssetsPath, "Settlements");

        public static string EventsPath { get; set; } = Path.Combine(Master.AssetsPath, "Events");

        public static string WorldPath { get; set; } = Path.Combine(Master.AssetsPath, "World");

        public static string CompatibilityPatchesPath { get; set; } = Path.Combine(Master.AssetsPath, "Patches");

        //References

        public static WhitelistConfigFile Whitelist { get; set; } = null;

        public static PlanetConfigFile WorldValues { get; set; } = null;

        public static ServerConfigFile ServerConfig { get; set; } = null;

        public static ActionsConfigFile ActionConfigs { get; set; } = null;

        public static DifficultyConfigFile DifficultyValues { get; set; } = null;

        public static StorytellerConfigFile StorytellerValues { get; set; } = null;

        public static ScenarioConfigFile ScenarioValues { get; set; } = null;

        public static BackupsConfigFile BackupConfig { get; set; } = null;

        public static ModsConfigFile ModConfig { get; set; } = null;

        public static ChatConfigFile ChatConfig { get; set; } = null;

        public static ServerBrowserConfigFile ServerBrowserConfig { get; set; } = null;

        public static LeaderboardFile LeaderboardFile { get; set; } = null;
    }
}
