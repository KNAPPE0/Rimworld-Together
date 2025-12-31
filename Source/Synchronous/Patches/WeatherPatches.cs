using GameClient.Misc;
using HarmonyLib;
using RimWorld;
using Synchronous.Managers;
using Synchronous.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Synchronous.Patches
{
    [HarmonyPatch(typeof(WeatherManager), nameof(WeatherManager.TransitionTo))]
    public static class P_WeatherManager_TransitionTo
    {
        [HarmonyPrefix]
        public static bool TransitionTo(WeatherDef newWeather)
        {
            if (PatchHandler.BypassFlag) return true;
            else
            {
                if (!SessionHandler.IsSynchronousHost) return false;
                else
                {
                    SWeatherManager.Ask(newWeather);
                    return false;
                }
            }
        }
    }
}
