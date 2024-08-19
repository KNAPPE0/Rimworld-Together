﻿using Shared;
using Verse;

namespace GameClient
{
    public static class CustomDifficultyManager
    {
        public static void SetCustomDifficulty(ServerGlobalData serverGlobalData)
        {
            DifficultyValues.UseCustomDifficulty = serverGlobalData.DifficultyValues.UseCustomDifficulty;

            DifficultyValues.ThreatScale = serverGlobalData.DifficultyValues.ThreatScale;

            DifficultyValues.AllowBigThreats = serverGlobalData.DifficultyValues.AllowBigThreats;

            DifficultyValues.AllowViolentQuests = serverGlobalData.DifficultyValues.AllowViolentQuests;

            DifficultyValues.AllowIntroThreats = serverGlobalData.DifficultyValues.AllowIntroThreats;

            DifficultyValues.PredatorsHuntHumanlikes = serverGlobalData.DifficultyValues.PredatorsHuntHumanlikes;

            DifficultyValues.AllowExtremeWeatherIncidents = serverGlobalData.DifficultyValues.AllowExtremeWeatherIncidents;

            DifficultyValues.CropYieldFactor = serverGlobalData.DifficultyValues.CropYieldFactor;

            DifficultyValues.MineYieldFactor = serverGlobalData.DifficultyValues.MineYieldFactor;

            DifficultyValues.ButcherYieldFactor = serverGlobalData.DifficultyValues.ButcherYieldFactor;

            DifficultyValues.ResearchSpeedFactor = serverGlobalData.DifficultyValues.ResearchSpeedFactor;

            DifficultyValues.QuestRewardValueFactor = serverGlobalData.DifficultyValues.QuestRewardValueFactor;

            DifficultyValues.RaidLootPointsFactor = serverGlobalData.DifficultyValues.RaidLootPointsFactor;

            DifficultyValues.TradePriceFactorLoss = serverGlobalData.DifficultyValues.TradePriceFactorLoss;

            DifficultyValues.MaintenanceCostFactor = serverGlobalData.DifficultyValues.MaintenanceCostFactor;

            DifficultyValues.ScariaRotChance = serverGlobalData.DifficultyValues.ScariaRotChance;

            DifficultyValues.EnemyDeathOnDownedChanceFactor = serverGlobalData.DifficultyValues.EnemyDeathOnDownedChanceFactor;

            DifficultyValues.ColonistMoodOffset = serverGlobalData.DifficultyValues.ColonistMoodOffset;

            DifficultyValues.FoodPoisonChanceFactor = serverGlobalData.DifficultyValues.FoodPoisonChanceFactor;

            DifficultyValues.ManhunterChanceOnDamageFactor = serverGlobalData.DifficultyValues.ManhunterChanceOnDamageFactor;

            DifficultyValues.PlayerPawnInfectionChanceFactor = serverGlobalData.DifficultyValues.PlayerPawnInfectionChanceFactor;

            DifficultyValues.DiseaseIntervalFactor = serverGlobalData.DifficultyValues.DiseaseIntervalFactor;

            DifficultyValues.EnemyReproductionRateFactor = serverGlobalData.DifficultyValues.EnemyReproductionRateFactor;

            DifficultyValues.DeepDrillInfestationChanceFactor = serverGlobalData.DifficultyValues.DeepDrillInfestationChanceFactor;

            DifficultyValues.FriendlyFireChanceFactor = serverGlobalData.DifficultyValues.FriendlyFireChanceFactor;

            DifficultyValues.AllowInstantKillChance = serverGlobalData.DifficultyValues.AllowInstantKillChance;

            DifficultyValues.PeacefulTemples = serverGlobalData.DifficultyValues.PeacefulTemples;

            DifficultyValues.AllowCaveHives = serverGlobalData.DifficultyValues.AllowCaveHives;

            DifficultyValues.UnwaveringPrisoners = serverGlobalData.DifficultyValues.UnwaveringPrisoners;

            DifficultyValues.AllowTraps = serverGlobalData.DifficultyValues.AllowTraps;

            DifficultyValues.AllowTurrets = serverGlobalData.DifficultyValues.AllowTurrets;

            DifficultyValues.AllowMortars = serverGlobalData.DifficultyValues.AllowMortars;

            DifficultyValues.ClassicMortars = serverGlobalData.DifficultyValues.ClassicMortars;

            DifficultyValues.AdaptationEffectFactor = serverGlobalData.DifficultyValues.AdaptationEffectFactor;

            DifficultyValues.AdaptationGrowthRateFactorOverZero = serverGlobalData.DifficultyValues.AdaptationGrowthRateFactorOverZero;

            DifficultyValues.FixedWealthMode = serverGlobalData.DifficultyValues.FixedWealthMode;

            DifficultyValues.LowPopConversionBoost = serverGlobalData.DifficultyValues.LowPopConversionBoost;

            DifficultyValues.NoBabiesOrChildren = serverGlobalData.DifficultyValues.NoBabiesOrChildren;

            DifficultyValues.BabiesAreHealthy = serverGlobalData.DifficultyValues.BabiesAreHealthy;

            DifficultyValues.ChildRaidersAllowed = serverGlobalData.DifficultyValues.ChildRaidersAllowed;

            DifficultyValues.ChildAgingRate = serverGlobalData.DifficultyValues.ChildAgingRate;

            DifficultyValues.AdultAgingRate = serverGlobalData.DifficultyValues.AdultAgingRate;

            DifficultyValues.WastepackInfestationChanceFactor = serverGlobalData.DifficultyValues.WastepackInfestationChanceFactor;
        }

