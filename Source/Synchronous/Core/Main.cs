using GameClient.Misc;
using HarmonyLib;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static Shared.CommonEnumerators;

namespace Synchronous.Core
{
    [StaticConstructorOnStartup]
    public static class Main_
    {
        public static Harmony Instance { get; private set; } = null;

        private static void Start() { ToggleHarmonyPatches(true); }

        [OnSessionEnd]
        private static void Stop() { ToggleHarmonyPatches(false); }

        private static void ToggleHarmonyPatches(bool mode)
        {
            if (mode)
            {
                if (Instance == null) Instance = new Harmony(Master.ModID);
                Instance.PatchCategory("Synchronous");
                Printer.Warning("Patched Synchronous methods", LogImportanceMode.Verbose);
            }

            else
            {
                Instance.UnpatchCategory("Synchronous");
                Printer.Warning("Unpatched Synchronous methods", LogImportanceMode.Verbose);
            }
        }
    }
}
