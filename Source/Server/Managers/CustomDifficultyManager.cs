using Shared;
using static Shared.CommonEnumerators;

namespace GameServer
{
    public static class CustomDifficultyManager
    {
        public static void ParseDifficultyPacket(ServerClient client, Packet packet)
        {
            // Deserialize the difficulty data from the packet
            var difficultyData = Serializer.ConvertBytesToObject<DifficultyData>(packet.Contents);
            if (difficultyData == null)
            {
                Logger.Warning("[CustomDifficultyManager] > Failed to deserialize difficulty data.");
                return;
            }

            SetCustomDifficulty(client, difficultyData);
        }

        public static void SetCustomDifficulty(ServerClient client, DifficultyData difficultyData)
        {
            if (!client.userFile.IsAdmin)
            {
                ResponseShortcutManager.SendIllegalPacket(client, $"Player {client.userFile.Username} attempted to set the custom difficulty while not being an admin.");
                return;
            }

            // Update the master difficulty values with the provided data
            Master.difficultyValues = new DifficultyValuesFile
            {
                ThreatScale = difficultyData.ThreatScale,
                AllowBigThreats = difficultyData.AllowBigThreats,
                AllowViolentQuests = difficultyData.AllowViolentQuests,
                AllowIntroThreats = difficultyData.AllowIntroThreats,
                PredatorsHuntHumanlikes = difficultyData.PredatorsHuntHumanlikes,
                AllowExtremeWeatherIncidents = difficultyData.AllowExtremeWeatherIncidents,
                CropYieldFactor = difficultyData.CropYieldFactor,
                MineYieldFactor = difficultyData.MineYieldFactor,
                ButcherYieldFactor = difficultyData.ButcherYieldFactor,
                ResearchSpeedFactor = difficultyData.ResearchSpeedFactor,
                QuestRewardValueFactor = difficultyData.QuestRewardValueFactor,
                RaidLootPointsFactor = difficultyData.RaidLootPointsFactor,
                TradePriceFactorLoss = difficultyData.TradePriceFactorLoss,
                MaintenanceCostFactor = difficultyData.MaintenanceCostFactor,
                ScariaRotChance = difficultyData.ScariaRotChance,
                EnemyDeathOnDownedChanceFactor = difficultyData.EnemyDeathOnDownedChanceFactor,
                ColonistMoodOffset = difficultyData.ColonistMoodOffset,
                FoodPoisonChanceFactor = difficultyData.FoodPoisonChanceFactor,
                ManhunterChanceOnDamageFactor = difficultyData.ManhunterChanceOnDamageFactor,
                PlayerPawnInfectionChanceFactor = difficultyData.PlayerPawnInfectionChanceFactor,
                DiseaseIntervalFactor = difficultyData.DiseaseIntervalFactor,
                EnemyReproductionRateFactor = difficultyData.EnemyReproductionRateFactor,
                DeepDrillInfestationChanceFactor = difficultyData.DeepDrillInfestationChanceFactor,
                FriendlyFireChanceFactor = difficultyData.FriendlyFireChanceFactor,
                AllowInstantKillChance = difficultyData.AllowInstantKillChance,
                PeacefulTemples = difficultyData.PeacefulTemples,
                AllowCaveHives = difficultyData.AllowCaveHives,
                UnwaveringPrisoners = difficultyData.UnwaveringPrisoners,
                AllowTraps = difficultyData.AllowTraps,
                AllowTurrets = difficultyData.AllowTurrets,
                AllowMortars = difficultyData.AllowMortars,
                ClassicMortars = difficultyData.ClassicMortars,
                AdaptationEffectFactor = difficultyData.AdaptationEffectFactor,
                AdaptationGrowthRateFactorOverZero = difficultyData.AdaptationGrowthRateFactorOverZero,
                FixedWealthMode = difficultyData.FixedWealthMode,
                LowPopConversionBoost = difficultyData.LowPopConversionBoost,
                NoBabiesOrChildren = difficultyData.NoBabiesOrChildren,
                BabiesAreHealthy = difficultyData.BabiesAreHealthy,
                ChildRaidersAllowed = difficultyData.ChildRaidersAllowed,
                ChildAgingRate = difficultyData.ChildAgingRate,
                AdultAgingRate = difficultyData.AdultAgingRate,
                WastepackInfestationChanceFactor = difficultyData.WastepackInfestationChanceFactor
            };

            Logger.Warning($"[Set difficulty] > {client.userFile.Username}");

            // Save the updated difficulty values
            Master.SaveValueFile(ServerFileMode.Difficulty);
        }
    }
}