        public static void SendCustomDifficulty()
        {
            DifficultyData difficultyData = new DifficultyData();

            difficultyData.ThreatScale = Current.Game.storyteller.difficulty.threatScale;

            difficultyData.AllowBigThreats = Current.Game.storyteller.difficulty.allowBigThreats;

            difficultyData.AllowViolentQuests = Current.Game.storyteller.difficulty.allowViolentQuests;

            difficultyData.AllowIntroThreats = Current.Game.storyteller.difficulty.allowIntroThreats;

            difficultyData.PredatorsHuntHumanlikes = Current.Game.storyteller.difficulty.predatorsHuntHumanlikes;

            difficultyData.AllowExtremeWeatherIncidents = Current.Game.storyteller.difficulty.allowExtremeWeatherIncidents;

            difficultyData.CropYieldFactor = Current.Game.storyteller.difficulty.cropYieldFactor;

            difficultyData.MineYieldFactor = Current.Game.storyteller.difficulty.mineYieldFactor;

            difficultyData.ButcherYieldFactor = Current.Game.storyteller.difficulty.butcherYieldFactor;

            difficultyData.ResearchSpeedFactor = Current.Game.storyteller.difficulty.researchSpeedFactor;

            difficultyData.QuestRewardValueFactor = Current.Game.storyteller.difficulty.questRewardValueFactor;

            difficultyData.RaidLootPointsFactor = Current.Game.storyteller.difficulty.raidLootPointsFactor;

            difficultyData.TradePriceFactorLoss = Current.Game.storyteller.difficulty.tradePriceFactorLoss;

            difficultyData.MaintenanceCostFactor = Current.Game.storyteller.difficulty.maintenanceCostFactor;

            difficultyData.ScariaRotChance = Current.Game.storyteller.difficulty.scariaRotChance;

            difficultyData.EnemyDeathOnDownedChanceFactor = Current.Game.storyteller.difficulty.enemyDeathOnDownedChanceFactor;

            difficultyData.ColonistMoodOffset = Current.Game.storyteller.difficulty.colonistMoodOffset;

            difficultyData.FoodPoisonChanceFactor = Current.Game.storyteller.difficulty.foodPoisonChanceFactor;

            difficultyData.ManhunterChanceOnDamageFactor = Current.Game.storyteller.difficulty.manhunterChanceOnDamageFactor;

            difficultyData.PlayerPawnInfectionChanceFactor = Current.Game.storyteller.difficulty.playerPawnInfectionChanceFactor;

            difficultyData.DiseaseIntervalFactor = Current.Game.storyteller.difficulty.diseaseIntervalFactor;

            difficultyData.EnemyReproductionRateFactor = Current.Game.storyteller.difficulty.enemyReproductionRateFactor;

            difficultyData.DeepDrillInfestationChanceFactor = Current.Game.storyteller.difficulty.deepDrillInfestationChanceFactor;

            difficultyData.FriendlyFireChanceFactor = Current.Game.storyteller.difficulty.friendlyFireChanceFactor;

            difficultyData.AllowInstantKillChance = Current.Game.storyteller.difficulty.allowInstantKillChance;

            difficultyData.PeacefulTemples = Current.Game.storyteller.difficulty.peacefulTemples;

            difficultyData.AllowCaveHives = Current.Game.storyteller.difficulty.allowCaveHives;

            difficultyData.UnwaveringPrisoners = Current.Game.storyteller.difficulty.unwaveringPrisoners;

            difficultyData.AllowTraps = Current.Game.storyteller.difficulty.allowTraps;

            difficultyData.AllowTurrets = Current.Game.storyteller.difficulty.allowTurrets;

            difficultyData.AllowMortars = Current.Game.storyteller.difficulty.allowMortars;

            difficultyData.ClassicMortars = Current.Game.storyteller.difficulty.classicMortars;

            difficultyData.AdaptationEffectFactor = Current.Game.storyteller.difficulty.adaptationEffectFactor;

            difficultyData.AdaptationGrowthRateFactorOverZero = Current.Game.storyteller.difficulty.adaptationGrowthRateFactorOverZero;

            difficultyData.FixedWealthMode = Current.Game.storyteller.difficulty.fixedWealthMode;

            difficultyData.LowPopConversionBoost = Current.Game.storyteller.difficulty.lowPopConversionBoost;

            difficultyData.NoBabiesOrChildren = Current.Game.storyteller.difficulty.noBabiesOrChildren;

            difficultyData.BabiesAreHealthy = Current.Game.storyteller.difficulty.babiesAreHealthy;

            difficultyData.ChildRaidersAllowed = Current.Game.storyteller.difficulty.childRaidersAllowed;

            difficultyData.ChildAgingRate = Current.Game.storyteller.difficulty.childAgingRate;

            difficultyData.AdultAgingRate = Current.Game.storyteller.difficulty.adultAgingRate;

            difficultyData.WastepackInfestationChanceFactor = Current.Game.storyteller.difficulty.wastepackInfestationChanceFactor;

            Packet packet = Packet.CreatePacketFromObject(nameof(PacketHandler.CustomDifficultyPacket), difficultyData);
            Network.Listener.EnqueuePacket(packet);
        }

