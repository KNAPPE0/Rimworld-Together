using Shared.Files.Sites;
using System.Collections.Generic;

namespace Shared.Files.Actions
{
    public class SiteAction : BaseAction
    {
        public bool IsEnabled { get; set; } = true;

        public double Cooldown { get; set; } = -1;

        /// <summary>How often rewards are produced (ms). Default: 30 minutes.</summary>
        public double TimeInterval { get; set; } = 1800000;

        /// <summary>Whether players can create custom site types.</summary>
        public bool AllowCustomSites { get; set; } = false;

        /// <summary>Price multiplier for custom sites (higher = more expensive).</summary>
        public double CustomSitePriceMultiplier { get; set; } = 3.0;

        /// <summary>Maximum reward items per cycle for custom sites.</summary>
        public int CustomSiteMaxRewardAmount { get; set; } = 50;

        // === KMH: Economy / treasury / marketplace ===

        /// <summary>Marketplace tax (0-50). Goes to the server "house" silver pool.</summary>
        public int MarketplaceTaxPercent { get; set; } = 5;

        /// <summary>How long a marketplace listing lives before unsold stock returns to seller treasury.</summary>
        public int MarketplaceListingLifetimeHours { get; set; } = 168; // 7 days

        /// <summary>Hard cap on simultaneous open listings per seller — anti-spam.</summary>
        public int MarketplaceMaxOpenListingsPerUser { get; set; } = 25;

        /// <summary>Silver per unit floor for any marketplace listing — anti-flooding.</summary>
        public int MarketplaceMinUnitPrice { get; set; } = 1;

        /// <summary>Silver per unit cap — prevents int-overflow shenanigans.</summary>
        public int MarketplaceMaxUnitPrice { get; set; } = 100_000;

        /// <summary>Multiplier on XP gained per cycle by workers. 1.0 = default.</summary>
        public double WorkerXpMultiplier { get; set; } = 1.0;

        /// <summary>
        /// When true, marketplace listing/buying packets require the caller to
        /// have an active caravan or be at a site. Admins always bypass.
        /// Quest posting/claiming follows the same rule.
        /// </summary>
        public bool RequireSiteAccessForEconomy { get; set; } = false;

