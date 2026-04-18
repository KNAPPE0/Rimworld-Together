using GameClient.Dialogs;
using GameClient.Dialogs.Default;
using GameClient.Managers;
using GameClient.Misc;
using GameClient.Hooks.TCPNetwork;
using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace GameClient.Patches.Pages
{
    /// <summary>
    /// KMH: Intercepts Mod.DoSettingsWindowContents to block when enforcement active.
    /// This fires when user clicks any mod in the mod options list.
    /// </summary>
    [HarmonyPatch(typeof(Verse.Mod), nameof(Verse.Mod.DoSettingsWindowContents))]
    public static class Patch_Mod_DoSettingsWindowContents
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (!EnforcementGuard.IsActive) return true;
            if (SessionHandler.CurrentNetworkState != ClientNetwork.ClientNetworkState.Disconnected
                && SessionHandler.IsAdmin) return true;
            // Block rendering mod settings - the settings area will just be empty
            return false;
        }
    }

    /// <summary>
    /// KMH: Intercepts Mod.WriteSettings to prevent saving changes when enforcement active.
    /// </summary>
    [HarmonyPatch(typeof(Verse.Mod), nameof(Verse.Mod.WriteSettings))]
    public static class Patch_Mod_WriteSettings
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (!EnforcementGuard.IsActive) return true;
            if (SessionHandler.CurrentNetworkState != ClientNetwork.ClientNetworkState.Disconnected
                && SessionHandler.IsAdmin) return true;
            return false;
        }
    }

    /// <summary>KMH: Blocks mod list changes when enforcement is active.</summary>
    [HarmonyPatch(typeof(Page_ModsConfig), "PreOpen")]
    public static class Patch_Page_ModsConfig_PreOpen
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (!EnforcementGuard.IsActive) return true;
            if (SessionHandler.CurrentNetworkState != ClientNetwork.ClientNetworkState.Disconnected
                && SessionHandler.IsAdmin) return true;

            DLG_Base.PushNewDialog(new DLG_Message("Mod List Locked",
                new[] { "Mod list is locked by server config enforcement.\n\nRestore your original configs from the main menu." }));
            return false;
        }
    }

    /// <summary>KMH: Adds restore banner to RimWorld Settings dialog.</summary>
    [HarmonyPatch(typeof(Dialog_Options), "DoWindowContents")]
    public static class Patch_Dialog_Options_InjectRestore
    {
        [HarmonyPostfix]
        public static void Postfix(Rect inRect)
        {
            if (!EnforcementGuard.IsActive) return;
            if (!EnforcementGuard.HasBackup) return;

            float btnW = 220f;
            float btnH = 30f;
            Rect panel = new Rect(8f, inRect.yMax - btnH - 24f, btnW + 10f, btnH + 20f);
            Widgets.DrawBoxSolid(panel, new Color(0.15f, 0.12f, 0.08f, 0.9f));
            Widgets.DrawBox(panel);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 0.8f, 0.4f);
            Widgets.Label(new Rect(14f, inRect.yMax - btnH - 20f, btnW, 16f), "Server enforcement active");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            GUI.color = new Color(1f, 0.65f, 0.2f);
            if (Widgets.ButtonText(new Rect(14f, inRect.yMax - btnH - 2f, btnW, btnH), "Restore Original Configs"))
            {
                GUI.color = Color.white;
                OptionsProfileSessionManager.RestorePersonalConfigsManual();
            }
            GUI.color = Color.white;
        }
    }
}
