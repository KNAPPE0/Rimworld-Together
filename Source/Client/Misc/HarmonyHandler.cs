using GameClient.Core;
using GameClient.Dialogs;
using HarmonyLib;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace GameClient.Misc
{
    public static class HarmonyHandler
    {
        private static Harmony Instance { get; set; } = null;

        private static readonly string HarmonyStartID = $"{Master.ModID}-Start";

        private static readonly string HarmonyMainID = $"{Master.ModID}-Main";

        public static void EnableStartPatches() { new Harmony(HarmonyStartID).PatchCategory("Start"); }

        public static void EnableMainPatches()
        {
            if (Instance == null) Instance = new Harmony(HarmonyMainID);
            Instance.PatchAllUncategorized(Assembly.GetExecutingAssembly());
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

        private static PatchClassProcessor CreateClassProcessor(Type type) { return new PatchClassProcessor(Instance, type); }

        public static bool CheckForModCollision()
        {
            EnableMainPatches();

            List<string> collidingMods = new List<string>();

            foreach (MethodBase method in Instance.GetPatchedMethods())
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

            if (collidingMods.Count == 0) return true;
            else
            {
                string title = "Problematic mods found";
                string description = "The following mods might cause issues during gameplay";
                DLG_Base.PushNewDialog(new DLG_Listing(title, description, collidingMods.ToArray()));
                return false;
            }
        }
    }
}
