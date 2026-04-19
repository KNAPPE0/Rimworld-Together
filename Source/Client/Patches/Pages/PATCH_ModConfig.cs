using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Managers;
using GameClient.Misc;
using GameClient.Hooks.TCPNetwork;
using HarmonyLib;
using RimWorld;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace GameClient.Patches.Pages
{
    public static class LocalConfigLockUtility
    {
        private static float LastPopupTime = -999f;

        public static bool IsConnectedAdminBypass()
        {
            return SessionHandler.CurrentNetworkState != ClientNetwork.ClientNetworkState.Disconnected
                && SessionHandler.IsAdmin;
        }

        public static bool ShouldLockConfigEditing()
        {
            if (!OptionsProfileSessionManager.IsEnforcementActive()) return false;
            if (OptionsProfileSessionManager.IsInternalApplyInProgress) return false;
            if (IsConnectedAdminBypass()) return false;

            return true;
        }

        public static bool ShouldRedirectModsCategory()
        {
            if (!ShouldLockConfigEditing()) return false;
            if (Find.WindowStack == null) return false;
            if (!Find.WindowStack.IsOpen<Dialog_Options>()) return false;

            return true;
        }

        public static void TryShowLockedMessage()
        {
            if (Time.realtimeSinceStartup - LastPopupTime < 0.75f) return;

            if (Find.WindowStack != null && Find.WindowStack.IsOpen<DLG_Message>())
                return;

            LastPopupTime = Time.realtimeSinceStartup;

            DLG_Base.PushNewDialog(new DLG_Message(
                "Config Locked",
                new[]
                {
                    "A server-enforced config profile is active.\n\n" +
                    "Mod options are locked right now.\n\n" +
                    "Connected admins may still edit while in a server.\n\n" +
                    "Use Restore Original Configs from the main menu if you want your personal configs back."
                }));
        }
    }

    [HarmonyPatchCategory("Start")]
    [HarmonyPatch(typeof(Dialog_Options), "DoWindowContents")]
    public static class Patch_Dialog_Options_BlockModsCategory
    {
        private static readonly FieldInfo SelectedCategoryField =
            AccessTools.Field(typeof(Dialog_Options), "selectedCategory");

        private static readonly FieldInfo SelectedModField =
            AccessTools.Field(typeof(Dialog_Options), "selectedMod");

        private static readonly FieldInfo ModFilterField =
            AccessTools.Field(typeof(Dialog_Options), "modFilter");

        private static readonly FieldInfo OptionsScrollPositionField =
            AccessTools.Field(typeof(Dialog_Options), "optionsScrollPosition");

        private static readonly FieldInfo OptionsViewRectHeightField =
            AccessTools.Field(typeof(Dialog_Options), "optionsViewRectHeight");

        [HarmonyPostfix]
        public static void Postfix(Dialog_Options __instance)
        {
            if (!LocalConfigLockUtility.ShouldRedirectModsCategory()) return;
            if (__instance == null) return;
            if (SelectedCategoryField == null) return;

            OptionCategoryDef selectedCategory = SelectedCategoryField.GetValue(__instance) as OptionCategoryDef;
            if (selectedCategory == null) return;

            if (!string.Equals(selectedCategory.defName, "Mods", StringComparison.OrdinalIgnoreCase))
                return;

            OptionCategoryDef generalCategory =
                DefDatabase<OptionCategoryDef>.AllDefs.FirstOrDefault(fetch =>
                    string.Equals(fetch.defName, "General", StringComparison.OrdinalIgnoreCase));

            if (generalCategory != null)
                SelectedCategoryField.SetValue(__instance, generalCategory);

            if (SelectedModField != null)
                SelectedModField.SetValue(__instance, null);

            if (ModFilterField != null)
                ModFilterField.SetValue(__instance, string.Empty);

            if (OptionsScrollPositionField != null)
                OptionsScrollPositionField.SetValue(__instance, Vector2.zero);

            if (OptionsViewRectHeightField != null)
                OptionsViewRectHeightField.SetValue(__instance, 0f);

            LocalConfigLockUtility.TryShowLockedMessage();
        }
    }

    [HarmonyPatchCategory("Start")]
    [HarmonyPatch(typeof(Verse.Mod), nameof(Verse.Mod.DoSettingsWindowContents))]
    public static class Patch_Mod_DoSettingsWindowContents
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            return !LocalConfigLockUtility.ShouldLockConfigEditing();
        }
    }

    [HarmonyPatchCategory("Start")]
    [HarmonyPatch(typeof(Verse.Mod), nameof(Verse.Mod.WriteSettings))]
    public static class Patch_Mod_WriteSettings
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            return !LocalConfigLockUtility.ShouldLockConfigEditing();
        }
    }
}