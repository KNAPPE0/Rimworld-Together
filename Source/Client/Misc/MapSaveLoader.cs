using GameClient.Managers;
using RimWorld;
using RimWorld.Planet;
using Shared.Files;
using Shared.Misc;
using Synchronous.Misc;
using System;
using System.Linq;
using Verse;
using static Shared.Misc.Printer;

namespace GameClient.Misc
{
    public static class MapSaveLoader
    {
        public enum OperationType { Get, Set }

        public static MapFile MapToString(Map map)
        {
            MapFile mapFile = new MapFile();

            mapFile.Tile = map.Tile;

            mapFile.Size = TypeConverter.IntVec3ToArray(map.Size);

            mapFile.Wealth = (int)map.wealthWatcher.WealthTotal;

            // KMH: Populate stats for leaderboard and colony stats features
            try
            {
                mapFile.WealthExact = map.wealthWatcher.WealthTotal;
                mapFile.GameTicks = Find.TickManager.TicksGame;
                mapFile.LastSavedUtcTicks = DateTime.UtcNow.Ticks;

                // Settlement/Faction name
                var playerFaction = Faction.OfPlayer;
                if (playerFaction != null)
                    mapFile.FactionName = playerFaction.Name ?? "Unknown";

                // Count pawns
                var allPawns = map.mapPawns.AllPawns;
                mapFile.ColonistCount = map.mapPawns.FreeColonistsCount;
                mapFile.FactionHumanCount = allPawns.Count(p => p.RaceProps.Humanlike && p.Faction == playerFaction);
                mapFile.NonFactionHumanCount = allPawns.Count(p => p.RaceProps.Humanlike && p.Faction != playerFaction);
                mapFile.FactionAnimalCount = allPawns.Count(p => p.RaceProps.Animal && p.Faction == playerFaction);
                mapFile.NonFactionAnimalCount = allPawns.Count(p => p.RaceProps.Animal && p.Faction != playerFaction);

                // Thing counts
                var allThings = map.listerThings.AllThings;
                mapFile.FactionThingCount = allThings.Count(t => t.Faction == playerFaction);
                mapFile.NonFactionThingCount = allThings.Count(t => t.Faction != null && t.Faction != playerFaction);

                // Settlement name
                var worldObject = Find.WorldObjects.SettlementAt(map.Tile);
                if (worldObject != null)
                    mapFile.SettlementName = worldObject.Label ?? "Unknown";

                // Username
                mapFile.Username = SessionHandler.Username ?? "Unknown";

                // KMH: Playtime from MapPlaytimeComponent
                MapPlaytimeComponent playtime = map.GetComponent<MapPlaytimeComponent>();
                if (playtime != null)
                    mapFile.RealPlayTimeSeconds = playtime.TotalSeconds;
            }
            catch { }

            ToggleWeather(OperationType.Get, mapFile, map);

            ToggleTerrain(OperationType.Get, mapFile, map);

            TogglePollution(OperationType.Get, mapFile, map);

            ToggleRoofs(OperationType.Get, mapFile, map);

            ToggleMapPawns(OperationType.Get, mapFile, map);

            ToggleMapThings(OperationType.Get, mapFile, map);

            return mapFile;
        }

        public static Map StringToMap(MapFile mapFile, bool enforceIDs = false)
        {
            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(mapFile.Tile, TypeConverter.ArrayToIntVec3(mapFile.Size), null);

            ToggleTerrain(OperationType.Set, mapFile, map);

            TogglePollution(OperationType.Set, mapFile, map);

            ToggleRoofs(OperationType.Set, mapFile, map);

            ToggleMapPawns(OperationType.Set, mapFile, map, enforceIDs);

            ToggleMapThings(OperationType.Set, mapFile, map, enforceIDs);

            ToggleWeather(OperationType.Set, mapFile, map);

            RegenerateRoofGrid(mapFile, map);

            RegenerateFog(map);

            return map;
        }

        private static void ToggleWeather(OperationType type, MapFile file, Map map)
        {
            if (type == OperationType.Set) map.weatherManager.TransitionTo(DefDatabase<WeatherDef>.AllDefs.ToList()[file.WeatherByte]);
            else file.WeatherByte = (byte)DefDatabase<WeatherDef>.AllDefs.FirstIndexOf(fetch => fetch == map.weatherManager.curWeather);
        }

