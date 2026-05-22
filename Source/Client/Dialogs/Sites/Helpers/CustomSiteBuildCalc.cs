using RimWorld;
using System;
using System.Linq;
using Verse;

namespace GameClient.Dialogs.Sites
{
    /// <summary>
    /// Pure helpers for the custom-site build dialog: cost / cycle-time
    /// formulas and best-skill lookup. Kept in step with the server-side
    /// formulas in <see cref="Shared.Files.Sites.CustomSiteData"/>.
    /// </summary>
    internal static class CustomSiteBuildCalc
    {
        // Pricing constants (must match server-side hardening floor).
        public const double PriceMultiplier = 3.0;
        public const int MinCost = 500;

        public static int CalculateCost(ThingDef selectedItem, int amount)
        {
            if (selectedItem == null) return 0;
            int cost = (int)Math.Ceiling(selectedItem.BaseMarketValue * amount * PriceMultiplier);
            return Math.Max(MinCost, cost);
        }

        public static int CalculateCycleMinutes(ThingDef selectedItem)
        {
            if (selectedItem == null) return 30;
            double min = 30.0 + (selectedItem.BaseMarketValue / 5.0);
            return (int)Math.Min(240, Math.Max(30, min));
        }

        /// <summary>Find the best skill level among all current-map colonists for a given skill.</summary>
        public static int GetBestColonistSkill(string skillDefName)
        {
            try
            {
                if (Find.CurrentMap == null) return 0;

                SkillDef skillDef = DefDatabase<SkillDef>.AllDefsListForReading
                    .FirstOrDefault(s => s.defName == skillDefName);
                if (skillDef == null) return 0;

                int best = 0;
                foreach (Pawn pawn in Find.CurrentMap.mapPawns.FreeColonists)
                {
                    SkillRecord skill = pawn.skills?.GetSkill(skillDef);
                    if (skill != null && skill.Level > best)
                        best = skill.Level;
                }
                return best;
            }
            catch { return 0; }
        }
    }
}
