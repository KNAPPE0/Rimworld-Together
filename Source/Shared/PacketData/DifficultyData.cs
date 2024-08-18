using System;

namespace Shared
{
    [Serializable]
    public class DifficultyData
    {
        // General threat and event settings
        public float ThreatScale { get; set; } = 1.0f; // Default to 1.0, adjust as needed
        public bool AllowBigThreats { get; set; } = true; // Default to true
        public bool AllowViolentQuests { get; set; } = true; // Default to true
        public bool AllowIntroThreats { get; set; } = true; // Default to true
        public bool PredatorsHuntHumanlikes { get; set; } = true; // Default to true
        public bool AllowExtremeWeatherIncidents { get; set; } = true; // Default to true

        // Yield factors
        public float CropYieldFactor { get; set; } = 1.0f; // Default to 1.0
        public float MineYieldFactor { get; set; } = 1.0f; // Default to 1.0
        public float ButcherYieldFactor { get; set; } = 1.0f; // Default to 1.0

        // Speed factors
        public float ResearchSpeedFactor { get; set; } = 1.0f; // Default to 1.0

        // Economic factors
        public float QuestRewardValueFactor { get; set; } = 1.0f; // Default to 1.0
        public float RaidLootPointsFactor { get; set; } = 1.0f; // Default to 1.0
        public float TradePriceFactorLoss { get; set; } = 0.05f; // Default to 5% loss
        public float MaintenanceCostFactor { get; set; } = 1.0f; // Default to 1.0

        // Combat and survival factors
        public float ScariaRotChance { get; set; } = 1.0f; // Default to 1.0
        public float EnemyDeathOnDownedChanceFactor { get; set; } = 1.0f; // Default to 1.0
        public float ColonistMoodOffset { get; set; } = 0f; // Default to 0
        public float FoodPoisonChanceFactor { get; set; } = 1.0f; // Default to 1.0
        public float ManhunterChanceOnDamageFactor { get; set; } = 1.0f; // Default to 1.0
        public float PlayerPawnInfectionChanceFactor { get; set; } = 1.0f; // Default to 1.0
        public float DiseaseIntervalFactor { get; set; } = 1.0f; // Default to 1.0
        public float EnemyReproductionRateFactor { get; set; } = 1.0f; // Default to 1.0
        public float DeepDrillInfestationChanceFactor { get; set; } = 1.0f; // Default to 1.0
        public float FriendlyFireChanceFactor { get; set; } = 1.0f; // Default to 1.0
        public float AllowInstantKillChance { get; set; } = 0f; // Default to 0

        // Miscellaneous settings
        public bool PeacefulTemples { get; set; } = true; // Default to true
        public bool AllowCaveHives { get; set; } = true; // Default to true
        public bool UnwaveringPrisoners { get; set; } = true; // Default to true
        public bool AllowTraps { get; set; } = true; // Default to true
        public bool AllowTurrets { get; set; } = true; // Default to true
        public bool AllowMortars { get; set; } = true; // Default to true
        public bool ClassicMortars { get; set; } = false; // Default to false
        public float AdaptationEffectFactor { get; set; } = 1.0f; // Default to 1.0
        public float AdaptationGrowthRateFactorOverZero { get; set; } = 1.0f; // Default to 1.0
        public bool FixedWealthMode { get; set; } = false; // Default to false
        public float LowPopConversionBoost { get; set; } = 1.0f; // Default to 1.0
        public bool NoBabiesOrChildren { get; set; } = false; // Default to false
        public bool BabiesAreHealthy { get; set; } = true; // Default to true
        public bool ChildRaidersAllowed { get; set; } = true; // Default to true
        public float ChildAgingRate { get; set; } = 1.0f; // Default to 1.0
        public float AdultAgingRate { get; set; } = 1.0f; // Default to 1.0
        public float WastepackInfestationChanceFactor { get; set; } = 1.0f; // Default to 1.0
    }
}