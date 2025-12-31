using GameClient.Misc;
using HarmonyLib;
using RimWorld;
using Synchronous.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Shared.CommonEnumerators;

namespace Synchronous.Patches
{
    [HarmonyPatch(typeof(GlobalControls), nameof(GlobalControls.GlobalControlsOnGUI))]
    public static class Patch_GlobalControls_GlobalControlsOnGUI
    {
        [HarmonyPrefix]
        public static bool GlobalControlsOnGUI()
        {
            if (!Main_.CheckIfPatchShouldApply()) return true;
            else
            {
                DiagnosticsHandler.DrawDiagnosticsUI();
                return true;
            }
        }
    }
}