        public List<SiteType> SiteTypes { get; set; } = new List<SiteType>()
        {
            // === BASIC RESOURCE SITES (Low Cost: 300-500 silver) ===
            // Produce common raw materials. Reward every 30 min cycle.

            new SiteType()
            {
                DefName = "RTFarmland",
                Cost = 400,
                Description = "Produces crops every cycle. Low cost, steady food supply.",
                CycleTimeMinutes = 30,
                Rewards = new SiteReward[] {
                    new SiteReward() { DefName = "RawRice", Amount = 50 },
                    new SiteReward() { DefName = "RawCorn", Amount = 50 },
                    new SiteReward() { DefName = "SmokeleafLeaves", Amount = 25 },
                    new SiteReward() { DefName = "PsychoidLeaves", Amount = 25 }
                }
            },

            new SiteType()
            {
                DefName = "RTSawmill",
                Cost = 300,
                Description = "Produces wood logs every cycle. Cheapest site to build.",
                CycleTimeMinutes = 30,
                Rewards = new SiteReward[] {
                    new SiteReward() { DefName = "WoodLog", Amount = 100 }
                }
            },

            new SiteType()
            {
                DefName = "RTHunterCamp",
                Cost = 500,
                Description = "Produces meat and leather every cycle.",
                CycleTimeMinutes = 30,
                Rewards = new SiteReward[] {
                    new SiteReward() { DefName = "Meat_Muffalo", Amount = 125 },
                    new SiteReward() { DefName = "Meat_Human", Amount = 125 },
                    new SiteReward() { DefName = "Leather_Chinchilla", Amount = 60 },
                    new SiteReward() { DefName = "Leather_Bear", Amount = 60 }
                }
            },

            // === INTERMEDIATE SITES (Medium Cost: 750 silver) ===
            // Produce processed or semi-rare materials. Same cycle time.

            new SiteType()
            {
                DefName = "RTQuarry",
                Cost = 750,
                Description = "Extracts stone blocks and metals every cycle.",
                CycleTimeMinutes = 30,
                Rewards = new SiteReward[] {
                    new SiteReward() { DefName = "BlocksGranite", Amount = 50 },
                    new SiteReward() { DefName = "BlocksMarble", Amount = 50 },
                    new SiteReward() { DefName = "Steel", Amount = 30 },
                    new SiteReward() { DefName = "Plasteel", Amount = 10 }
                }
            },

            new SiteType()
            {
                DefName = "RTRefinery",
                Cost = 750,
                Description = "Produces chemfuel every cycle.",
                CycleTimeMinutes = 30,
                Rewards = new SiteReward[] {
                    new SiteReward() { DefName = "Chemfuel", Amount = 50 }
                }
            },

            new SiteType()
            {
                DefName = "RTTextileFactory",
                Cost = 750,
                Description = "Produces cloth and devilstrand every cycle.",
                CycleTimeMinutes = 30,
                Rewards = new SiteReward[] {
                    new SiteReward() { DefName = "Cloth", Amount = 50 },
                    new SiteReward() { DefName = "DevilstrandCloth", Amount = 30 }
                }
            },

            new SiteType()
            {
                DefName = "RTFoodProcessor",
                Cost = 750,
                Description = "Produces survival meals and nutrient paste every cycle.",
                CycleTimeMinutes = 30,
                Rewards = new SiteReward[] {
                    new SiteReward() { DefName = "MealSurvivalPack", Amount = 10 },
                    new SiteReward() { DefName = "MealNutrientPaste", Amount = 30 }
                }
            },

            // === ADVANCED SITES (High Cost: 1000-1500 silver) ===
            // Produce rare/valuable items. Longer cycle time.

            new SiteType()
            {
                DefName = "RTBank",
                Cost = 1000,
                Description = "Generates silver and gold every cycle. High value, slow production.",
                CycleTimeMinutes = 45,
                Rewards = new SiteReward[] {
                    new SiteReward() { DefName = "Silver", Amount = 75 },
                    new SiteReward() { DefName = "Gold", Amount = 15 }
                }
            },

            new SiteType()
            {
                DefName = "RTLaboratory",
                Cost = 1200,
                Description = "Produces industrial and spacer components. Very valuable, slow cycle.",
                CycleTimeMinutes = 60,
                Rewards = new SiteReward[] {
                    new SiteReward() { DefName = "ComponentIndustrial", Amount = 10 },
                    new SiteReward() { DefName = "ComponentSpacer", Amount = 2 }
                }
            },

            new SiteType()
            {
                DefName = "RTHerbalWorkshop",
                Cost = 1000,
                Description = "Produces medicine every cycle.",
                CycleTimeMinutes = 45,
                Rewards = new SiteReward[] {
                    new SiteReward() { DefName = "MedicineHerbal", Amount = 15 },
                    new SiteReward() { DefName = "MedicineIndustrial", Amount = 5 }
                }
            },

            // === MARKETPLACE (Premium Cost: 2000 silver) ===
            // Trading hub - produces silver from trade fees.

            new SiteType()
            {
                DefName = "RTMarketplace",
                Cost = 2000,
                Description = "A trading marketplace. Generates silver from trade activity. Premium investment with high returns.",
                CycleTimeMinutes = 60,
                Rewards = new SiteReward[] {
                    new SiteReward() { DefName = "Silver", Amount = 150 },
                    new SiteReward() { DefName = "Gold", Amount = 5 },
                    new SiteReward() { DefName = "ComponentIndustrial", Amount = 3 }
                }
            }
        };
    }
}
