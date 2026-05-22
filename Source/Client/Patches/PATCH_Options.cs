using HarmonyLib;
using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace GameClient.Patches
{
    // Removes vanilla's permadeath autosave-interval clamp so KMH can
    // autosave more frequently than 1 day. Companion patches below
    // rewrite the vanilla "max interval" warning label to reflect this.

    [HarmonyPatch(typeof(Autosaver), "AutosaveIntervalDays", MethodType.Getter)]
    public static class Patch_Autosaver_AutosaveIntervalDays
    {
        [HarmonyPrefix]
        public static bool Prefix(ref float __result)
        {
            __result = Prefs.AutosaveIntervalDays;
            return false;
        }
    }

    internal static class AutosaverPatchShared
    {
        // Armed for exactly one Listing_Standard.Label call after
        // DoGeneralOptions enters; the Label patch consumes it. Single
        // GUI thread, static is safe.
        public static bool SkipNextLabel = false;
    }

    [HarmonyPatch(typeof(Dialog_Options), "DoGeneralOptions")]
    public static class DoGeneralOptions_Patch
    {
        [HarmonyPrefix]
        public static void Prefix() { AutosaverPatchShared.SkipNextLabel = false; }
    }

    [HarmonyPatch(typeof(Listing_Standard), nameof(Listing_Standard.Label),
        new Type[] { typeof(TaggedString), typeof(float), typeof(string) })]
    public static class Listing_Standard_Label_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref TaggedString label)
        {
            if (AutosaverPatchShared.SkipNextLabel) return true;

            // TaggedString equality can throw on installs with broken
            // language packs — swallow so the options dialog doesn't crash.
            try
            {
                if (label == "MaxPermadeathAutosaveIntervalInfo".Translate(1f))
                {
                    label = "Permadeath autosave interval is currently overridden by KMH".Colorize(Color.green);
                    AutosaverPatchShared.SkipNextLabel = true;
                }
            }
            catch { }

            return true;
        }
    }
}
