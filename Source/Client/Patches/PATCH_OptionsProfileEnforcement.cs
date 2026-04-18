using GameClient.Managers;
using GameClient.Misc;
using HarmonyLib;
using GameClient.Hooks.TCPNetwork;
using System.Reflection;
using Verse;

namespace GameClient.Patches
{
    [HarmonyPatch(typeof(LoadedModManager))]
    public static class Patch_LoadedModManager_WriteModSettings_OptionsProfile
    {
        private static bool IsRunning;

        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(LoadedModManager), "WriteModSettings")
                ?? AccessTools.Method(typeof(LoadedModManager), "SaveModSettings")
                ?? null;
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            if (IsRunning) return;
            if (SessionHandler.CurrentNetworkState == GameClient.Hooks.TCPNetwork.ClientNetwork.ClientNetworkState.Disconnected) return;
            if (SessionHandler.IsAdmin) return;

            try
            {
                IsRunning = true;
                OptionsProfileSessionManager.ReapplyProfileToDiskIfActive(softReload: false);
            }
            finally
            {
                IsRunning = false;
            }
        }
    }
}