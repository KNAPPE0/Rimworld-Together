using GameClient.Misc;
using GameClient.PacketManagers;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace GameClient.Patches
{
    [HarmonyPatch(typeof(MainMenuDrawer), "DoMainMenuControls")]
    public static class Patch_MainMenuDrawer_DoMainMenuControls
    {
        [HarmonyPrefix]
        public static bool DoPre()
        {
            if (Current.ProgramState == ProgramState.Playing)
            {
                Vector2 buttonSize = new Vector2(170f, 45f);

                if (Widgets.ButtonText(new Rect(0, (buttonSize.y + 7) * 2, buttonSize.x, buttonSize.y), ""))
                {
                    Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
                    SessionHandler.IsExiting = true;
                    PM_Saves.ForceSave();
                }

                if (Widgets.ButtonText(new Rect(0, (buttonSize.y + 7) * 3, buttonSize.x, buttonSize.y), ""))
                {
                    Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
                    SessionHandler.IsExiting = true;
                    PM_Saves.ForceSave();
                }
            }

            return true;
        }
    }
}