        private static void ToggleTerrain(OperationType type, MapFile file, Map map)
        {
            int index = 0;

            for (int z = 0; z < map.Size.z; ++z)
            {
                for (int x = 0; x < map.Size.x; ++x)
                {
                    IntVec3 vector = new IntVec3(x, map.Size.y, z);
                    if (type == OperationType.Get) file.Tiles.Add(map.terrainGrid.TerrainAt(vector).defName);
                    else map.terrainGrid.SetTerrain(vector, DefDatabase<TerrainDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == file.Tiles[index]));

                    index++;
                }
            }
        }

        private static void TogglePollution(OperationType type, MapFile file, Map map)
        {
            int index = 0;

            for (int z = 0; z < map.Size.z; ++z)
            {
                for (int x = 0; x < map.Size.x; ++x)
                {
                    IntVec3 vector = new IntVec3(x, map.Size.y, z);
                    if (type == OperationType.Get) file.Pollutions.Add(map.pollutionGrid.IsPolluted(vector));
                    else map.pollutionGrid.SetPolluted(vector, file.Pollutions[index]);

                    index++;
                }
            }
        }

        private static void ToggleRoofs(OperationType type, MapFile file, Map map)
        {
            int index = 0;

            for (int z = 0; z < map.Size.z; ++z)
            {
                for (int x = 0; x < map.Size.x; ++x)
                {
                    IntVec3 vector = new IntVec3(x, map.Size.y, z);

                    if (type == OperationType.Get)
                    {
                        try { file.Roofs.Add(map.roofGrid.RoofAt(vector).defName); }
                        catch { file.Roofs.Add(null); }
                    }

                    else
                    {
                        try { map.roofGrid.SetRoof(vector, DefDatabase<RoofDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == file.Roofs[index])); }
                        catch { }
                    }

                    index++;
                }
            }
        }

        private static void ToggleMapThings(OperationType type, MapFile file, Map map, bool enforceIDs = false)
        {
            if (type == OperationType.Get)
            {
                foreach (Thing thing in map.listerThings.AllThings.Where(fetch => !RimworldManager.CheckIfThingIsPawn(fetch)).ToArray())
                {
                    try { file.Things.Add(ScribeManager.SerializeToString(thing, ScribeManager.SerializableType.Thing)); }
                    catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
                }
            }

            else
            {
                foreach (string str in file.Things)
                {
                    try
                    {
                        Thing thing = ScribeManager.SerializeFromString<Thing>(str, ScribeManager.SerializableType.Thing, enforceIDs);
                        RimworldManager.PlaceThingIntoMap(thing, map, thing.Position);
                    }
                    catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
                }
            }
        }

        private static void ToggleMapPawns(OperationType type, MapFile file, Map map, bool enforceIDs = false)
        {
            if (type == OperationType.Get)
            {
                foreach (Thing pawn in map.listerThings.AllThings.Where(fetch => RimworldManager.CheckIfThingIsPawn(fetch)).ToArray())
                {
                    try { file.Pawns.Add(ScribeManager.SerializeToString(pawn, ScribeManager.SerializableType.Pawn)); }
                    catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
                }
            }

            else
            {
                foreach (string str in file.Pawns)
                {
                    try
                    {
                        Pawn pawn = ScribeManager.SerializeFromString<Pawn>(str, ScribeManager.SerializableType.Pawn, enforceIDs);
                        RimworldManager.PlaceThingIntoMap(pawn, map, pawn.PositionHeld);
                    }
                    catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
                }
            }
        }

        private static void RegenerateRoofGrid(MapFile file, Map map)
        {
            map.roofCollapseBuffer.Clear();
            map.roofGrid.Drawer.SetDirty();
        }

        private static void RegenerateFog(Map map)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            Caravan caravan = CaravanMaker.MakeCaravan(new Pawn[] { pawn }, Faction.OfPlayer, map.Tile, true);
            CaravanEnterMapUtility.Enter(caravan, map, CaravanEnterMode.Edge);

            FloodFillerFog.FloodUnfog(MapGenerator.PlayerStartSpot, map);

            pawn.Destroy();
        }
    }
}
