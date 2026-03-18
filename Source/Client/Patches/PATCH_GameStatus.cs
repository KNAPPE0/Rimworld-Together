using GameClient.Managers;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Shared;
using Verse;
using static Shared.CommonEnumerators;
using GameClient.Misc;
using GameClient.Dialogs;
using System;
using GameClient.PacketManagers;

namespace GameClient.Patches
{
    [HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
    public static class InitModePatch
    {
        [HarmonyPostfix]
        public static void ModifyPost(Game __instance)
        {
            PM_Settlements.SendNewPlayerSettlement(__instance.CurrentMap.Tile);
            SessionHandler.IsReadyToPlay = true;

            if (SessionHandler.IsGeneratingFreshWorld) PM_Mods.OpenModManagerMenu(true);
            MainThreadHandler.Instance.DoOnStartMethods();
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
    public static class LoadModePatch
    {
        [HarmonyPostfix]
        public static void GetIDFromExistingGame()
        {
            PlanetManager.BuildPlanet();
            SessionHandler.IsReadyToPlay = true;
            MainThreadHandler.Instance.DoOnStartMethods();
        }
    }
}
