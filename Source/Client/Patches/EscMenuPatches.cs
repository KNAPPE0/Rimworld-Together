using GameClient.Dialogs;
using GameClient.Managers;
using GameClient.Misc;
using GameClient.PacketManagers;
using HarmonyLib;
using RimWorld;
using System;
using UnityEngine;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Patches
{
    [HarmonyPatch(typeof(MainMenuDrawer), "DoMainMenuControls")]
    public static class SaveMenuPatch
    {
        [HarmonyPrefix]
        public static bool DoPre()
        {
            if (Current.ProgramState == ProgramState.Playing)
            {
                Vector2 buttonSize = new Vector2(170f, 45f);

                if (Widgets.ButtonText(new Rect(0, (buttonSize.y + 7) * 2, buttonSize.x, buttonSize.y), ""))
                {
                    if (SessionHandler.SynchronousMap != null) return false;

                    Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
                    SessionHandler.IsExiting = true;
                    PM_Saves.ForceSave();
                }

                if (Widgets.ButtonText(new Rect(0, (buttonSize.y + 7) * 3, buttonSize.x, buttonSize.y), ""))
                {
                    if (SessionHandler.SynchronousMap != null) return false;

                    Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
                    SessionHandler.IsExiting = true;
                    PM_Saves.ForceSave();
                }
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(MainMenuDrawer), "DoMainMenuControls")]
    public static class AdminMenuPatch
    {
        [HarmonyPrefix]
        public static bool DoPre()
        {
            if (Current.ProgramState == ProgramState.Playing && SessionHandler.IsAdmin)
            {
                Vector2 buttonSize = new Vector2(170f, 45f);

                if (Widgets.ButtonText(new Rect(0, (buttonSize.y + 7) * 4, buttonSize.x, buttonSize.y), ""))
                {
                    Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
                    AdminMenuManager.ShowAdminMenu();
                }
            }

            return true;
        }

        [HarmonyPostfix]
        public static void DoPost()
        {
            if (Current.ProgramState == ProgramState.Playing && SessionHandler.IsAdmin)
            {
                Vector2 buttonSize = new Vector2(170f, 45f);
                if (Widgets.ButtonText(new Rect(0, (buttonSize.y + 7) * 4, buttonSize.x, buttonSize.y), "Admin menu")) { }
            }

            return;
        }
    }

    [HarmonyPatch(typeof(MainMenuDrawer), "DoMainMenuControls")]
    public static class RestartGamePatch
    {
        [HarmonyPrefix]
        public static bool DoPre()
        {
            if (Current.ProgramState == ProgramState.Playing)
            {
                Vector2 buttonSize = new Vector2(170f, 45f);

                if (Widgets.ButtonText(new Rect(0, (buttonSize.y + 6) * 6, buttonSize.x, buttonSize.y), ""))
                {
                    Find.MainTabsRoot.EscapeCurrentTab(playSound: false);

                    Action r1 = delegate
                    {
                        DLG_Base.PushNewDialog(new DLG_Wait("Waiting for server response"));
                        PM_Saves.RequestResetSave();
                    };

                    DLG_YesNo d1 = new DLG_YesNo("Are you sure you want to delete your save?", r1, null, "DELETE!", "no", Color.red);
                    DLG_Base.PushNewDialog(d1);
                }
            }

            return true;
        }

        [HarmonyPostfix]
        public static void DoPost()
        {
            if (Current.ProgramState == ProgramState.Playing)
            {
                Vector2 buttonSize = new Vector2(170f, 45f);

                GUI.color = Color.red;
                if (Widgets.ButtonText(new Rect(0, (buttonSize.y + 6) * 6, buttonSize.x, buttonSize.y), "Delete Save")) { }
                GUI.color = Color.white;
            }

            return;
        }
    }
}