        public static void EnforceCustomDifficulty()
        {
            if (!DifficultyValues.UseCustomDifficulty) return;
            else
            {
                Current.Game.storyteller.difficulty.threatScale = DifficultyValues.ThreatScale;

                Current.Game.storyteller.difficulty.allowBigThreats = DifficultyValues.AllowBigThreats;

                Current.Game.storyteller.difficulty.allowViolentQuests = DifficultyValues.AllowViolentQuests;

                Current.Game.storyteller.difficulty.allowIntroThreats = DifficultyValues.AllowIntroThreats;

                Current.Game.storyteller.difficulty.predatorsHuntHumanlikes = DifficultyValues.PredatorsHuntHumanlikes;

                Current.Game.storyteller.difficulty.allowExtremeWeatherIncidents = DifficultyValues.AllowExtremeWeatherIncidents;

                Current.Game.storyteller.difficulty.cropYieldFactor = DifficultyValues.CropYieldFactor;

                Current.Game.storyteller.difficulty.mineYieldFactor = DifficultyValues.MineYieldFactor;

                Current.Game.storyteller.difficulty.butcherYieldFactor = DifficultyValues.ButcherYieldFactor;

                Current.Game.storyteller.difficulty.researchSpeedFactor = DifficultyValues.ResearchSpeedFactor;

                Current.Game.storyteller.difficulty.questRewardValueFactor = DifficultyValues.QuestRewardValueFactor;

                Current.Game.storyteller.difficulty.raidLootPointsFactor = DifficultyValues.RaidLootPointsFactor;

                Current.Game.storyteller.difficulty.tradePriceFactorLoss = DifficultyValues.TradePriceFactorLoss;

                Current.Game.storyteller.difficulty.maintenanceCostFactor = DifficultyValues.MaintenanceCostFactor;

                Current.Game.storyteller.difficulty.scariaRotChance = DifficultyValues.ScariaRotChance;

                Current.Game.storyteller.difficulty.enemyDeathOnDownedChanceFactor = DifficultyValues.EnemyDeathOnDownedChanceFactor;

                Current.Game.storyteller.difficulty.colonistMoodOffset = DifficultyValues.ColonistMoodOffset;

                Current.Game.storyteller.difficulty.foodPoisonChanceFactor = DifficultyValues.FoodPoisonChanceFactor;

                Current.Game.storyteller.difficulty.manhunterChanceOnDamageFactor = DifficultyValues.ManhunterChanceOnDamageFactor;

                Current.Game.storyteller.difficulty.playerPawnInfectionChanceFactor = DifficultyValues.PlayerPawnInfectionChanceFactor;

                Current.Game.storyteller.difficulty.diseaseIntervalFactor = DifficultyValues.DiseaseIntervalFactor;

                Current.Game.storyteller.difficulty.enemyReproductionRateFactor = DifficultyValues.EnemyReproductionRateFactor;

                Current.Game.storyteller.difficulty.deepDrillInfestationChanceFactor = DifficultyValues.DeepDrillInfestationChanceFactor;

                Current.Game.storyteller.difficulty.friendlyFireChanceFactor = DifficultyValues.FriendlyFireChanceFactor;

                Current.Game.storyteller.difficulty.allowInstantKillChance = DifficultyValues.AllowInstantKillChance;

                Current.Game.storyteller.difficulty.peacefulTemples = DifficultyValues.PeacefulTemples;

                Current.Game.storyteller.difficulty.allowCaveHives = DifficultyValues.AllowCaveHives;

                Current.Game.storyteller.difficulty.unwaveringPrisoners = DifficultyValues.UnwaveringPrisoners;

                Current.Game.storyteller.difficulty.allowTraps = DifficultyValues.AllowTraps;

                Current.Game.storyteller.difficulty.allowTurrets = DifficultyValues.AllowTurrets;

                Current.Game.storyteller.difficulty.allowMortars = DifficultyValues.AllowMortars;

                Current.Game.storyteller.difficulty.classicMortars = DifficultyValues.ClassicMortars;

                Current.Game.storyteller.difficulty.adaptationEffectFactor = DifficultyValues.AdaptationEffectFactor;

                Current.Game.storyteller.difficulty.adaptationGrowthRateFactorOverZero = DifficultyValues.AdaptationGrowthRateFactorOverZero;

                Current.Game.storyteller.difficulty.fixedWealthMode = DifficultyValues.FixedWealthMode;

                Current.Game.storyteller.difficulty.lowPopConversionBoost = DifficultyValues.LowPopConversionBoost;

                Current.Game.storyteller.difficulty.noBabiesOrChildren = DifficultyValues.NoBabiesOrChildren;

                Current.Game.storyteller.difficulty.babiesAreHealthy = DifficultyValues.BabiesAreHealthy;

                Current.Game.storyteller.difficulty.childRaidersAllowed = DifficultyValues.ChildRaidersAllowed;

                Current.Game.storyteller.difficulty.childAgingRate = DifficultyValues.ChildAgingRate;

                Current.Game.storyteller.difficulty.adultAgingRate = DifficultyValues.AdultAgingRate;

                Current.Game.storyteller.difficulty.wastepackInfestationChanceFactor = DifficultyValues.WastepackInfestationChanceFactor;
            }
        }
    }

