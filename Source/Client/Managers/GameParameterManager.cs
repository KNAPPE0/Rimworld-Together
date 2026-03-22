using GameClient.Dialogs;
using GameClient.Misc;
using RimWorld;
using Shared;
using System.Collections.Generic;
using System.Linq;
using Verse;
using static Shared.CommonEnumerators;
using TCPNetwork.Packets;
using Shared.Files.Configs;
using GameClient.Hooks.TCPNetwork;
using TCPNetwork;
using GameClient.PacketManagers;
using Shared.Misc;

namespace GameClient.Managers
{
    public static class GameParameterManager
    {
        public static void SetFirstTimeSetup()
        {
            string title = "Server Enforcements";
            string description = "Choose what features to enforce";
            string[] keys = new string[] { "Scenario", "Storyteller", "Difficulty" };
            string[] values = new string[] { "Free", "Enforced" };

            DLG_Base.PushNewDialog(new DLG_ListingWithTuple(
                title,
                description,
                keys,
                values,
                null,
                SendFirstTimeSetup));
        }

        public static void SetValues(PKT_ServerGlobalData data)
        {
            if (data == null)
                return;

            SessionHandler.CurrentScenario = data._scenarioValues;
            SessionHandler.CurrentStoryteller = data._storytellerValues;
            SessionHandler.CurrentDifficulty = data._difficultyValues;
        }

        public static void ApplyServerGameParameters()
        {
            try
            {
                if (Current.Game == null)
                    return;

                // Safe order:
                // Scenario first, then storyteller, then difficulty
                // so difficulty does not end up stomping storyteller setup weirdly.
                SetScenario(SessionHandler.CurrentScenario);
                SetStoryteller(SessionHandler.CurrentStoryteller);
                SetDifficulty(SessionHandler.CurrentDifficulty);
            }
            catch (System.Exception e)
            {
                Printer.Warning($"[GameParameter] Failed applying server game parameters: {e}");
            }
        }

        public static void SetScenario(ScenarioConfigFile file)
        {
            if (file == null || !file.IsEnforced)
                return;

            try
            {
                Scenario toFind = ScenarioLister.AllScenarios().FirstOrDefault(fetch => fetch.name == file.Name);
                if (toFind != null)
                    Current.Game.Scenario = toFind;
                else
                    Current.Game.Scenario = ScenarioLister.AllScenarios().FirstOrDefault();
            }
            catch (System.Exception e)
            {
                Printer.Warning($"[GameParameter] Failed to set scenario: {e}");
            }
        }

        public static void SetDifficulty(DifficultyConfigFile file, bool bypass = false)
        {
            if (file == null)
                return;

            if (!file.IsEnforced && !bypass)
                return;

            try
            {
                if (Current.Game == null || Current.Game.storyteller == null)
                    return;

                Difficulty difficulty = ScribeManager.SerializeFromString<Difficulty>(
                    file.ScribeData);

                if (difficulty == null)
                    return;

                if (Current.Game.storyteller.difficultyDef == null)
                    Current.Game.storyteller.difficultyDef = DifficultyDefOf.Rough;

                Current.Game.storyteller.difficulty = difficulty;
            }
            catch (System.Exception e)
            {
                Printer.Warning($"[GameParameter] Failed to set difficulty: {e}");
            }
        }

        public static void SetStoryteller(StorytellerConfigFile file, bool bypassCheck = false)
        {
            if (file == null)
                return;

            if (!file.IsEnforced && !bypassCheck)
                return;

            try
            {
                if (Current.Game == null)
                    return;

                StorytellerDef storytellerDef = DefDatabase<StorytellerDef>.AllDefs
                    .FirstOrDefault(fetch => fetch.defName == file.DefName);

                if (storytellerDef == null)
                    return;

                DifficultyDef difficultyDef = Current.Game.storyteller?.difficultyDef ?? DifficultyDefOf.Easy;
                Difficulty difficulty = Current.Game.storyteller?.difficulty ?? new Difficulty(difficultyDef);

                Current.Game.storyteller = new Storyteller(storytellerDef, difficultyDef, difficulty);
            }
            catch (System.Exception e)
            {
                Printer.Warning($"[GameParameter] Failed to set storyteller: {e}");
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
            file.ScribeData = ScribeManager.SerializeToString(
                Current.Game.storyteller.difficulty,
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
            data._configFile = ModManagerH.SortModsIntoCategories(
                DLG_ListingWithTuple.DialogTupleListingResultString,
                DLG_ListingWithTuple.DialogTupleListingResultInt);

            data._configFile.IsEnforced = isEnforced;

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.ModManager, data);
        }

        private static void SendFirstTimeSetup()
        {
            if (DLG_ListingWithTuple.DialogTupleListingResultInt[0] == 1)
                SendCurrentScenario(true);

            if (DLG_ListingWithTuple.DialogTupleListingResultInt[1] == 1)
                SendCurrentStoryteller(true);

            if (DLG_ListingWithTuple.DialogTupleListingResultInt[2] == 1)
                SendCurrentDifficulty(true);

            PM_World.SendWorld();
            PM_Events.SendExistingEventsToServer();

            DLG_Base.PushNewDialog(new DLG_Message(
                "MESSAGE",
                new string[]
                {
                    "Server setup was sent.",
                    "Some configurations may still require reconnecting or restarting the server to fully refresh."
                }));
        }
    }
}