using GameClient.Core;
using GameClient.Core.Configs;
using GameClient.Dialogs;
using HarmonyLib;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Shared.Misc.Printer;

namespace GameClient.Misc
{
    public static class HarmonyHandler
    {
        private static Harmony MainInstance { get; set; } = null;

        public static Harmony SynchronousInstance { get; private set; } = null;

        private static readonly string HarmonyStartID = $"{Master.ModID}-Start";

        private static readonly string HarmonyMainID = $"{Master.ModID}-Main";

        public static void EnableStartPatches() { new Harmony(HarmonyStartID).PatchCategory("Start"); }

        public static void EnableMainPatches()
        {
            if (MainInstance == null) MainInstance = new Harmony(HarmonyMainID);
            MainInstance.PatchAllUncategorized(Assembly.GetExecutingAssembly());
        }

        [OnSessionEnd]
        private static void DisableMainPatches() 
        {
            PatchClassProcessor[] sequence = (from type in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly())
                where type.HasHarmonyAttribute() select type).Select(CreateClassProcessor).ToArray();

            sequence.DoIf((PatchClassProcessor patchClass) => string.IsNullOrEmpty(patchClass.Category), delegate (PatchClassProcessor patchClass)
            {
                patchClass.Unpatch();
            });
        }

        [OnSynchronousStart]
        private static void EnableSynchronousPatches()
        {
            if (SynchronousInstance == null) SynchronousInstance = new Harmony("RimWorld Together Synchronous");

            Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(fetch => fetch.GetName().Name == "Synchronous");
            SynchronousInstance.PatchCategory(assembly, "Synchronous");

            Printer.Warning("Patched Synchronous methods", LogImportanceMode.Verbose);
        }

        [OnSessionEnd]
        [OnSynchronousEnd]
        private static void DisableSynchronousPatches()
        {
            if (SynchronousInstance != null)
            {
                Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(fetch => fetch.GetName().Name == "Synchronous");
                SynchronousInstance.UnpatchCategory(assembly, "Synchronous");

                Printer.Warning("Unpatched Synchronous methods", LogImportanceMode.Verbose);
            }
        }

        private static PatchClassProcessor CreateClassProcessor(Type type) { return new PatchClassProcessor(MainInstance, type); }

        public static bool CheckForModCollision()
        {
            // Honour both the persistent bypass (mod
            // settings menu) and the session-only one ("Continue anyway"
            // button in DLG_Compatibility). Either flag is enough — the
            // user has explicitly opted out of the safety net.
            if (ModConfigGetter.BypassModCompatibilityCheck) return true;
            if (ModConfigGetter.BypassModCheckThisSession) return true;

            EnableMainPatches();

            List<string> collidingMods = new List<string>();

            foreach (MethodBase method in MainInstance.GetPatchedMethods())
            {
                HarmonyLib.Patches patchInfo = Harmony.GetPatchInfo(method);

                foreach (HarmonyLib.Patch patch in patchInfo.Prefixes)
                {
                    if (patch.owner != HarmonyMainID && patch.owner != HarmonyStartID)
                    {
                        if (!collidingMods.Contains(patch.owner)) collidingMods.Add(patch.owner);
                    }
                }

                foreach (HarmonyLib.Patch patch in patchInfo.Transpilers)
                {
                    if (patch.owner != HarmonyMainID && patch.owner != HarmonyStartID)
                    {
                        if (!collidingMods.Contains(patch.owner)) collidingMods.Add(patch.owner);
                    }
                }
            }

            DisableMainPatches();

            if (collidingMods.Count == 0)
            {
                // Clean scan flips the session bypass — mod set can't change without restart.
                ModConfigGetter.BypassModCheckThisSession = true;
                return true;
            }
            else
            {
                DLG_Base.PushNewDialog(new DLG_Compatibility(collidingMods));
                return false;
            }
        }
    }
}
