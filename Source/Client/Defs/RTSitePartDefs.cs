using RimWorld;
using Verse;

namespace GameClient.Defs
{
    [StaticConstructorOnStartup]
    public static class RTSitePartDefs
    {
        public static SitePartDef[] Defs { get; set; } = new SitePartDef[]
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
            RTSitePartDefOf.RTMarketplace
        };
    }
}
