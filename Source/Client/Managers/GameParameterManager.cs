using GameClient.Dialogs;
using GameClient.Misc;
using RimWorld;
using Shared;
using System.IO;
using System.Linq;
using Verse;
using TCPNetwork.Packets;
using Shared.Files.Configs;
using TCPNetwork;
using GameClient.PacketManagers;
using static TCPNetwork.Packets.GameParameterData;
using static TCPNetwork.Packets.PKT_ModConfig;
using GameClient.Dialogs.Default;
using Shared.Misc;

namespace GameClient.Managers
{
    public static class GameParameterManager
    {
        public static void SetFirstTimeSetup()
        {
            string title = "Server Enforcements";
            string description = "Chose what features to enforce";
            string[] keys = new string[] { "Scenario", "Storyteller", "Difficulty" };
            string[] values = new string[] { "Free", "Enforced" };

            DLG_Base.PushNewDialog(new DLG_ListingWithTuple(title, description, keys, values, null,
                GameParameterManager.SendFirstTimeSetup));
        }

        public static void SetValues()
        {
            SessionHandler.CurrentScenario = SessionHandler.GlobalData._scenarioValues;
            SessionHandler.CurrentStoryteller = SessionHandler.GlobalData._storytellerValues;
            SessionHandler.CurrentDifficulty = SessionHandler.GlobalData._difficultyValues;
        }

        public static void SetScenario(ScenarioConfigFile file)
        {
            if (!file.IsEnforced) return;
            else
            {
                Scenario toFind = ScenarioLister.AllScenarios().FirstOrDefault(fetch => fetch.name == file.Name);
                if (toFind != null) Current.Game.Scenario = toFind;
                else Current.Game.Scenario = ScenarioLister.AllScenarios().ToArray()[0];
            }
        }

        public static void SetDifficulty(DifficultyConfigFile file, bool bypass = false)
        {
            if (!file.IsEnforced && !bypass) return;

            try
            {
                // Deserialize the enforced difficulty settings
                Difficulty enforcedDifficulty = (Difficulty)ScribeManager.SerializeFromString<Difficulty>(file.ScribeData);
                if (enforcedDifficulty != null)
                {
                    // Keep the current difficultyDef if one exists, otherwise default to Rough
                    if (Current.Game.storyteller.difficultyDef == null)
                        Current.Game.storyteller.difficultyDef = DifficultyDefOf.Rough;
                    
                    Current.Game.storyteller.difficulty = enforcedDifficulty;
                }
            }
            catch
            {
                // Fallback: if deserialization fails, just set a safe default
                Current.Game.storyteller.difficultyDef = DifficultyDefOf.Rough;
                Current.Game.storyteller.difficulty = new Difficulty(DifficultyDefOf.Rough);
            }
        }

        public static void SetStoryteller(StorytellerConfigFile file, bool bypassCheck = false)
        {
            if (!file.IsEnforced && !bypassCheck) return;

            try
            {
                StorytellerDef storytellerDef = DefDatabase<StorytellerDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == file.DefName);
                if (storytellerDef == null)
                {
                    Printer.Warning($"[GameParameter] Enforced storyteller '{file.DefName}' not found. Using default.");
                    storytellerDef = StorytellerDefOf.Cassandra;
                }

                // Preserve current difficulty settings when changing storyteller
                DifficultyDef difficultyDef = Current.Game.storyteller?.difficultyDef ?? DifficultyDefOf.Rough;
                Difficulty difficulty = Current.Game.storyteller?.difficulty ?? new Difficulty(difficultyDef);

                Current.Game.storyteller = new Storyteller(storytellerDef, difficultyDef, difficulty);
            }
            catch (System.Exception e)
            {
                Printer.Warning($"[GameParameter] Failed to set storyteller: {e.Message}");
            }
        }

        public static void SendCurrentScenario(bool isEnforced)
        {
            ScenarioConfigFile file = new ScenarioConfigFile();
            file.Name = Current.Game.Scenario.name;
            file.IsEnforced = isEnforced;

            GameParameterData data = new GameParameterData();
            data._stepMode = GenStepMode.Scenario;
            data._bytes = Serializer.ConvertObjectToBytes(file);

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.GameParameterManager, data);
        }

        public static void SendCurrentStoryteller(bool isEnforced)
        {
            StorytellerConfigFile file = new StorytellerConfigFile();
            file.DefName = Current.Game.storyteller.def.defName;
            file.IsEnforced = isEnforced;

            GameParameterData data = new GameParameterData();
            data._stepMode = GenStepMode.Storyteller;
            data._bytes = Serializer.ConvertObjectToBytes(file);

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.GameParameterManager, data);
        }

        public static void SendCurrentDifficulty(bool isEnforced)
        {
            DifficultyConfigFile file = new DifficultyConfigFile();
            file.IsEnforced = isEnforced;
            file.ScribeData = ScribeManager.SerializeToString(Current.Game.storyteller.difficulty, 
                ScribeManager.SerializableType.Other);

            GameParameterData data = new GameParameterData();
            data._stepMode = GenStepMode.Difficulty;
            data._bytes = Serializer.ConvertObjectToBytes(file);

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.GameParameterManager, data);
        }

        public static void SendCurrentModConfigs(bool isEnforced)
        {
            PKT_ModConfig data = new PKT_ModConfig();
            data._stepMode = ModConfigStepMode.Send;
            data._configFile = ModManagerH.SortModsIntoCategories(DLG_ModConfig.ResultMods, DLG_ModConfig.ResultInt);

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.ModManager, data);
        }

        private static void SendFirstTimeSetup()
        {
            if (DLG_ListingWithTuple.DialogTupleListingResultInt[0] == 1) { GameParameterManager.SendCurrentScenario(true); }
            if (DLG_ListingWithTuple.DialogTupleListingResultInt[1] == 1) { GameParameterManager.SendCurrentStoryteller(true); }
            if (DLG_ListingWithTuple.DialogTupleListingResultInt[2] == 1) { GameParameterManager.SendCurrentDifficulty(true); }

            PM_World.SendWorld();
            PM_Events.SendExistingEventsToServer();
        }
    }
}
