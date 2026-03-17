using GameClient.Defs;
using GameClient.Managers;
using RimWorld;
using RimWorld.Planet;
using Shared.Files;
using Shared.Files.Maps;
using Shared.Misc;
using System;
using System.Linq;
using System.Reflection;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Misc
{
    public static class MapSaveLoader
    {
        public enum OperationType { Get, Set }

        public static MapFile MapToString(Map map)
        {
            MapFile mapFile = new MapFile();

            mapFile.Tile = map.Tile;
            mapFile.Size = ValueParser.IntVec3ToArray(map.Size);

            mapFile.Wealth = (int)map.wealthWatcher.WealthTotal;
            mapFile.WealthExact = map.wealthWatcher.WealthTotal;

            mapFile.GameTicks = Find.TickManager != null ? Find.TickManager.TicksGame : -1;
            mapFile.LastSavedUtcTicks = DateTime.UtcNow.Ticks;

            ToggleWeather(OperationType.Get, mapFile, map);
            mapFile.CurWeatherDefName = map.weatherManager?.curWeather?.defName ?? string.Empty;

            mapFile.Mods = ModManagerH.GetRunningModList();

            mapFile.SettlementName = GetCommunityNameSafe(map);
            mapFile.FactionName = GetFactionNameSafe();

            (double totalSeconds, double interactingSeconds) = GetRimWorldPlaytimesSafe(map);
            mapFile.RealPlayTimeInteractingSeconds = interactingSeconds;
            mapFile.RealPlayTimeSeconds = totalSeconds;

            GetMapTerrain(mapFile, map);
            GetMapThings(mapFile, map);
            GetMapPawns(mapFile, map);

            return mapFile;
        }

        public static Map StringToMap(MapFile mapFile, bool enforceIDs = false)
        {
            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(
                mapFile.Tile,
                ValueParser.ArrayToIntVec3(mapFile.Size),
                null);

            if (map == null) return null;

            SetMapTerrain(mapFile, map);
            SetMapThings(mapFile, map, enforceIDs);
            SetMapPawns(mapFile, map, enforceIDs);

            ToggleWeather(OperationType.Set, mapFile, map);
            RegenerateRoofGrid(map);
            RegenerateFog(map);

            return map;
        }

        private static void GetMapTerrain(MapFile mapFile, Map map)
        {
            for (int z = 0; z < map.Size.z; ++z)
            {
                for (int x = 0; x < map.Size.x; ++x)
                {
                    try
                    {
                        MapTile component = new MapTile();
                        IntVec3 vectorToCheck = new IntVec3(x, map.Size.y, z);

                        TerrainDef terrainDef = map.terrainGrid.TerrainAt(vectorToCheck);
                        if (terrainDef != null)
                            component.TileString = terrainDef.defName;

                        component.IsPolluted = map.pollutionGrid.IsPolluted(vectorToCheck);

                        RoofDef roofDef = map.roofGrid.RoofAt(vectorToCheck);
                        if (roofDef != null)
                            component.RoofString = roofDef.defName;

                        mapFile.Tiles.Add(component);
                    }
                    catch (Exception e)
                    {
                        Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                    }
                }
            }
        }

        private static void GetMapThings(MapFile mapFile, Map map)
        {
            foreach (Thing thing in map.listerThings.AllThings.Where(fetch => !RimworldManager.CheckIfThingIsPawn(fetch)).ToArray())
            {
                try
                {
                    mapFile.Things.Add(ScribeManager.SerializeToString(thing, ScribeManager.SerializableType.Thing));
                }
                catch (Exception e)
                {
                    Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                }
            }
        }

        private static void GetMapPawns(MapFile mapFile, Map map)
        {
            foreach (Thing pawn in map.listerThings.AllThings.Where(fetch => RimworldManager.CheckIfThingIsPawn(fetch)).ToArray())
            {
                try
                {
                    mapFile.Pawns.Add(ScribeManager.SerializeToString(pawn, ScribeManager.SerializableType.Pawn));
                }
                catch (Exception e)
                {
                    Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                }
            }
        }

        private static void SetMapTerrain(MapFile mapFile, Map map)
        {
            int index = 0;

            for (int z = 0; z < map.Size.z; ++z)
            {
                for (int x = 0; x < map.Size.x; ++x)
                {
                    try
                    {
                        MapTile component = mapFile.Tiles[index];
                        IntVec3 vectorToCheck = new IntVec3(x, map.Size.y, z);

                        map.pollutionGrid.SetPolluted(vectorToCheck, component.IsPolluted);

                        if (!string.IsNullOrEmpty(component.TileString))
                        {
                            TerrainDef terrainToUse = DefDatabase<TerrainDef>.AllDefs.First(fetch => fetch.defName == component.TileString);
                            map.terrainGrid.SetTerrain(vectorToCheck, terrainToUse);
                        }

                        if (!string.IsNullOrEmpty(component.RoofString))
                        {
                            RoofDef roofToUse = DefDatabase<RoofDef>.AllDefs.First(fetch => fetch.defName == component.RoofString);
                            map.roofGrid.SetRoof(vectorToCheck, roofToUse);
                        }
                    }
                    catch (Exception e)
                    {
                        Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                    }

                    index++;
                }
            }
        }

        private static void SetMapThings(MapFile mapFile, Map map, bool enforceIDs)
        {
            foreach (string str in mapFile.Things)
            {
                try
                {
                    Thing thing = ScribeManager.SerializeFromString<Thing>(str, ScribeManager.SerializableType.Thing, enforceIDs);
                    RimworldManager.PlaceThingIntoMap(thing, map, thing.Position);
                }
                catch (Exception e)
                {
                    Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                }
            }
        }

        private static void SetMapPawns(MapFile mapFile, Map map, bool enforceIDs)
        {
            foreach (string str in mapFile.Pawns)
            {
                try
                {
                    Pawn pawn = ScribeManager.SerializeFromString<Pawn>(str, ScribeManager.SerializableType.Pawn, enforceIDs);
                    RimworldManager.PlaceThingIntoMap(pawn, map, pawn.PositionHeld);
                }
                catch (Exception e)
                {
                    Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                }
            }
        }

        private static void RegenerateRoofGrid(Map map)
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

        private static void ToggleWeather(OperationType type, MapFile mapFile, Map map)
        {
            if (type == OperationType.Set)
                map.weatherManager.TransitionTo(DefDatabase<WeatherDef>.AllDefs.ToList()[mapFile.WeatherByte]);
            else
                mapFile.WeatherByte = (byte)DefDatabase<WeatherDef>.AllDefs.FirstIndexOf(fetch => fetch == map.weatherManager.curWeather);
        }

        private static string GetFactionNameSafe()
        {
            try
            {
                if (Faction.OfPlayer != null)
                {
                    if (!string.IsNullOrWhiteSpace(Faction.OfPlayer.Name))
                        return Faction.OfPlayer.Name;

                    if (Faction.OfPlayer.def != null && !string.IsNullOrWhiteSpace(Faction.OfPlayer.def.label))
                        return Faction.OfPlayer.def.label;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string GetCommunityNameSafe(Map map)
        {
            try
            {
                if (map == null)
                    return string.Empty;

                try
                {
                    PropertyInfo parentProp = map.GetType().GetProperty("Parent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    object parent = parentProp != null ? parentProp.GetValue(map, null) : null;

                    if (parent != null)
                    {
                        PropertyInfo labelProp = parent.GetType().GetProperty("Label", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        object labelObj = labelProp != null ? labelProp.GetValue(parent, null) : null;
                        string label = labelObj as string;

                        if (!string.IsNullOrWhiteSpace(label))
                            return label;
                    }
                }
                catch
                {
                }

                object infoObj = null;

                PropertyInfo infoPropLower = map.GetType().GetProperty("info", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                PropertyInfo infoPropUpper = map.GetType().GetProperty("Info", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (infoPropLower != null)
                    infoObj = infoPropLower.GetValue(map, null);
                else if (infoPropUpper != null)
                    infoObj = infoPropUpper.GetValue(map, null);

                if (infoObj != null)
                {
                    PropertyInfo parentProp = infoObj.GetType().GetProperty("parent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                           ?? infoObj.GetType().GetProperty("Parent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    object parent = parentProp != null ? parentProp.GetValue(infoObj, null) : null;

                    if (parent != null)
                    {
                        PropertyInfo labelProp = parent.GetType().GetProperty("Label", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        object labelObj = labelProp != null ? labelProp.GetValue(parent, null) : null;
                        string label = labelObj as string;

                        if (!string.IsNullOrWhiteSpace(label))
                            return label;
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static (double totalSeconds, double interactingSeconds) GetRimWorldPlaytimesSafe(Map map)
        {
            double total = -1;
            double interacting = -1;

            try
            {
                if (Current.Game == null)
                    return (-1, -1);

                object infoObj = null;

                try
                {
                    FieldInfo infoField = Current.Game.GetType().GetField("info", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (infoField != null)
                        infoObj = infoField.GetValue(Current.Game);
                }
                catch
                {
                }

                if (infoObj == null)
                {
                    try
                    {
                        PropertyInfo infoProp = Current.Game.GetType().GetProperty("Info", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                             ?? Current.Game.GetType().GetProperty("info", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                        if (infoProp != null)
                            infoObj = infoProp.GetValue(Current.Game, null);
                    }
                    catch
                    {
                    }
                }

                if (infoObj != null)
                {
                    total = ReadNumericMember(infoObj, "realPlayTime", "RealPlayTime");
                    interacting = ReadNumericMember(infoObj, "realPlayTimeInteracting", "RealPlayTimeInteracting");
                }

                if (interacting < 0 && map != null)
                {
                    try
                    {
                        RT_MapPlaytimeComponent comp = map.GetComponent<RT_MapPlaytimeComponent>();
                        if (comp != null)
                            interacting = comp.TotalSeconds;
                    }
                    catch
                    {
                    }
                }

                return (total, interacting);
            }
            catch
            {
                return (-1, -1);
            }
        }

        private static double ReadNumericMember(object obj, string fieldName, string propName)
        {
            try
            {
                FieldInfo f = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null)
                {
                    object v = f.GetValue(obj);
                    if (v != null)
                        return Convert.ToDouble(v);
                }
            }
            catch
            {
            }

            try
            {
                PropertyInfo p = obj.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                               ?? obj.GetType().GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (p != null)
                {
                    object v = p.GetValue(obj, null);
                    if (v != null)
                        return Convert.ToDouble(v);
                }
            }
            catch
            {
            }

            return -1;
        }
    }
}