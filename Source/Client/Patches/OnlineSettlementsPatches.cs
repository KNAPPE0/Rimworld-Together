using GameClient.Misc;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Patches
{
    [HarmonyPatch(typeof(SettlementDefeatUtility), nameof(SettlementDefeatUtility.CheckDefeated))]
    public static class PatchSettlementJoin
    {
        [HarmonyPrefix]
        public static bool DoPre(Settlement factionBase)
        {
            if (SessionHandler.PlayerFactions.Contains(factionBase.Faction)) return false;
            else return true;
        }
    }

    [HarmonyPatch(typeof(CaravanEnterMapUtility), nameof(CaravanEnterMapUtility.Enter), new[] { typeof(Caravan), typeof(Map), typeof(Func<Pawn, IntVec3>), typeof(CaravanDropInventoryMode), typeof(bool) })]
    public static class PatchCaravanEnterMapUtility1
    {
        [HarmonyPostfix]
        public static void DoPost(Map map)
        {
            if (!SessionHandler.PlayerFactions.Contains(map.Parent.Faction)) return;

            FloodFillerFog.DebugRefogMap(map);
        }
    }

    [HarmonyPatch(typeof(CaravanEnterMapUtility), nameof(CaravanEnterMapUtility.Enter), new[] { typeof(Caravan), typeof(Map), typeof(CaravanEnterMode), typeof(CaravanDropInventoryMode), typeof(bool), typeof(Predicate<IntVec3>) })]
    public static class PatchCaravanEnterMapUtility2
    {
        [HarmonyPostfix]
        public static void DoPost(Map map)
        {
            if (!SessionHandler.PlayerFactions.Contains(map.Parent.Faction)) return;

            FloodFillerFog.DebugRefogMap(map);
        }
    }

    [HarmonyPatch(typeof(SitePartWorker_Outpost), "GetEnemiesCount")]
    public static class PatchSiteEnemyCount
    {
        [HarmonyPrefix]
        public static bool DoPre(Site site, ref int __result)
        {
            if (SessionHandler.PlayerFactions.Contains(site.Faction) || site.Faction == Faction.OfPlayer)
            {
                __result = 25;
                return false;
            }
            else return true;
        }
    }
}
