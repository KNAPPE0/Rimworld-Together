using RimWorld;
using System.Linq;
using Verse;

namespace GameClient.Defs
{
    [StaticConstructorOnStartup]
    public static class RTSitePartDefs
    {
        public static SitePartDef[] Defs { get; private set; }

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
        }

        public static SitePartDef GetByDefName(string defName)
        {
            if (string.IsNullOrWhiteSpace(defName))
                return null;

            SitePartDef direct = Defs.FirstOrDefault(fetch => fetch != null && fetch.defName == defName);
            if (direct != null)
                return direct;

            return DefDatabase<SitePartDef>.AllDefsListForReading
                .FirstOrDefault(fetch => fetch != null && fetch.defName == defName);
        }
    }
}