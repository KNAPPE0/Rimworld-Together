using GameClient.WorldObjects;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace GameClient.Misc
{
    public static class Finder
    {
        public static Map GetMapFromTile(int tile) { return Find.Maps.FirstOrDefault(fetch => fetch.Tile == tile); }

        public static Pawn GetPawnFromID(Map map, string id) { return GetAllPawnsInMap(map).FirstOrDefault(fetch => fetch.ThingID == id); }

        public static Thing GetThingFromID(Map map, string id) { return GetAllThingsInMap(map).FirstOrDefault(fetch => fetch.ThingID == id); }

        public static Pawn[] GetAllPawnsInMap(Map map) { return map.mapPawns.AllPawns.ToArray(); }

        public static Thing[] GetAllThingsInMap(Map map) { return map.listerThings.AllThings.ToArray(); }

        public static MentalStateDef GetMentalStateDefFromByte(byte value)
        {
            return DefDatabase<MentalStateDef>.AllDefs.ToList()[value];
        }

        public static WeatherDef GetWeatherDefFromByte(byte value)
        {
            return DefDatabase<WeatherDef>.AllDefs.ToList()[value];
        }

        public static WO_Settlement GetRTSettlementFromTile(int tile) 
        { 
            return (WO_Settlement)Find.World.worldObjects.AllWorldObjects.First(fetch => fetch.Tile == tile && fetch is WO_Settlement); 
        }

        public static WO_Site GetRTSiteFromTile(int tile)
        {
            return (WO_Site)Find.World.worldObjects.AllWorldObjects.First(fetch => fetch.Tile == tile && fetch is WO_Site);
        }

        public static WorldObject[] GetAllRTSettlements()
        {
            return (WorldObject[])Find.World.worldObjects.AllWorldObjects.FindAll(fetch => fetch is WO_Settlement).ToArray();
        }

        public static WorldObject[] GetAllRTSites()
        {
            return (WorldObject[])Find.World.worldObjects.AllWorldObjects.FindAll(fetch => fetch is WO_Site).ToArray();
        }

        public static Hediff GetHediffFromPart(Pawn pawn, BodyPartRecord part, string hediffDefname, bool forceUntended)
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.def.defName == hediffDefname)
                {
                    if (part == null || hediff.Part.def.defName == part.def.defName)
                    {
                        if (!forceUntended) return hediff;
                        else
                        {
                            if (hediff.IsTended()) continue;
                            else return hediff;
                        }
                    }
                }
            }

            return null;
        }

        public static BodyPartRecord GetBodyPartFromDefname(Pawn pawn, string defName)
        {
            return pawn.health.hediffSet.GetNotMissingParts().FirstOrDefault(fetch => fetch.def.defName == defName);
        }
    }
}
