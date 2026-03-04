using GameClient.Core.Configs;
using GameClient.Misc;
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameClient.Patches
{
    [HarmonyPatch(typeof(GlobalControls), nameof(GlobalControls.GlobalControlsOnGUI))]
    public static class Patch_GlobalControls_GlobalControlsOnGUI
    {
        [HarmonyPrefix]
        public static bool DoPre()
        {
            if (ModConfigGetter.ShowDiagnosticsBool) DiagnosticsHandler.DrawDiagnosticsUI();

            return true;
        }
    }
}
