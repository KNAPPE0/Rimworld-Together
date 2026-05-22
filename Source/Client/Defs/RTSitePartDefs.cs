using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace GameClient.Defs
{
    [StaticConstructorOnStartup]
    public static class RTSitePartDefs
    {
        public static SitePartDef[] Defs { get; private set; }

        // KMH: Backing dictionary so GetByDefName is O(1).
        private static Dictionary<string, SitePartDef> DefsByName;

        static RTSitePartDefs()
        {
            Defs = new SitePartDef[]
            {
                RTSitePartDefOf.RTFarmland,
                RTSitePartDefOf.RTHunterCamp,
                RTSitePartDefOf.RTQuarry,
                RTSitePartDefOf.RTSawmill,
                RTSitePartDefOf.RTBank,
                RTSitePartDefOf.RTLaboratory,
                RTSitePartDefOf.RTRefinery,
                RTSitePartDefOf.RTHerbalWorkshop,
                RTSitePartDefOf.RTTextileFactory,
                RTSitePartDefOf.RTFoodProcessor,
                RTSitePartDefOf.RTMarketplace,
                RTSitePartDefOf.RTCustomOutpost
            }
            .Where(d => d != null)
            .Distinct()
            .ToArray();

            DefsByName = new Dictionary<string, SitePartDef>(Defs.Length);
            foreach (SitePartDef def in Defs)
            {
                if (def != null && !string.IsNullOrEmpty(def.defName))
                    DefsByName[def.defName] = def;
            }
        }

        public static SitePartDef GetByDefName(string defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
                return null;

            if (DefsByName.TryGetValue(defName, out SitePartDef cached))
                return cached;

            return DefDatabase<SitePartDef>.GetNamedSilentFail(defName);
        }
    }
}
