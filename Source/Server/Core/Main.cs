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

            // KMH: Build the linked-accounts cache so login pushes are immediate.
            LinkedAccountsManager.Initialize();

            // KMH 2.7: Player lifetime stats — subscribes to treasury deposits
            // so guild donations get counted automatically.
            PlayerStatsManager.Initialize();

            // KMH 2.7: Item label cache — load disk snapshot so Discord
            // output is human-readable from the moment the bot starts up,
            // before any client has reconnected to refresh it.
            ItemLabelCache.Initialize();

            // KMH: Start Discord bridge if enabled
            if (Master.ServerConfig != null && Master.ServerConfig.EnableDiscordBridge)
            {
                GameServer.Integrations.Discord.DiscordBridge.TryStart();
                Task.Run(GameServer.Integrations.Discord.DiscordLeaderboardPoster.StartFeature);
                // KMH 26.5.20: Auto-prune stale `!showcase` posts.
                GameServer.Integrations.Discord.DiscordShowcaseSweep.StartFeature();
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
            if (!Directory.Exists(Master.TreasuriesPath)) Directory.CreateDirectory(Master.TreasuriesPath);
            if (!Directory.Exists(Master.SettlementsPath)) Directory.CreateDirectory(Master.SettlementsPath);
            if (!Directory.Exists(Master.EventsPath)) Directory.CreateDirectory(Master.EventsPath);
            if (!Directory.Exists(Master.CompatibilityPatchesPath)) Directory.CreateDirectory(Master.CompatibilityPatchesPath);
        }

        private static void LoadFiles()
        {
            Master.ServerConfig = (ServerConfigFile)ServerConfigFile.Load<ServerConfigFile>(ServerConfigFile.SavePath);
            ValidateServerConfigUrls(Master.ServerConfig);
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

        /// <summary>
        /// KMH 26.5.22.1: Validate the public-facing URLs the server
        /// publishes to the browser. Malformed or non-HTTPS URLs get
        /// scrubbed to empty so DLG_ServerListing hides the button
        /// instead of opening something nonsensical. We warn the operator
        /// so they can fix their config; we DON'T crash the server over
        /// a typo.
        /// </summary>
        private static void ValidateServerConfigUrls(ServerConfigFile config)
        {
            if (config == null) return;

            config.DiscordURL = SanitizeUrl(
                config.DiscordURL,
                requireHttps: true,
                allowedHostHints: new[] { "discord.gg", "discord.com" },
                fieldName: "DiscordURL");

            config.SteamWorkshopURL = SanitizeUrl(
                config.SteamWorkshopURL,
                requireHttps: true,
                allowedHostHints: new[] { "steamcommunity.com" },
                fieldName: "SteamWorkshopURL");
        }

        /// <summary>
        /// KMH 26.5.22.1: Best-effort URL validator. Returns the URL if
        /// it parses as an absolute HTTPS URI whose host contains at least
        /// one of the expected hints; otherwise warns and returns empty.
        /// Empty inputs pass through silently (operator just hasn't set
        /// the field — that's the default state).
        /// </summary>
        private static string SanitizeUrl(string url, bool requireHttps, string[] allowedHostHints, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            string trimmed = url.Trim();

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out Uri parsed))
            {
                Printer.Warning($"[ServerConfig] {fieldName} is not a valid URL — ignoring");
                return string.Empty;
            }
            if (requireHttps && parsed.Scheme != Uri.UriSchemeHttps)
            {
                Printer.Warning($"[ServerConfig] {fieldName} must be HTTPS — ignoring");
                return string.Empty;
            }
            if (allowedHostHints != null && allowedHostHints.Length > 0)
            {
                bool ok = false;
                string host = (parsed.Host ?? string.Empty).ToLowerInvariant();
                foreach (string hint in allowedHostHints)
                {
                    if (host.Contains(hint.ToLowerInvariant())) { ok = true; break; }
                }
                if (!ok)
                {
                    Printer.Warning($"[ServerConfig] {fieldName} host '{parsed.Host}' is not one of: {string.Join(", ", allowedHostHints)} — ignoring");
                    return string.Empty;
                }
            }
            return trimmed;
        }
    }
}