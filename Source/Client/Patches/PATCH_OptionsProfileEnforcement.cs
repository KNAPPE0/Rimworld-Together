using GameClient.Patches.Pages;
using HarmonyLib;
using Shared;
using System.Reflection;
using Verse;

namespace GameClient.Patches
{
    [HarmonyPatchCategory("Start")]
    [HarmonyPatch(typeof(LoadedModManager))]
    public static class Patch_LoadedModManager_WriteModSettings_OptionsProfile
    {
        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(LoadedModManager), "WriteModSettings")
                ?? AccessTools.Method(typeof(LoadedModManager), "SaveModSettings")
                ?? null;
        }

        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (!LocalConfigLockUtility.ShouldLockConfigEditing()) return true;
            return false;
        }
    }

    public static class Patch_EnforcedPrefsLock
    {
        [OnUpdate]
        private static void ForceLockedPrefsWhileEnforced()
        {
            if (!LocalConfigLockUtility.ShouldLockConfigEditing()) return;

            if (Prefs.DevMode)
                Prefs.DevMode = false;
        }
    }
}