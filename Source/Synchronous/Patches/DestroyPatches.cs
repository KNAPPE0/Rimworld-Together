//using GameClient.Misc;
//using HarmonyLib;
//using Synchronous.Core;
//using Synchronous.Managers;
//using Synchronous.Misc;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Verse;

//namespace Synchronous.Patches
//{
//    [HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
//    public static class P_Thing_Destroy
//    {
//        [HarmonyPrefix]
//        public static bool Destroy(Thing __instance)
//        {
//            if (PatchHandler.BypassFlag) return true;
//            else if (!Main_.CheckIfShouldPatch(__instance.Map)) return true;
//            else if (!SessionHandler.IsSynchronousHost) return false;
//            else
//            {
//                SDestroyManager.Ask(__instance);
//                return false;
//            }
//        }
//    }
//}
