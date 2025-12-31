using HarmonyLib;
using Synchronous.Managers;
using Synchronous.Misc;
using Synchronous.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace Synchronous.Patches
{
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class P_MentalStateHandler_TryStartMentalState
    {
        [HarmonyPrefix]
        public static bool TryStartMentalState(MentalStateDef stateDef, Pawn ___pawn)
        {
            if (PatchHandler.BypassFlag) return true;
            else
            {
                SMentalStateManager.Ask(___pawn, stateDef, PlayerMentalState.MentalMode.Add);
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(MentalState), nameof(MentalState.RecoverFromState))]
    public static class P_MentalState_RecoverFromState
    {
        [HarmonyPrefix]
        public static bool RecoverFromState(MentalState __instance)
        {
            if (PatchHandler.BypassFlag) return true;
            else if (SMentalStateManager.LatestMentalState == __instance) return false;
            else
            {
                SMentalStateManager.Ask(__instance.pawn, __instance.def, PlayerMentalState.MentalMode.Remove);
                SMentalStateManager.LatestMentalState = __instance;
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.MentalStateHandlerTickInterval))]
    public static class P_MentalStateHandler_MentalStateHandlerTickInterval
    {
        [HarmonyPrefix]
        public static bool MentalStateHandlerTickInterval(MentalStateHandler __instance)
        {
            if (__instance.CurState == SMentalStateManager.LatestMentalState) return false;
            else return true;
        }
    }
}
