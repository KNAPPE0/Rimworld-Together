using System;

namespace Shared
{
    [Serializable]
    public class SiteValuesFile
    {
        public int PersonalFarmlandCost { get; private set; } = 1000;
        public int FactionFarmlandCost { get; private set; } = 2000;
        public int FarmlandRewardCount { get; private set; } = 50;

        public int PersonalQuarryCost { get; private set; } = 1000;
        public int FactionQuarryCost { get; private set; } = 2000;
        public int QuarryRewardCount { get; private set; } = 50;

        public int PersonalSawmillCost { get; private set; } = 1000;
        public int FactionSawmillCost { get; private set; } = 2000;
        public int SawmillRewardCount { get; private set; } = 50;

        public int PersonalBankCost { get; private set; } = 1000;
        public int FactionBankCost { get; private set; } = 2000;
        public int BankRewardCount { get; private set; } = 50;

        public int PersonalLaboratoryCost { get; private set; } = 1000;
        public int FactionLaboratoryCost { get; private set; } = 2000;
        public int LaboratoryRewardCount { get; private set; } = 50;

        public int PersonalRefineryCost { get; private set; } = 1000;
        public int FactionRefineryCost { get; private set; } = 2000;
        public int RefineryRewardCount { get; private set; } = 50;

        public int PersonalHerbalWorkshopCost { get; private set; } = 1000;
        public int FactionHerbalWorkshopCost { get; private set; } = 2000;
        public int HerbalWorkshopRewardCount { get; private set; } = 50;

        public int PersonalTextileFactoryCost { get; private set; } = 1000;
        public int FactionTextileFactoryCost { get; private set; } = 2000;
        public int TextileFactoryRewardCount { get; private set; } = 50;

        public int PersonalFoodProcessorCost { get; private set; } = 1000;
        public int FactionFoodProcessorCost { get; private set; } = 2000;
        public int FoodProcessorRewardCount { get; private set; } = 50;

        // Parameterless constructor for default initialization
        public SiteValuesFile() { }

        // Constructor for specific value initialization
        public SiteValuesFile(int personalFarmlandCost, int factionFarmlandCost, int farmlandRewardCount,
                              int personalQuarryCost, int factionQuarryCost, int quarryRewardCount,
                              int personalSawmillCost, int factionSawmillCost, int sawmillRewardCount,
                              int personalBankCost, int factionBankCost, int bankRewardCount,
                              int personalLaboratoryCost, int factionLaboratoryCost, int laboratoryRewardCount,
                              int personalRefineryCost, int factionRefineryCost, int refineryRewardCount,
                              int personalHerbalWorkshopCost, int factionHerbalWorkshopCost, int herbalWorkshopRewardCount,
                              int personalTextileFactoryCost, int factionTextileFactoryCost, int textileFactoryRewardCount,
                              int personalFoodProcessorCost, int factionFoodProcessorCost, int foodProcessorRewardCount)
        {
            PersonalFarmlandCost = personalFarmlandCost;
            FactionFarmlandCost = factionFarmlandCost;
            FarmlandRewardCount = farmlandRewardCount;

            PersonalQuarryCost = personalQuarryCost;
            FactionQuarryCost = factionQuarryCost;
            QuarryRewardCount = quarryRewardCount;

            PersonalSawmillCost = personalSawmillCost;
            FactionSawmillCost = factionSawmillCost;
            SawmillRewardCount = sawmillRewardCount;

            PersonalBankCost = personalBankCost;
            FactionBankCost = factionBankCost;
            BankRewardCount = bankRewardCount;

            PersonalLaboratoryCost = personalLaboratoryCost;
            FactionLaboratoryCost = factionLaboratoryCost;
            LaboratoryRewardCount = laboratoryRewardCount;

            PersonalRefineryCost = personalRefineryCost;
            FactionRefineryCost = factionRefineryCost;
            RefineryRewardCount = refineryRewardCount;

            PersonalHerbalWorkshopCost = personalHerbalWorkshopCost;
            FactionHerbalWorkshopCost = factionHerbalWorkshopCost;
            HerbalWorkshopRewardCount = herbalWorkshopRewardCount;

            PersonalTextileFactoryCost = personalTextileFactoryCost;
            FactionTextileFactoryCost = factionTextileFactoryCost;
            TextileFactoryRewardCount = textileFactoryRewardCount;

            PersonalFoodProcessorCost = personalFoodProcessorCost;
            FactionFoodProcessorCost = factionFoodProcessorCost;
            FoodProcessorRewardCount = foodProcessorRewardCount;
        }
    }
}