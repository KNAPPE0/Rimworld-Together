using GameClient.Defs;
using GameClient.Dialogs;
using GameClient.Patches.Pages;
using GameClient.WorldObjects;
using RimWorld;
using RimWorld.Planet;
using Shared;
using Shared.Files.Actions;
using Shared.Files.Configs;
using Shared.Files.Configs.Mods;
using System.Collections.Generic;
using System.Linq;
using TCPNetwork.Packets;
using Verse;
using static GameClient.Hooks.TCPNetwork.ClientNetwork;
using static Shared.CommonEnumerators;
using static TCPNetwork.Packets.PKT_Activity;

namespace GameClient.Misc
{
    public static class SessionHandler
    {
        public static string Username { get; set; } = string.Empty;

        public static ClientNetworkState CurrentNetworkState { get; set; } = ClientNetworkState.Disconnected;

        public static ActivityType latestActivity { get; set; } = ActivityType.Raid;

        public static WO_Settlement ChosenSettlement { get; set; } = null;

        public static WO_Site ChosenSite { get; set; } = null;

        public static Caravan ChosenCaravan { get; set; } = null;

        public static IEnumerable<IThingHolder> ChosenPods { get; set; } = null;

        public static PKT_Transfer OutgoingManifest { get; set; } = new PKT_Transfer();

        public static PKT_Transfer IncomingManifest { get; set; } = new PKT_Transfer();

        public static ActionsConfigFile CurrentActionValues { get; set; } = null;

        public static List<ModConfig> CurrentMods { get; set; } = null;

        public static ModConfigFile CurrentModConfig { get; set; } = new ModConfigFile();

        public static ScenarioConfigFile CurrentScenario { get; set; } = null;

        public static StorytellerConfigFile CurrentStoryteller { get; set; } = null;

        public static DifficultyConfigFile CurrentDifficulty { get; set; } = null;

        public static PlanetConfigFile CurrentWorld { get; set; } = null;

        public static bool IsAdmin { get; set; } = false;

        public static bool HasFaction { get; set; } = false;

        public static bool IsGeneratingFreshWorld { get; set; } = false;

        public static bool IsReadyToPlay { get; set; } = false;

        public static bool IsSavingGame { get; set; } = false;

        public static bool IsInTransfer { get; set; } = false;

        public static bool IsUsingScriber { get; set; } = false;

        public static List<Faction> PlayerFactions { get; set; } = new List<Faction>();

        public static List<FactionDef> PlayerFactionDefs { get; set; } = new List<FactionDef>();

        public static Faction EnemyFaction { get; set; } = null;

        public static Faction AllyFaction { get; set; } = null;

        public static Faction NeutralFaction { get; set; } = null;

        public static Faction GuildFaction { get; set; } = null;

        public static TradeMode LastTradeStep { get; set; } = CommonEnumerators.TradeMode.None;

        public static bool IsExiting { get; set; } = false;

        public static bool IsSynchronousHost { get; set; } = false;

        public static Map SynchronousMap { get; set; } = null;

        public static PKT_ServerGlobalData GlobalData { get; set; } = null;

        public static int CurrentServerPlayers { get; set; } = int.MinValue;

        public static void SetValues()
        {
            IsAdmin = GlobalData._isClientAdmin;
            HasFaction = GlobalData._isClientFactionMember;
            CurrentActionValues = GlobalData._actionValues;

            // KMH 26.5.22.1: Refresh Discord Rich Presence now that we
            // have a username + server endpoint. Safe to call any time —
            // no-ops if Discord RP isn't available.
            try { DiscordHandler.RefreshFromSession(); }
            catch { }
        }

        [OnUpdate]
        private static void ForcePermadeath()
        {
            try { Current.Game.Info.permadeathMode = true; }
            catch { }
        }

        [OnUpdate]
        private static void ManageDevOptions()
        {
            try
            {
                // Only force-disable DevMode for non-admins while actually connected.
                // Disconnected enforcement is handled by Patch_EnforcedPrefsLock.
                if (CurrentNetworkState != ClientNetworkState.Disconnected && !IsAdmin)
                    Prefs.DevMode = false;
            }
            catch { }
        }

        [OnUpdate]
        private static void ForceBackgroundMode()
        {
            try { Prefs.RunInBackground = true; }
            catch { }
        }

        /// <summary>
        /// KMH 26.5.22.1: Ported from upstream RWT (May 2026 — "Patched
        /// learning helper"). Vanilla RimWorld's Adaptive Training (the
        /// little "did you know..." tutorial popups) pause the game,
        /// interrupt UI, and confuse new multiplayer players. In a
        /// connected session that's a hard nope — your colony might be
        /// under raid while you're staring at a tutorial about hauling.
        ///
        /// <para>Forcing it off every tick is cheap (one bool set) and
        /// resilient — even if the user re-enables it via vanilla
        /// options, the next OnUpdate flips it back. We don't gate this
        /// on connection state because the same "no popups please"
        /// expectation applies the moment KMH is active.</para>
        /// </summary>
        [OnUpdate]
        private static void ForceDisableLearningHelper()
        {
            try { Prefs.AdaptiveTrainingEnabled = false; }
            catch { }
        }

        [OnSessionStart]
        private static void SetOverrideGenerators()
        {
            MapGeneratorDef emptyGenerator = DefDatabase<MapGeneratorDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == "Empty");

            WorldObjectDef settlement = RTWorldObjectDefOf.RTSettlement;
            settlement.mapGenerator = emptyGenerator;

            WorldObjectDef site = RTWorldObjectDefOf.RTSite;
            site.mapGenerator = emptyGenerator;
        }

        [OnSessionEnd]
        private static void CleanValues()
        {
            ChosenSettlement = null;
            ChosenCaravan = null;
            ChosenSite = null;

            OutgoingManifest = new PKT_Transfer();
            IncomingManifest = new PKT_Transfer();
            LastTradeStep = TradeMode.None;

            IsGeneratingFreshWorld = false;
            IsReadyToPlay = false;
            IsInTransfer = false;
            IsSavingGame = false;
            IsUsingScriber = false;
            IsExiting = false;
            IsSynchronousHost = false;
            SynchronousMap = null;

            IsAdmin = false;
            HasFaction = false;
            GlobalData = null;
            CurrentActionValues = null;
            CurrentMods = null;
            CurrentModConfig = new ModConfigFile();
            CurrentScenario = null;
            CurrentStoryteller = null;
            CurrentDifficulty = null;
            CurrentWorld = null;
            CurrentServerPlayers = int.MinValue;

            DLG_Chat.IsDialogOpen = false;
            DLG_Admin.IsDialogOpen = false;
            Patch_Page_SelectScenario_DoWindowContents.executedMessage = false;
            Patch_Page_SelectStoryteller_DoWindowContents.executedMessage = false;

            CurrentNetworkState = ClientNetworkState.Disconnected;

            try { GameClient.Managers.OptionsProfileSessionManager.TryRestoreOnDisconnect(); }
            catch { }

            // KMH 26.5.22.1: Disconnect resets the Rich Presence so the
            // player's Discord profile shows "On main menu" again.
            try { DiscordHandler.RefreshFromSession(); }
            catch { }
        }
    }
}