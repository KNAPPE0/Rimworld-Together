using HarmonyLib;
using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace GameClient.Patches
{
    /// <summary>
    /// KMH 26.5.22.1: Ported from upstream RWT (Apr 2026 — "Added bypass to
    /// autosave interval").
    ///
    /// <para>Vanilla RimWorld clamps <see cref="Autosaver.AutosaveIntervalDays"/>
    /// to a max of 1.0 days in permadeath mode (or 0.5 / 0.25 depending on
    /// storyteller). Multiplayer needs autosaves more frequently than
    /// vanilla allows for two reasons:</para>
    /// <list type="bullet">
    ///   <item>The server pulls fresh map data + leaderboard stats every
    ///   save — without it, leaderboards stagnate between visible saves.</item>
    ///   <item>Disconnect recovery relies on the latest autosave; the
    ///   default day interval can lose hours of progress on a crash.</item>
    /// </list>
    ///
    /// <para>This patch removes the clamp by always returning the raw
    /// <see cref="Prefs.AutosaveIntervalDays"/> value (whatever the user
    /// configured in their RimWorld settings, including the 0.05-day /
    /// 1-hour minimum that vanilla normally hides behind permadeath).
    /// The companion patches on Dialog_Options + Listing_Standard.Label
    /// replace the "max interval" warning text so the player sees a clear
    /// "overridden by KMH" notice instead of the vanilla note that no
    /// longer applies.</para>
    /// </summary>
    [HarmonyPatch(typeof(Autosaver), "AutosaveIntervalDays", MethodType.Getter)]
    public static class Patch_Autosaver_AutosaveIntervalDays
    {
        [HarmonyPrefix]
        public static bool Prefix(ref float __result)
        {
            // KMH 26.5.22.1: Skip vanilla's clamp logic entirely. Return
            // the user-selected interval verbatim. `false` from a prefix
            // tells Harmony to skip the original method.
            __result = Prefs.AutosaveIntervalDays;
            return false;
        }
    }

    /// <summary>
    /// KMH 26.5.22.1: Flattens the previous nested-class layout. Two
    /// flat top-level patches — `DoGeneralOptions_Patch` arms the
    /// "rewrite next label" flag, and `Listing_Standard_Label_Patch`
    /// consumes it. Nested Harmony classes are auto-discovered by
    /// Harmony 2.x's PatchAll/PatchAllUncategorized, but flat classes
    /// remove any ambiguity about discovery order — and let us share
    /// the flag explicitly via a non-nested static.
    /// </summary>
    internal static class AutosaverPatchShared
    {
        /// <summary>
        /// True for exactly one upcoming
        /// <see cref="Listing_Standard.Label(TaggedString, float, string)"/>
        /// call so the next Label render is rewritten. Reset by
        /// <see cref="DoGeneralOptions_Patch"/>'s prefix on entry into
        /// the options dialog so a stale flag from a previous frame
        /// can't leak. Single-threaded UI access — static is safe.
        /// </summary>
        public static bool SkipNextLabel = false;
    }

    /// <summary>
    /// KMH 26.5.22.1: Resets <see cref="AutosaverPatchShared.SkipNextLabel"/>
    /// on every entry into the vanilla options dialog so we don't carry
    /// a stale flag from a previous frame.
    /// </summary>
    [HarmonyPatch(typeof(Dialog_Options), "DoGeneralOptions")]
    public static class DoGeneralOptions_Patch
    {
        [HarmonyPrefix]
        public static void Prefix() { AutosaverPatchShared.SkipNextLabel = false; }
    }

    /// <summary>
    /// KMH 26.5.22.1: Rewrites the "max permadeath autosave interval"
    /// warning label when the vanilla options dialog renders it. We
    /// match on the TaggedString equality with the vanilla translated
    /// string so this works in every locale — vanilla resolves the same
    /// string we're comparing against.
    /// </summary>
    [HarmonyPatch(typeof(Listing_Standard), nameof(Listing_Standard.Label),
        new Type[] { typeof(TaggedString), typeof(float), typeof(string) })]
    public static class Listing_Standard_Label_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref TaggedString label)
        {
            if (AutosaverPatchShared.SkipNextLabel) return true;

            // KMH 26.5.22.1: TaggedString equality can throw on some
            // installs (missing-key bug in older language packs). Defensive
            // try/catch — failing to rewrite the label is a cosmetic loss,
            // not worth crashing the options dialog over.
            try
            {
                if (label == "MaxPermadeathAutosaveIntervalInfo".Translate(1f))
                {
                    label = "Permadeath autosave interval is currently overridden by KMH".Colorize(Color.green);
                    AutosaverPatchShared.SkipNextLabel = true;
                }
            }
            catch { /* see comment above */ }

            return true;
        }
    }
}
