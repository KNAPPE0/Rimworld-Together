using System;

namespace Shared.Files.Guilds
{
    /// <summary>
    /// Spendable guild upgrades — silver from the treasury buys per-tier perks
    /// that benefit every member. Replaces the old per-site upgrade flow.
    ///
    /// Tiers go 0..MaxLevel. Cost climbs quadratically so high tiers feel earned.
    /// </summary>
    public class GuildPerks
    {
        public const int MaxLevel = 3;

        /// <summary>Adds +0/+2/+4/+6 to the MaxWorkers cap on every guild member's custom site.</summary>
        public int SiteMaxWorkersBonusLevel { get; set; }

        /// <summary>Reduces the marketplace house tax by 0/1/2/3 percentage points for guild members.</summary>
        public int MarketplaceTaxReductionLevel { get; set; }

        /// <summary>Multiplies cycle XP by 1.0/1.25/1.5/2.0 for workers at guild members' sites.</summary>
        public int WorkerXpBonusLevel { get; set; }

        /// <summary>Discounts custom-site build cost by 0/10/20/30%.</summary>
        public int CustomSiteCostDiscountLevel { get; set; }

        // -- effect resolvers --

        public int SiteMaxWorkersBonus => Clamp(SiteMaxWorkersBonusLevel) * 2;

        public int MarketplaceTaxReductionPoints => Clamp(MarketplaceTaxReductionLevel);

        public double WorkerXpMultiplier
        {
            get
            {
                switch (Clamp(WorkerXpBonusLevel))
                {
                    case 0: return 1.0;
                    case 1: return 1.25;
                    case 2: return 1.5;
                    default: return 2.0;
                }
            }
        }

        public double CustomSiteCostMultiplier => 1.0 - 0.10 * Clamp(CustomSiteCostDiscountLevel);

        // -- cost ladder --

        /// <summary>Silver cost to advance from <paramref name="currentLevel"/> to current+1.</summary>
        public static int CostFor(int currentLevel)
        {
            int next = Math.Max(0, currentLevel + 1);
            // 5,000 → 15,000 → 30,000
            return 5_000 * next * (next + 1) / 2;
        }

        private static int Clamp(int level) => Math.Max(0, Math.Min(MaxLevel, level));
    }
}
