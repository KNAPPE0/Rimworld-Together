using GameServer.Files;
using GameServer.Hooks.ServerBrowser;
using GameServer.Hooks.Shared;
using GameServer.Hooks.TCPNetwork;
using GameServer.Managers;
using GameServer.PacketManager;
using Shared;
using Shared.Files;
using Shared.Files.Actions;
using Shared.Files.Configs;
using Shared.Files.Configs.Mods;
using Shared.Files.Guilds;
using Shared.Misc;
using TCPNetwork;
using TCPNetwork.PacketManagers;
using static Shared.Misc.Printer;

namespace GameServer.Core
{
    public static class Main_
    {
        static void Main()
        {
            ServerPrinter.CreateLogger();
            CultureHandler.SetCulture();

            SetPaths();
            CreateFolders();
            LoadFiles();

            Printer.Title($"Server version {CommonValues.ExecutableVersion}");
            Printer.Title($"Loading all necessary resources");
            Printer.Title(Printer.SeparatorString);

            EventManagerH.LoadAllEvents();
            Printer.Title(Printer.SeparatorString, LogImportanceMode.Extreme);
            CMD_Base.GetAllCommands();
            Printer.Title(Printer.SeparatorString, LogImportanceMode.Extreme);
            PM_Base.CacheAllPackets(PM_Base.AssemblyType.Server);
            Printer.Title(Printer.SeparatorString, LogImportanceMode.Extreme);

            ServerNetwork.StartFeature();
            Task.Run(BackupManager.StartFeature);
            Task.Run(ServerBrowserManager.StartFeature);

            // KMH: Initialize options profile enforcement
            OptionsProfileManager.Initialize();

            // KMH: Start Discord bridge if enabled
            if (Master.ServerConfig != null && Master.ServerConfig.EnableDiscordBridge)
            {
                GameServer.Integrations.Discord.DiscordBridge.TryStart();
            }

            while (true) CMD_Base.ListenForCommands();
        }

        private static void SetPaths()
        {
            ServerConfigFile.SavePath = Path.Combine(Master.ConfigsPath, "ServerConfig.json");
            ActionsConfigFile.SavePath = Path.Combine(Master.ConfigsPath, "ActionConfig.json");
            PlanetConfigFile.SavePath = Path.Combine(Master.AssetsPath, "WorldValuesFile.json");
            StorytellerConfigFile.SavePath = Path.Combine(Master.ConfigsPath, "StorytellerConfig.json");
            ScenarioConfigFile.SavePath = Path.Combine(Master.ConfigsPath, "ScenarioConfig.json");
            ModConfigFile.SavePath = Path.Combine(Master.ConfigsPath, "ModConfig.json");
            DifficultyConfigFile.SavePath = Path.Combine(Master.ConfigsPath, "DifficultyConfig.json");
            WhitelistConfigFile.SavePath = Path.Combine(Master.ConfigsPath, "WhitelistConfig.json");
            BackupsConfigFile.SavePath = Path.Combine(Master.ConfigsPath, "BackupConfig.json");
            ChatConfigFile.SavePath = Path.Combine(Master.ConfigsPath, "ChatConfig.json");
            LeaderboardFile.SavePath = Path.Combine(Master.AssetsPath, "Leaderboard.json");
            CommonValues.ServerUsersPath = Master.UsersPath;
            CommonValues.ServerSitesPath = Master.SitesPath;
            GuildFile.SavePath = Path.Combine(Master.GuildsPath);
        }

        private static void CreateFolders()
        {
            if (!Directory.Exists(Master.AssetsPath)) Directory.CreateDirectory(Master.AssetsPath);
            if (!Directory.Exists(Master.ConfigsPath)) Directory.CreateDirectory(Master.ConfigsPath);
            if (!Directory.Exists(Master.LogsPath)) Directory.CreateDirectory(Master.LogsPath);
            if (!Directory.Exists(Master.SystemLogsPath)) Directory.CreateDirectory(Master.SystemLogsPath);
            if (!Directory.Exists(Master.ChatLogsPath)) Directory.CreateDirectory(Master.ChatLogsPath);
            if (!Directory.Exists(Master.BackupsPath)) Directory.CreateDirectory(Master.BackupsPath);
            if (!Directory.Exists(Master.BackupUsersPath)) Directory.CreateDirectory(Master.BackupUsersPath);
            if (!Directory.Exists(Master.BackupServerPath)) Directory.CreateDirectory(Master.BackupServerPath);
            if (!Directory.Exists(Master.TempPath)) Directory.CreateDirectory(Master.TempPath);
            if (!Directory.Exists(Master.UsersPath)) Directory.CreateDirectory(Master.UsersPath);
            if (!Directory.Exists(Master.SavesPath)) Directory.CreateDirectory(Master.SavesPath);
            if (!Directory.Exists(Master.MapsPath)) Directory.CreateDirectory(Master.MapsPath);
            if (!Directory.Exists(Master.SitesPath)) Directory.CreateDirectory(Master.SitesPath);
            if (!Directory.Exists(Master.GuildsPath)) Directory.CreateDirectory(Master.GuildsPath);
            if (!Directory.Exists(Master.SettlementsPath)) Directory.CreateDirectory(Master.SettlementsPath);
            if (!Directory.Exists(Master.EventsPath)) Directory.CreateDirectory(Master.EventsPath);
            if (!Directory.Exists(Master.CompatibilityPatchesPath)) Directory.CreateDirectory(Master.CompatibilityPatchesPath);
        }

        private static void LoadFiles()
        {
            Master.ServerConfig = (ServerConfigFile)ServerConfigFile.Load<ServerConfigFile>(ServerConfigFile.SavePath);
            Master.ActionConfigs = (ActionsConfigFile)ActionsConfigFile.Load<ActionsConfigFile>(ActionsConfigFile.SavePath);
            Master.Whitelist = (WhitelistConfigFile)WhitelistConfigFile.Load<WhitelistConfigFile>(WhitelistConfigFile.SavePath);
            Master.DifficultyValues = (DifficultyConfigFile)DifficultyConfigFile.Load<DifficultyConfigFile>(DifficultyConfigFile.SavePath);
            Master.ScenarioValues = (ScenarioConfigFile)ScenarioConfigFile.Load<ScenarioConfigFile>(ScenarioConfigFile.SavePath);
            Master.StorytellerValues = (StorytellerConfigFile)StorytellerConfigFile.Load<StorytellerConfigFile>(StorytellerConfigFile.SavePath);
            Master.BackupConfig = (BackupsConfigFile)BackupsConfigFile.Load<BackupsConfigFile>(BackupsConfigFile.SavePath);
            Master.ModConfig = (ModConfigFile)ModConfigFile.Load<ModConfigFile>(ModConfigFile.SavePath);
            Master.ChatConfig = (ChatConfigFile)ChatConfigFile.Load<ChatConfigFile>(ChatConfigFile.SavePath);
            Master.WorldValues = (PlanetConfigFile)PlanetConfigFile.Load<PlanetConfigFile>(PlanetConfigFile.SavePath, false);
            Master.LeaderboardFile = (LeaderboardFile)LeaderboardFile.Load<LeaderboardFile>(LeaderboardFile.SavePath);
        }
    }
}