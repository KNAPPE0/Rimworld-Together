using Shared.Files.Economy;
using System;
using System.Collections.Generic;

namespace Shared.Files.Sites
{
    // Client → server custom-site build request. Server re-validates everything.
    public class CustomSiteRequest
    {
        public string ItemDefName { get; set; } = string.Empty;

        public int AmountPerCycle { get; set; } = 1;

        // Client-calculated; server re-checks.
        public float MarketValuePerUnit { get; set; } = 0f;

        public SiteAccessMode AccessMode { get; set; } = SiteAccessMode.GuildOnly;

        // 0-50.
        public int OwnerTaxPercent { get; set; } = 10;

        public int Tile { get; set; } = -1;

        public RewardDestination OwnerRewardDestination { get; set; } = RewardDestination.Caravan;

        // Used when reward destination is Marketplace.
        public int MarketplaceUnitPrice { get; set; } = 1;
    }

    public enum SiteAccessMode
    {
        GuildOnly,
        Public,  // owner takes a cut
        Private
    }

    // Server-side extended data attached to a SiteFile for multi-worker sites.
    public class CustomSiteData
    {
        public string ItemDefName { get; set; } = string.Empty;

        // Solo-worker baseline.
        public int BaseAmountPerCycle { get; set; } = 1;

        public float MarketValuePerUnit { get; set; } = 0f;

        // Solo-worker baseline.
        public double BaseCycleTimeMs { get; set; } = 1800000;

        public SiteAccessMode AccessMode { get; set; } = SiteAccessMode.GuildOnly;

        // 0-50, transferred from worker rewards to owner.
        public int OwnerTaxPercent { get; set; } = 10;

        public List<string> Workers { get; set; } = new List<string>();

        public int MaxWorkers { get; set; } = 5;

        // Replaces legacy WorkerSkills (client could self-assert skill on join).
        public Dictionary<string, WorkerProgress> WorkerProgress { get; set; } = new Dictionary<string, WorkerProgress>(StringComparer.OrdinalIgnoreCase);

        // Worker-specific overrides live in WorkerProgress.
        public RewardDestination OwnerRewardDestination { get; set; } = RewardDestination.Caravan;

        public int MarketplaceUnitPrice { get; set; } = 1;

        // Auto-determined from item category.
        public string RelevantSkillDef { get; set; } = "Crafting";

        public long LastRewardUtcTicks { get; set; } = 0;

        public double TotalSilverGenerated { get; set; } = 0;

        public double GetAverageSkillLevel()
        {
            if (WorkerProgress == null || WorkerProgress.Count == 0) return 0;
            double total = 0;
            foreach (var kv in WorkerProgress) total += kv.Value?.CurrentLevel ?? 0;
            return total / WorkerProgress.Count;
        }

        // 0.6x at level 0 → 1.6x at level 20. Levels are earned, not asserted.
        public double GetSkillEfficiency()
        {
            double avgSkill = GetAverageSkillLevel();
            if (avgSkill <= 0) return 1.0;
            return 0.6 + (avgSkill / 20.0) * 1.0;
        }

        public double GetTotalProductionMultiplier()
        {
            return GetSpeedMultiplier() * GetSkillEfficiency();
        }

        public static string DetermineRelevantSkill(string itemDefName)
        {
            if (string.IsNullOrEmpty(itemDefName)) return "Crafting";
            string lower = itemDefName.ToLower();

            if (lower.Contains("steel") || lower.Contains("plasteel") || lower.Contains("uranium") ||
                lower.Contains("jade") || lower.Contains("blocks") || lower.Contains("chunk") ||
                lower.Contains("gold") || lower.Contains("silver"))
                return "Mining";

            if (lower.Contains("raw") || lower.Contains("corn") || lower.Contains("rice") ||
                lower.Contains("berry") || lower.Contains("hay") || lower.Contains("smokeleaf") ||
                lower.Contains("psychoid") || lower.Contains("cotton") || lower.Contains("devilstrand") ||
                lower.Contains("wood"))
                return "Plants";

            if (lower.Contains("meal") || lower.Contains("food") || lower.Contains("kibble") ||
                lower.Contains("pemmican") || lower.Contains("nutrient"))
                return "Cooking";

            if (lower.Contains("medicine") || lower.Contains("herbal") || lower.Contains("glitter") ||
                lower.Contains("penox") || lower.Contains("luci"))
                return "Medicine";

            if (lower.Contains("meat") || lower.Contains("leather") || lower.Contains("wool") ||
                lower.Contains("fur") || lower.Contains("skin"))
                return "Animals";

            if (lower.Contains("cloth") || lower.Contains("synthread") || lower.Contains("hyperweave"))
                return "Crafting";

            if (lower.Contains("component") || lower.Contains("spacer") || lower.Contains("archotech") ||
                lower.Contains("ai") || lower.Contains("techprof"))
                return "Intellectual";

            if (lower.Contains("brick") || lower.Contains("concrete"))
                return "Construction";

            if (lower.Contains("gun") || lower.Contains("rifle") || lower.Contains("pistol") ||
                lower.Contains("sword") || lower.Contains("armor") || lower.Contains("shield") ||
                lower.Contains("helmet") || lower.Contains("vest"))
                return "Crafting";

            if (lower.Contains("chemfuel"))
                return "Crafting";

            return "Crafting";
        }

        // 1=1x, 2=1.35x, 3=1.55x, 5=1.8x. Diminishing returns: 1 + 0.5*ln(workers).
        public double GetSpeedMultiplier()
        {
            int count = Workers?.Count ?? 0;
            if (count <= 0) return 0;
            if (count == 1) return 1.0;
            return 1.0 + 0.5 * Math.Log(count);
        }

        public double GetEffectiveCycleTimeMs()
        {
            double totalMult = GetTotalProductionMultiplier();
            if (totalMult <= 0) return double.MaxValue;
            return BaseCycleTimeMs / totalMult;
        }

        // marketValue × amount × multiplier, min 500 silver.
        public static int CalculateBuildCost(float marketValue, int amount, double multiplier = 3.0)
        {
            int cost = (int)Math.Ceiling(marketValue * amount * multiplier);
            return Math.Max(500, cost);
        }

        // 30 + (marketValue / 5) minutes, clamped to [30, 240].
        public static double CalculateCycleTimeMs(float marketValue)
        {
            double minutes = 30.0 + (marketValue / 5.0);
            minutes = Math.Min(minutes, 240.0);
            minutes = Math.Max(30.0, minutes);
            return minutes * 60.0 * 1000.0;
        }
    }
}
