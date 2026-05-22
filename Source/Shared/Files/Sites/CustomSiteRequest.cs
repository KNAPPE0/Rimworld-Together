using Shared.Files.Economy;
using System;
using System.Collections.Generic;

namespace Shared.Files.Sites
{
    /// <summary>
    /// Represents a player's request to create a custom production site.
    /// Sent from client to server for validation and pricing.
    /// </summary>
    public class CustomSiteRequest
    {
        /// <summary>Item defName to produce (e.g., "Steel", "ComponentIndustrial").</summary>
        public string ItemDefName { get; set; } = string.Empty;

        /// <summary>Amount to produce per cycle.</summary>
        public int AmountPerCycle { get; set; } = 1;

        /// <summary>Client-calculated market value per unit (server will verify).</summary>
        public float MarketValuePerUnit { get; set; } = 0f;

        /// <summary>Access mode for the site.</summary>
        public SiteAccessMode AccessMode { get; set; } = SiteAccessMode.GuildOnly;

        /// <summary>Tax percentage taken from workers' rewards (0-50).</summary>
        public int OwnerTaxPercent { get; set; } = 10;

        /// <summary>Tile to build on.</summary>
        public int Tile { get; set; } = -1;

        /// <summary>Owner's chosen reward destination at build time.</summary>
        public RewardDestination OwnerRewardDestination { get; set; } = RewardDestination.Caravan;

        /// <summary>Unit price (silver) used when reward destination is Marketplace.</summary>
        public int MarketplaceUnitPrice { get; set; } = 1;
    }

    public enum SiteAccessMode
    {
        /// <summary>Only guild members can work this site.</summary>
        GuildOnly,
        /// <summary>Anyone can work this site (owner takes a cut).</summary>
        Public,
        /// <summary>Only the owner can work this site.</summary>
        Private
    }

    /// <summary>
    /// Extended site data for multi-worker custom sites.
    /// Stored alongside the SiteFile on the server.
    /// </summary>
    public class CustomSiteData
    {
        /// <summary>The item being produced.</summary>
        public string ItemDefName { get; set; } = string.Empty;

        /// <summary>Base amount produced per cycle (solo worker).</summary>
        public int BaseAmountPerCycle { get; set; } = 1;

        /// <summary>Market value per unit of the item.</summary>
        public float MarketValuePerUnit { get; set; } = 0f;

        /// <summary>Base cycle time in milliseconds (solo worker).</summary>
        public double BaseCycleTimeMs { get; set; } = 1800000;

        /// <summary>Access mode.</summary>
        public SiteAccessMode AccessMode { get; set; } = SiteAccessMode.GuildOnly;

        /// <summary>Tax percentage (0-50) taken from worker rewards and given to owner.</summary>
        public int OwnerTaxPercent { get; set; } = 10;

        /// <summary>List of usernames currently assigned as workers.</summary>
        public List<string> Workers { get; set; } = new List<string>();

        /// <summary>Maximum workers allowed.</summary>
        public int MaxWorkers { get; set; } = 5;

        /// <summary>
        /// Per-worker progress (XP, level, tenure, reward destination).
        /// Replaces the legacy <c>WorkerSkills</c> dict where the client
        /// could self-assert a skill value at join time.
        /// </summary>
        public Dictionary<string, WorkerProgress> WorkerProgress { get; set; } = new Dictionary<string, WorkerProgress>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Owner's reward destination. Worker overrides live in WorkerProgress.
        /// </summary>
        public RewardDestination OwnerRewardDestination { get; set; } = RewardDestination.Caravan;

        /// <summary>Unit price for marketplace auto-listing (when destination is Marketplace).</summary>
        public int MarketplaceUnitPrice { get; set; } = 1;

        /// <summary>
        /// The RimWorld skill defName most relevant to this site's production.
        /// Auto-determined based on item category.
        /// </summary>
        public string RelevantSkillDef { get; set; } = "Crafting";

        /// <summary>When the last reward was collected (UTC ticks).</summary>
        public long LastRewardUtcTicks { get; set; } = 0;

        /// <summary>Total silver earned by this site across all time.</summary>
        public double TotalSilverGenerated { get; set; } = 0;

        /// <summary>
        /// Average current level across all workers, derived from XP.
        /// </summary>
        public double GetAverageSkillLevel()
        {
            if (WorkerProgress == null || WorkerProgress.Count == 0) return 0;
            double total = 0;
            foreach (var kv in WorkerProgress) total += kv.Value?.CurrentLevel ?? 0;
            return total / WorkerProgress.Count;
        }

        /// <summary>
        /// Skill-based efficiency multiplier (0.6x at level 0 → 1.6x at level 20).
        /// Levels are earned through cycles served at this site, not asserted by the client.
        /// </summary>
        public double GetSkillEfficiency()
        {
            double avgSkill = GetAverageSkillLevel();
            if (avgSkill <= 0) return 1.0;
            return 0.6 + (avgSkill / 20.0) * 1.0;
        }

