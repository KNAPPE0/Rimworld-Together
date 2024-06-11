﻿using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace GameClient
{
    [HarmonyPatch(typeof(GameDataSaveLoader), "SaveGame", typeof(string))]
    public static class SaveOnlineGame
    {
        [HarmonyPrefix]
        public static bool DoPre(ref string fileName, ref int ___lastSaveTick)
        {
            try
            {
                if (Network.state == NetworkState.Disconnected) return true;
                if (ClientValues.isSavingGame || ClientValues.isSendingSaveToServer) return false;

                ClientValues.ToggleSavingGame(true);
                ClientValues.ForcePermadeath();
                ClientValues.ManageDevOptions();
                CustomDifficultyManager.EnforceCustomDifficulty();

                string filePath = GenFilePaths.FilePathForSavedGame(fileName);

                Logger.Message($"Creating local save at {filePath}");
                try
                {
                    SafeSaver.Save(filePath, "savegame", delegate
                    {
                        ScribeMetaHeaderUtility.WriteMetaHeader();
                        Game target = Current.Game;
                        Scribe_Deep.Look(ref target, "game");
                    }, Find.GameInfo.permadeathMode);
                    ___lastSaveTick = Find.TickManager.TicksGame;
                }
                catch (Exception e) { Logger.Error("Exception while saving game: " + e); }

                if (Network.state.Equals(NetworkState.Connected))
                {
                    Logger.Message("Sending maps to server");
                    MapManager.SendPlayerMapsToServer();

                    Logger.Message("Sending save to server");
                    SaveManager.SendSavePartToServer();
                }
            }
            catch (Exception e) { Logger.Error($"{e}"); }

            ClientValues.ToggleSavingGame(false);

            return false;
        }
    }

    [HarmonyPatch(typeof(Autosaver), "DoAutosave")]
    public static class Autosave
    {
        [HarmonyPrefix]
        public static bool DoPre()
        {
            if (Network.state == NetworkState.Disconnected) return true;

            return false;
        }
    }

    [HarmonyPatch(typeof(Autosaver), "AutosaverTick")]
    public static class AutosaveTick
    {
        [HarmonyPrefix]
        public static bool DoPre()
        {
            if (Network.state == NetworkState.Disconnected) return true;

            ClientValues.autosaveCurrentTicks++;

            if (ClientValues.autosaveCurrentTicks >= ClientValues.autosaveInternalTicks && !GameDataSaveLoader.SavingIsTemporarilyDisabled)
            {
                SaveManager.ForceSave();
            }

            return false;
            
        }
    }
}
