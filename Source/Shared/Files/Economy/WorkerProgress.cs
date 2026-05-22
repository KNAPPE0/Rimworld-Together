using System;

namespace Shared.Files.Economy
{
    /// <summary>
    /// Per-worker tenure + XP record stored on a custom site.
    ///
    /// XP is awarded when a reward cycle completes. The level derived from XP
    /// drives the production multiplier — replacing the previous flow where
    /// the client asserted a static skill value at join time.
    ///
    /// Vanilla RimWorld uses an exponential XP curve. We use a simplified one:
    ///     xpForLevel(L) = 1000 + L * 1000
    ///     So L0->L1 = 1000, L1->L2 = 2000, ..., L19->L20 = 20000
    ///     Total to L20 = ~210,000 XP
    /// At ~250 XP per cycle (60-min cycles default), a fresh worker reaches
    /// L10 in ~22 hours of game-time presence and L20 in ~14 days. That is
    /// intentionally slow — it rewards persistent guild membership.
    /// </summary>
    public class WorkerProgress
    {
        /// <summary>UTC ticks at which this worker joined the site.</summary>
        public long JoinedUtcTicks { get; set; }

        /// <summary>Number of reward cycles credited to this worker.</summary>
        public int CyclesCompleted { get; set; }

        /// <summary>Total relevant-skill XP accumulated at this site.</summary>
        public double Xp { get; set; }

        /// <summary>
        /// KMH 26.5.20.1: Base skill level the worker brought to the site,
        /// derived from their assigned pawn's RimWorld skill matching the
        /// site's RelevantSkillDef. Server-clamped to 0..20. A pawn with
        /// Crafting 15 starts contributing at level 15 immediately — they
        /// don't have to grind 1000s of cycles to be useful.
        ///
        /// On-site cycles still grant XP. CurrentLevel = max(BaseSkillLevel,
        /// XP-derived level), so a worker can outgrow their starting skill
        /// over time.
        /// </summary>
        public int BaseSkillLevel { get; set; } = 0;

        /// <summary>
        /// Per-worker reward destination override. Falls back to the site's
        /// default if Caravan is the desired override (i.e. the legacy default).
        /// </summary>
        public RewardDestination Destination { get; set; } = RewardDestination.Caravan;

        public const double XpPerCycle = 250.0;

        /// <summary>Resolve current level (0..20) from accumulated XP.</summary>
        public int CurrentLevel
        {
            get
            {
                // KMH 26.5.20.1: Combine the worker's pre-existing pawn
                // skill with anything they've earned through on-site cycles.
                // The maximum keeps a fresh experienced pawn from being
                // "dragged down" to L0 while still rewarding long-term
                // workers who grind past their starting skill.
                int xpLevel = LevelFromXp(Xp);
                int baseClamped = BaseSkillLevel < 0 ? 0 : (BaseSkillLevel > 20 ? 20 : BaseSkillLevel);
                return System.Math.Max(baseClamped, xpLevel);
            }
        }

        private static int LevelFromXp(double xp)
        {
            double remaining = xp;
            for (int level = 0; level < 20; level++)
            {
                double need = XpForNextLevel(level);
                if (remaining < need) return level;
                remaining -= need;
            }
            return 20;
        }

        /// <summary>How much XP to advance from <paramref name="level"/> to level+1.</summary>
        public static double XpForNextLevel(int level)
        {
            if (level < 0) level = 0;
            if (level >= 20) return double.MaxValue;
            return 1000.0 + level * 1000.0;
        }

        /// <summary>Progress 0.0–1.0 toward the next level.</summary>
        public double ProgressToNextLevel
        {
            get
            {
                double remaining = Xp;
                for (int level = 0; level < 20; level++)
                {
                    double need = XpForNextLevel(level);
                    if (remaining < need) return remaining / need;
                    remaining -= need;
                }
                return 1.0;
            }
        }

        /// <summary>Award XP for completing a cycle. Returns the new level.</summary>
        public int AwardCycleXp(double extraMultiplier = 1.0)
        {
            CyclesCompleted++;
            Xp += XpPerCycle * extraMultiplier;
            return CurrentLevel;
        }
    }
}