        /// <summary>
        /// Get total production multiplier combining workers AND skill.
        /// TotalMult = speedMult × skillEfficiency
        /// </summary>
        public double GetTotalProductionMultiplier()
        {
            return GetSpeedMultiplier() * GetSkillEfficiency();
        }

        /// <summary>
        /// Determine the most relevant RimWorld skill for an item.
        /// </summary>
        public static string DetermineRelevantSkill(string itemDefName)
        {
            if (string.IsNullOrEmpty(itemDefName)) return "Crafting";
            string lower = itemDefName.ToLower();

            // Mining/Quarry items
            if (lower.Contains("steel") || lower.Contains("plasteel") || lower.Contains("uranium") ||
                lower.Contains("jade") || lower.Contains("blocks") || lower.Contains("chunk") ||
                lower.Contains("gold") || lower.Contains("silver"))
                return "Mining";

            // Farming items
            if (lower.Contains("raw") || lower.Contains("corn") || lower.Contains("rice") ||
                lower.Contains("berry") || lower.Contains("hay") || lower.Contains("smokeleaf") ||
                lower.Contains("psychoid") || lower.Contains("cotton") || lower.Contains("devilstrand") ||
                lower.Contains("wood"))
                return "Plants";

            // Cooking items
            if (lower.Contains("meal") || lower.Contains("food") || lower.Contains("kibble") ||
                lower.Contains("pemmican") || lower.Contains("nutrient"))
                return "Cooking";

            // Medical items
            if (lower.Contains("medicine") || lower.Contains("herbal") || lower.Contains("glitter") ||
                lower.Contains("penox") || lower.Contains("luci"))
                return "Medicine";

            // Hunting items
            if (lower.Contains("meat") || lower.Contains("leather") || lower.Contains("wool") ||
                lower.Contains("fur") || lower.Contains("skin"))
                return "Animals";

            // Textile items
            if (lower.Contains("cloth") || lower.Contains("synthread") || lower.Contains("hyperweave"))
                return "Crafting";

            // High-tech items
            if (lower.Contains("component") || lower.Contains("spacer") || lower.Contains("archotech") ||
                lower.Contains("ai") || lower.Contains("techprof"))
                return "Intellectual";

            // Construction materials
            if (lower.Contains("brick") || lower.Contains("concrete"))
                return "Construction";

            // Weapons/armor
            if (lower.Contains("gun") || lower.Contains("rifle") || lower.Contains("pistol") ||
                lower.Contains("sword") || lower.Contains("armor") || lower.Contains("shield") ||
                lower.Contains("helmet") || lower.Contains("vest"))
                return "Crafting";

            // Chemfuel
            if (lower.Contains("chemfuel"))
                return "Crafting";

            return "Crafting"; // Default
        }

        /// <summary>
        /// Calculate effective production speed based on worker count.
        /// More workers = faster, but diminishing returns.
        /// Formula: speed = 1 + 0.5*ln(workers) for workers > 1
        /// 1 worker = 1x, 2 workers = 1.35x, 3 workers = 1.55x, 5 workers = 1.8x
        /// </summary>
        public double GetSpeedMultiplier()
        {
            int count = Workers?.Count ?? 0;
            if (count <= 0) return 0;
            if (count == 1) return 1.0;
            return 1.0 + 0.5 * Math.Log(count);
        }

        /// <summary>
        /// Get effective cycle time considering workers.
        /// More workers = shorter cycle.
        /// </summary>
        public double GetEffectiveCycleTimeMs()
        {
            double totalMult = GetTotalProductionMultiplier();
            if (totalMult <= 0) return double.MaxValue;
            return BaseCycleTimeMs / totalMult;
        }

        /// <summary>
        /// Calculate the build cost for this custom site.
        /// Cost = marketValue × amount × multiplier
        /// Minimum cost: 500 silver
        /// </summary>
        public static int CalculateBuildCost(float marketValue, int amount, double multiplier = 3.0)
        {
            int cost = (int)Math.Ceiling(marketValue * amount * multiplier);
            return Math.Max(500, cost);
        }

        /// <summary>
        /// Calculate base cycle time based on item value.
        /// Cheap items (wood, rice): 30 min
        /// Medium items (steel, components): 45-60 min
        /// Expensive items (spacer components, archotech): 120+ min
        /// Formula: minutes = 30 + (marketValue / 5)
        /// Capped at 240 min (4 hours)
        /// </summary>
        public static double CalculateCycleTimeMs(float marketValue)
        {
            double minutes = 30.0 + (marketValue / 5.0);
            minutes = Math.Min(minutes, 240.0);
            minutes = Math.Max(30.0, minutes);
            return minutes * 60.0 * 1000.0;
        }
    }
}