    public static class DifficultyValues
    {
        public static bool UseCustomDifficulty;

        public static float ThreatScale;

        public static bool AllowBigThreats;

        public static bool AllowViolentQuests;

        public static bool AllowIntroThreats;

        public static bool PredatorsHuntHumanlikes;

        public static bool AllowExtremeWeatherIncidents;

        public static float CropYieldFactor;

        public static float MineYieldFactor;

        public static float ButcherYieldFactor;

        public static float ResearchSpeedFactor;

        public static float QuestRewardValueFactor;

        public static float RaidLootPointsFactor;

        public static float TradePriceFactorLoss;

        public static float MaintenanceCostFactor;

        public static float ScariaRotChance;

        public static float EnemyDeathOnDownedChanceFactor;

        public static float ColonistMoodOffset;

        public static float FoodPoisonChanceFactor;

        public static float ManhunterChanceOnDamageFactor;

        public static float PlayerPawnInfectionChanceFactor;

        public static float DiseaseIntervalFactor;

        public static float EnemyReproductionRateFactor;

        public static float DeepDrillInfestationChanceFactor;

        public static float FriendlyFireChanceFactor;

        public static float AllowInstantKillChance;

        public static bool PeacefulTemples;

        public static bool AllowCaveHives;

        public static bool UnwaveringPrisoners;

        public static bool AllowTraps;

        public static bool AllowTurrets;

        public static bool AllowMortars;

        public static bool ClassicMortars;

        public static float AdaptationEffectFactor;

        public static float AdaptationGrowthRateFactorOverZero;

        public static bool FixedWealthMode;

        public static float LowPopConversionBoost;

        public static bool NoBabiesOrChildren;

        public static bool BabiesAreHealthy;

        public static bool ChildRaidersAllowed;

        public static float ChildAgingRate;

        public static float AdultAgingRate;

        public static float WastepackInfestationChanceFactor;
    }
}
