using GameClient.Managers;
using RimWorld;
using Shared.Files;
using Shared.Files.Maps;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Misc
{
    public static class MapSaveLoader
    {
        public static MapFile MapToString(Map map)
        {
            MapFile mapFile = new MapFile();

            mapFile.Tile = map.Tile;
            mapFile.Size = ValueParser.IntVec3ToArray(map.Size);

            mapFile.Wealth = (int)map.wealthWatcher.WealthTotal;
            mapFile.WealthExact = map.wealthWatcher.WealthTotal;

            mapFile.GameTicks = Find.TickManager != null ? Find.TickManager.TicksGame : -1;
            mapFile.LastSavedUtcTicks = DateTime.UtcNow.Ticks;

            mapFile.WeatherByte = (byte)DefDatabase<WeatherDef>.AllDefs.FirstIndexOf(fetch => fetch == map.weatherManager.curWeather);

            mapFile.Mods = ModManagerH.GetRunningModList();

            mapFile.SettlementName = GetCommunityNameSafe(map);
            mapFile.FactionName = GetFactionNameSafe();

            (double totalSeconds, double interactingSeconds) = GetRimWorldPlaytimesSafe(map);
            mapFile.RealPlayTimeInteractingSeconds = interactingSeconds;
            mapFile.RealPlayTimeSeconds = totalSeconds;

            GetMapTerrain(mapFile, map);
            GetMapThings(mapFile, map);
            GetMapHumans(mapFile, map);
            GetMapAnimals(mapFile, map);

            return mapFile;
        }

        public static Map StringToMap(MapFile mapFile, bool factionThings, bool nonFactionThings, bool factionHumans, bool nonFactionHumans,
            bool factionAnimals, bool nonFactionAnimals, bool lessLoot = false, bool enforceIDs = false)
        {
            Map map = SetEmptyMap(mapFile, mapFile.Tile);
            if (map == null) return null;

            SetMapTerrain(mapFile, map);

            if (factionThings || nonFactionThings)
                SetMapThings(mapFile, map, factionThings, nonFactionThings, lessLoot, enforceIDs);

            if (factionHumans || nonFactionHumans)
                SetMapHumans(mapFile, map, factionHumans, nonFactionHumans, enforceIDs);

            if (factionAnimals || nonFactionAnimals)
                SetMapAnimals(mapFile, map, factionAnimals, nonFactionAnimals, enforceIDs);

            SetWeather(mapFile, map);
            SetFog(map);
            SetRoofs(map);

            return map;
        }

        private static void GetMapTerrain(MapFile mapFile, Map map)
        {
            try
            {
                for (int z = 0; z < map.Size.z; ++z)
                {
                    for (int x = 0; x < map.Size.x; ++x)
                    {
                        IntVec3 vectorToCheck = new IntVec3(x, map.Size.y, z);

                        MapTile component = new MapTile();

                        TerrainDef terrainDef = map.terrainGrid.TerrainAt(vectorToCheck);
                        if (terrainDef != null)
                            component.TileByte = (byte)DefDatabase<TerrainDef>.AllDefs.FirstIndexOf(fetch => fetch == terrainDef);

                        component.IsPolluted = map.pollutionGrid.IsPolluted(vectorToCheck);

                        RoofDef roofDef = map.roofGrid.RoofAt(vectorToCheck);
                        if (roofDef != null)
                            component.RoofByte = (byte)DefDatabase<RoofDef>.AllDefs.FirstIndexOf(fetch => fetch == roofDef);

                        mapFile.Tiles.Add(component);
                    }
                }
            }
            catch (Exception e)
            {
                Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
            }
        }

        private static void GetMapThings(MapFile mapFile, Map map)
        {
            Thing[] toList = map.listerThings.AllThings
                .Where(fetch => !ScriberH.CheckIfThingIsHuman(fetch) && !ScriberH.CheckIfThingIsAnimal(fetch))
                .ToArray();

            foreach (Thing thing in toList)
            {
                try
                {
                    string data = ScribeManager.SerializeToString(thing, ScribeManager.SerializableType.Thing);

                    if (thing.def.alwaysHaulable)
                        mapFile.FactionThings.Add(data);
                    else
                        mapFile.NonFactionThings.Add(data);
                }
                catch (Exception e)
                {
                    Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                }
            }
        }

        private static void GetMapHumans(MapFile mapFile, Map map)
        {
            Thing[] toList = map.listerThings.AllThings
                .Where(fetch => ScriberH.CheckIfThingIsHuman(fetch))
                .ToArray();

            foreach (Thing thing in toList)
            {
                try
                {
                    string humanData = ScribeManager.SerializeToString(thing as Pawn, ScribeManager.SerializableType.Thing);

                    if (thing.Faction == Faction.OfPlayer)
                        mapFile.FactionHumans.Add(humanData);
                    else
                        mapFile.NonFactionHumans.Add(humanData);
                }
                catch (Exception e)
                {
                    Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                }
            }
        }

        private static void GetMapAnimals(MapFile mapFile, Map map)
        {
            Thing[] toList = map.listerThings.AllThings
                .Where(fetch => ScriberH.CheckIfThingIsAnimal(fetch))
                .ToArray();

            foreach (Thing thing in toList)
            {
                try
                {
                    string animalData = ScribeManager.SerializeToString(thing as Pawn, ScribeManager.SerializableType.Thing);

                    if (thing.Faction == Faction.OfPlayer)
                        mapFile.FactionAnimals.Add(animalData);
                    else
                        mapFile.NonFactionAnimals.Add(animalData);
                }
                catch (Exception e)
                {
                    Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                }
            }
        }

        private static Map SetEmptyMap(MapFile mapFile, int tileToUse)
        {
            try
            {
                PlanetManagerHelper.SetOverrideGenerators();
                Map toReturn = GetOrGenerateMapUtility.GetOrGenerateMap(tileToUse, ValueParser.ArrayToIntVec3(mapFile.Size), null);
                PlanetManagerHelper.SetDefaultGenerators();

                return toReturn;
            }
            catch (Exception e)
            {
                Printer.Error(e.ToString(), LogImportanceMode.Verbose);
                return null;
            }
        }

        private static void SetMapTerrain(MapFile mapFile, Map map)
        {
            int index = 0;

            try
            {
                for (int z = 0; z < map.Size.z; ++z)
                {
                    for (int x = 0; x < map.Size.x; ++x)
                    {
                        MapTile component = mapFile.Tiles[index];
                        IntVec3 vectorToCheck = new IntVec3(x, map.Size.y, z);

                        try
                        {
                            TerrainDef terrainToUse = DefDatabase<TerrainDef>.AllDefs.ToList()[component.TileByte];
                            map.terrainGrid.SetTerrain(vectorToCheck, terrainToUse);
                            map.pollutionGrid.SetPolluted(vectorToCheck, component.IsPolluted);
                        }
                        catch (Exception e)
                        {
                            Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                        }

                        try
                        {
                            RoofDef roofToUse = DefDatabase<RoofDef>.AllDefs.ToList()[component.RoofByte];
                            map.roofGrid.SetRoof(vectorToCheck, roofToUse);
                        }
                        catch (Exception e)
                        {
                            Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                        }

                        index++;
                    }
                }
            }
            catch (Exception e)
            {
                Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
            }
        }

        private static void SetMapThings(MapFile mapFile, Map map, bool factionThings, bool nonFactionThings, bool lessLoot, bool enforceIDs)
        {
            Random rnd = new Random();

            if (factionThings)
            {
                foreach (string str in mapFile.FactionThings)
                {
                    try
                    {
                        if (lessLoot && rnd.Next(1, 100) <= 70)
                            continue;

                        Thing thing = ScribeManager.SerializeFromString<Thing>(str, ScribeManager.SerializableType.Thing, enforceIDs);

                        if (thing.def.CanHaveFaction)
                            thing.SetFaction(SessionHandler.NeutralFaction);

                        RimworldManager.PlaceThingIntoMap(thing, map, thing.Position);
                    }
                    catch (Exception e)
                    {
                        Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                    }
                }
            }

            if (nonFactionThings)
            {
                foreach (string str in mapFile.NonFactionThings)
                {
                    try
                    {
                        Thing thing = ScribeManager.SerializeFromString<Thing>(str, ScribeManager.SerializableType.Thing, enforceIDs);

                        if (thing.def.CanHaveFaction)
                            thing.SetFaction(SessionHandler.NeutralFaction);

                        RimworldManager.PlaceThingIntoMap(thing, map, thing.Position);
                    }
                    catch (Exception e)
                    {
                        Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                    }
                }
            }
        }

        private static void SetMapHumans(MapFile mapFile, Map map, bool factionHumans, bool nonFactionHumans, bool enforceIDs)
        {
            if (factionHumans)
            {
                foreach (string str in mapFile.FactionHumans)
                {
                    try
                    {
                        Pawn pawn = ScribeManager.SerializeFromString<Pawn>(str, ScribeManager.SerializableType.Pawn, enforceIDs);
                        pawn.SetFaction(SessionHandler.NeutralFaction);
                        RimworldManager.PlaceThingIntoMap(pawn, map, pawn.PositionHeld);
                    }
                    catch (Exception e)
                    {
                        Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                    }
                }
            }

            if (nonFactionHumans)
            {
                foreach (string str in mapFile.NonFactionHumans)
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

        private static void SetMapAnimals(MapFile mapFile, Map map, bool factionAnimals, bool nonFactionAnimals, bool enforceIDs)
        {
            if (factionAnimals)
            {
                foreach (string str in mapFile.FactionAnimals)
                {
                    try
                    {
                        Pawn pawn = ScribeManager.SerializeFromString<Pawn>(str, ScribeManager.SerializableType.Pawn, enforceIDs);
                        pawn.SetFaction(SessionHandler.NeutralFaction);
                        RimworldManager.PlaceThingIntoMap(pawn, map, pawn.PositionHeld);
                    }
                    catch (Exception e)
                    {
                        Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
                    }
                }
            }

            if (nonFactionAnimals)
            {
                foreach (string str in mapFile.NonFactionAnimals)
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

        private static void SetWeather(MapFile mapFile, Map map)
        {
            try
            {
                WeatherDef weatherDef = DefDatabase<WeatherDef>.AllDefs.ToList()[mapFile.WeatherByte];
                map.weatherManager.TransitionTo(weatherDef);
            }
            catch (Exception e)
            {
                Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
            }
        }

        private static void SetFog(Map map)
        {
            try
            {
                FloodFillerFog.FloodUnfog(MapGenerator.PlayerStartSpot, map);
            }
            catch (Exception e)
            {
                Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
            }
        }

        private static void SetRoofs(Map map)
        {
            try
            {
                map.roofCollapseBuffer.Clear();
                map.roofGrid.Drawer.SetDirty();
            }
            catch (Exception e)
            {
                Printer.Warning(e.ToString(), LogImportanceMode.Verbose);
            }
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