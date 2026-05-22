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
    /// <summary>
    /// KMH 26.5.22.1: Ported from upstream RWT (May 2026 — "Added
    /// groundwork for planet syncing"). Pre-wiring for a future feature
    /// where world-object adds/removes get synced across multiplayer
    /// clients in real time (think bandits spawning on the world map,
    /// other players placing markers, etc.).
    ///
    /// <para>Today this just <i>logs</i> add/remove events at Verbose
    /// level — it doesn't actually send packets. The hooks land here
    /// because they're cheap (one Type check per event) and adding them
    /// now means the future packet-emitter doesn't need a Harmony patch
    /// migration when planet sync ships properly.</para>
    ///
    /// <para><b>KMH improvement over upstream</b>: rapid-fire WO churn
    /// (e.g. raid spawn batching, caravan unloading) used to flood the
    /// Verbose log with hundreds of consecutive lines. We coalesce
    /// events into a single rolled-up log message every
    /// <see cref="FlushIntervalSeconds"/> seconds. The flush is keyed by
    /// "&lt;action&gt; &lt;type-name&gt;" so a burst of 50 bandit camps
    /// renders as "[PlanetSync] Added 50 × BanditCamp" instead of 50
    /// separate lines. Players who care about per-event detail can still
    /// scrape the synced packets once the feature lands; the log is
    /// strictly for KMH developers debugging the sync layer.</para>
    ///
    /// <para><b>Ignored types</b>: settlements, sites, and caravans
    /// already have dedicated sync paths (PM_Settlements / PM_Sites /
    /// PM_Caravan) so we exclude them here to avoid double-logging.</para>
    /// </summary>
    public static class Patch_WorldObjectsHolder
    {
        // KMH 26.5.22.1: Excluded from the generic add/remove log
        // because each of these has its own dedicated sync pipeline
        // (see PM_Settlements, PM_Sites, PM_Caravan). Duplicating their
        // events here would spam the Verbose log on every legitimate
        // sync.
        public static readonly List<Type> IgnoredTypes = new List<Type>
        {
            typeof(WO_Settlement),
            typeof(WO_Site),
            typeof(WO_Caravan),
            typeof(Caravan),
        };

        // KMH 26.5.22.1: Rolling event counters, keyed by "Added Foo" /
        // "Removed Foo". Drained on a schedule so multi-event bursts
        // produce one log line instead of N.
        private static readonly Dictionary<string, int> EventCounts = new Dictionary<string, int>();
        private static readonly object EventLock = new object();
        private static DateTime LastFlushUtc = DateTime.UtcNow;

        private const double FlushIntervalSeconds = 2.0;

        /// <summary>
        /// KMH 26.5.22.1: Returns true if the object's type should be
        /// excluded from generic planet-sync logging. Centralised so the
        /// add and remove patches stay in sync.
        /// </summary>
        public static bool ShouldIgnore(Type type)
        {
            if (type == null) return true;
            for (int i = 0; i < IgnoredTypes.Count; i++)
            {
                if (type == IgnoredTypes[i]) return true;
            }
            return false;
        }

        /// <summary>
        /// KMH 26.5.22.1: Increment the rolling counter for the given
        /// event key and drain the log if the flush interval has
        /// elapsed. Thread-safe — Harmony patches can fire from any
        /// thread depending on what RimWorld is doing.
        /// </summary>
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
                        : $"[PlanetSync] {kv.Key} ×{kv.Value}";
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
            // KMH 26.5.22.1: Only log once we're actually in-game, not
            // during initial world load (where every existing WO would
            // get logged as "Added" — spammy and useless).
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
