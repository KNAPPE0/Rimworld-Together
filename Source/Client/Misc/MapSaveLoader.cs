using GameClient.Defs;
using GameClient.Managers;
using RimWorld;
using RimWorld.Planet;
using Shared.Files;
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

            ToggleTerrain(OperationType.Get, mapFile, map);
            TogglePollution(OperationType.Get, mapFile, map);
            ToggleRoofs(OperationType.Get, mapFile, map);
            ToggleMapPawns(OperationType.Get, mapFile, map);
            ToggleMapThings(OperationType.Get, mapFile, map);

            return mapFile;
        }

        public static Map StringToMap(MapFile mapFile, bool enforceIDs = false)
        {
            Map map = GetOrGenerateMapUtility.GetOrGenerateMap(
                mapFile.Tile,
                ValueParser.ArrayToIntVec3(mapFile.Size),
                null);

            if (map == null) return null;

            ToggleTerrain(OperationType.Set, mapFile, map);
            TogglePollution(OperationType.Set, mapFile, map);
            ToggleRoofs(OperationType.Set, mapFile, map);
            ToggleMapPawns(OperationType.Set, mapFile, map, enforceIDs);
            ToggleMapThings(OperationType.Set, mapFile, map, enforceIDs);

            ToggleWeather(OperationType.Set, mapFile, map);
            RegenerateRoofGrid(map);
            RegenerateFog(map);

            return map;
        }

        private static void ToggleWeather(OperationType type, MapFile file, Map map)
        {
            if (type == OperationType.Set)
                map.weatherManager.TransitionTo(DefDatabase<WeatherDef>.AllDefs.ToList()[file.WeatherByte]);
            else
                file.WeatherByte = (byte)DefDatabase<WeatherDef>.AllDefs.FirstIndexOf(fetch => fetch == map.weatherManager.curWeather);
        }

        private static void ToggleTerrain(OperationType type, MapFile file, Map map)
        {
            int index = 0;

            for (int z = 0; z < map.Size.z; ++z)
            {
                for (int x = 0; x < map.Size.x; ++x)
                {
                    try
                    {
                        IntVec3 vector = new IntVec3(x, map.Size.y, z);

                        if (type == OperationType.Get)
                        {
                            TerrainDef terrain = map.terrainGrid.TerrainAt(vector);
                            file.Tiles.Add(terrain != null ? terrain.defName : null);
                        }
                        else
                        {
                            if (file.Tiles != null && index < file.Tiles.Count && !string.IsNullOrEmpty(file.Tiles[index]))
                            {
                                TerrainDef terrainToUse = DefDatabase<TerrainDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == file.Tiles[index]);
                                if (terrainToUse != null)
                                    map.terrainGrid.SetTerrain(vector, terrainToUse);
                            }
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

        private static void TogglePollution(OperationType type, MapFile file, Map map)
        {
            int index = 0;

            for (int z = 0; z < map.Size.z; ++z)
            {
                for (int x = 0; x < map.Size.x; ++x)
                {
                    try
                    {
                        IntVec3 vector = new IntVec3(x, map.Size.y, z);

                        if (type == OperationType.Get)
                        {
                            file.Pollutions.Add(map.pollutionGrid.IsPolluted(vector));
                        }
                        else
                        {
                            if (file.Pollutions != null && index < file.Pollutions.Count)
                                map.pollutionGrid.SetPolluted(vector, file.Pollutions[index]);
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

        private static void ToggleRoofs(OperationType type, MapFile file, Map map)
        {
            int index = 0;

            for (int z = 0; z < map.Size.z; ++z)
            {
                for (int x = 0; x < map.Size.x; ++x)
                {
                    try
                    {
                        IntVec3 vector = new IntVec3(x, map.Size.y, z);

                        if (type == OperationType.Get)
                        {
                            RoofDef roof = map.roofGrid.RoofAt(vector);
                            file.Roofs.Add(roof != null ? roof.defName : null);
                        }
                        else
                        {
                            if (file.Roofs != null && index < file.Roofs.Count && !string.IsNullOrEmpty(file.Roofs[index]))
                            {
                                RoofDef roofToUse = DefDatabase<RoofDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == file.Roofs[index]);
                                if (roofToUse != null)
                                    map.roofGrid.SetRoof(vector, roofToUse);
                            }
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

        private static void ToggleMapThings(OperationType type, MapFile file, Map map, bool enforceIDs = false)
        {
            if (type == OperationType.Get)
            {
                foreach (Thing thing in map.listerThings.AllThings.Where(fetch => !RimworldManager.CheckIfThingIsPawn(fetch)).ToArray())
                {
                    try
                    {
                        file.Things.Add(ScribeManager.SerializeToString(thing, ScribeManager.SerializableType.Thing));
                    }
                    catch (Exception e)
                    {
                        Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                    }
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
                    catch (Exception e)
                    {
                        Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                    }
                }
            }
        }

        private static void ToggleMapPawns(OperationType type, MapFile file, Map map, bool enforceIDs = false)
        {
            if (type == OperationType.Get)
            {
                foreach (Thing pawn in map.listerThings.AllThings.Where(fetch => RimworldManager.CheckIfThingIsPawn(fetch)).ToArray())
                {
                    try
                    {
                        file.Pawns.Add(ScribeManager.SerializeToString(pawn, ScribeManager.SerializableType.Pawn));
                    }
                    catch (Exception e)
                    {
                        Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                    }
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
                    catch (Exception e)
                    {
                        Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                    }
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