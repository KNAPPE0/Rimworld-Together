using GameClient.Misc;
using HarmonyLib;
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
        static Main_() 
        {
            ApplyHarmonyPathches();
        }

        private static void ApplyHarmonyPathches()
        {
            Harmony harmony = new Harmony(Master.ModID);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }

        public static bool CheckIfPatchShouldApply()
        {
            return true;
            if (SessionHandler.CurrentNetworkState != ClientNetworkState.Connected) return false;
            else if (Master.SelectedMap == null) return false;
            else if (Master.IsInActivity == false) return false;
            else return true;
        }
    }
}
