using GameClient.Misc;
using GameClient.WorldObjects;
using HarmonyLib;
using RimWorld.Planet;
using Shared.Misc;
using System;
using System.Collections.Generic;
using Verse;
using static Shared.Misc.Printer;

namespace GameClient.Patches
{
    // Groundwork for future planet syncing. Today: logs world-object
    // add/remove at Verbose, coalescing bursts into one line every 2s
    // so raid spawns don't flood the log. Settlements / sites / caravans
    // are excluded — they have dedicated sync pipelines already.
    public static class Patch_WorldObjectsHolder
    {
        public static readonly List<Type> IgnoredTypes = new List<Type>
        {
            typeof(WO_Settlement),
            typeof(WO_Site),
            typeof(WO_Caravan),
            typeof(Caravan),
        };

        private static readonly Dictionary<string, int> EventCounts = new Dictionary<string, int>();
        private static readonly object EventLock = new object();
        private static DateTime LastFlushUtc = DateTime.UtcNow;
        private const double FlushIntervalSeconds = 2.0;

        public static bool ShouldIgnore(Type type)
        {
            if (type == null) return true;
            for (int i = 0; i < IgnoredTypes.Count; i++)
            {
                if (type == IgnoredTypes[i]) return true;
            }
            return false;
        }

        // Harmony patches can fire from any thread — lock to be safe.
        internal static void Record(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            lock (EventLock)
            {
                if (!EventCounts.ContainsKey(key)) EventCounts[key] = 0;
                EventCounts[key]++;

                DateTime now = DateTime.UtcNow;
                if ((now - LastFlushUtc).TotalSeconds < FlushIntervalSeconds) return;

                LastFlushUtc = now;
                foreach (KeyValuePair<string, int> kv in EventCounts)
                {
                    string msg = kv.Value <= 1
                        ? $"[PlanetSync] {kv.Key}"
                        : $"[PlanetSync] {kv.Key} x{kv.Value}";
                    Printer.Message(msg, LogImportanceMode.Verbose);
                }
                EventCounts.Clear();
            }
        }
    }

    [HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Add))]
    public static class Patch_WorldObjectsHolder_Add
    {
        [HarmonyPostfix]
        public static void DoPost(WorldObject o)
        {
            // Skip during initial world load — every existing WO would
            // get logged as "Added" otherwise.
            if (!SessionHandler.IsReadyToPlay) return;
            if (o == null) return;
            if (Patch_WorldObjectsHolder.ShouldIgnore(o.GetType())) return;

            Patch_WorldObjectsHolder.Record($"Added {o.GetType().Name}");
        }
    }

    [HarmonyPatch(typeof(WorldObjectsHolder), nameof(WorldObjectsHolder.Remove))]
    public static class Patch_WorldObjectsHolder_Remove
    {
        [HarmonyPostfix]
        public static void DoPost(WorldObject o)
        {
            if (!SessionHandler.IsReadyToPlay) return;
            if (o == null) return;
            if (Patch_WorldObjectsHolder.ShouldIgnore(o.GetType())) return;

            Patch_WorldObjectsHolder.Record($"Removed {o.GetType().Name}");
        }
    }
}
