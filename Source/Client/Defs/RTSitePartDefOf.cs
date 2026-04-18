using RimWorld;

namespace GameClient.Defs
{
    [DefOf]
    public static class RTSitePartDefOf
    {
        public static SitePartDef RTFarmland;

        public static SitePartDef RTHunterCamp;

        public static SitePartDef RTQuarry;

        public static SitePartDef RTSawmill;

        public static SitePartDef RTBank;

        public static SitePartDef RTLaboratory;

        public static SitePartDef RTRefinery;

        public static SitePartDef RTHerbalWorkshop;

        public static SitePartDef RTTextileFactory;

        public static SitePartDef RTFoodProcessor;

        // KMH: Marketplace and Custom Outpost
        public static SitePartDef RTMarketplace;

        public static SitePartDef RTCustomOutpost;

        static RTSitePartDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SitePartDefOf));
    }
}
