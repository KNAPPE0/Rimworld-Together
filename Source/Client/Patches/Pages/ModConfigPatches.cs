using GameClient.Dialogs;
using GameClient.Misc;
using HarmonyLib;
using RimWorld;
using System.Reflection;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Patches.Pages
{
    [HarmonyPatch(typeof(Dialog_Options))]
    public static class Patch_DialogOptions_DoModOptions
    {
        public static bool executedMessage;

        static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Dialog_Options), "DoModOptions")
                ?? AccessTools.Method(typeof(Dialog_Options), "DoModSettings")
                ?? AccessTools.Method(typeof(Dialog_Options), "DoMods")
                ?? null;
        }

        [HarmonyPrefix]
        public static bool Prefix(Dialog_Options __instance)
        {
            if (!SessionHandler.CurrentModConfig.IsEnforced) return true;
            if (SessionHandler.IsAdmin) return true;

            try
            {
                Find.WindowStack.TryRemove(__instance, doCloseSound: false);
            }
            catch
            {
                __instance.Close();
            }

            if (!executedMessage)
            {
                executedMessage = true;
                DLG_Base.PushNewDialog(new DLG_Message(
                    "Error",
                    new string[] { "Mod options can't be changed in this server!" },
                    delegate { executedMessage = false; }
                ));
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(Page_ModsConfig), "PreOpen")]
    public static class Patch_Page_ModsConfig_PreOpen
    {
        public static bool executedMessage;

        [HarmonyPrefix]
        public static void Prefix(Page_ModsConfig __instance)
        {
            if (!SessionHandler.CurrentModConfig.IsEnforced) return;
            if (SessionHandler.IsAdmin) return;

            try
            {
                Find.WindowStack.TryRemove(__instance, doCloseSound: false);
            }
            catch
            {
                __instance.Close();
            }

            if (!executedMessage)
            {
                executedMessage = true;
                DLG_Base.PushNewDialog(new DLG_Message(
                    "Error",
                    new string[] { "Mods can't be changed in this server!" },
                    delegate { executedMessage = false; }
                ));
            }
        }
    }
}