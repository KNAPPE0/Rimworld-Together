using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameClient.Managers;
using RimWorld;
using Shared;
using Shared.Files;
using Shared.Misc;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Misc
{
    public static class MapSaveLoader
    {
        public static MapFile MapToString(Map map, bool factionThings, bool nonFactionThings, bool factionHumans, bool nonFactionHumans,
            bool factionAnimals, bool nonFactionAnimals)
        {
            MapFile mapFile = new MapFile();

            mapFile.Tile = map.Tile;

            mapFile.Size = ValueParser.IntVec3ToArray(map.Size);

            mapFile.Wealth = (int)map.wealthWatcher.WealthTotal;
            mapFile.WealthExact = map.wealthWatcher.WealthTotal;

            mapFile.SettlementName = GetSettlementName(map);
            mapFile.FactionName = GetFactionName(map);

            mapFile.GameTicks = Find.TickManager != null ? Find.TickManager.TicksGame : -1;
            mapFile.RealPlayTimeInteractingSeconds = GetRealPlayTimeInteractingSeconds();
            mapFile.LastSavedUtcTicks = DateTime.UtcNow.Ticks;

            mapFile.CurWeatherDefName = map.weatherManager.curWeather.defName;

            GetMapTerrain(mapFile, map);

            GetMapThings(mapFile, map, factionThings, nonFactionThings);

            GetMapHumans(mapFile, map, factionHumans, nonFactionHumans);

            GetMapAnimals(mapFile, map, factionAnimals, nonFactionAnimals);

            GetMapMods(mapFile);

            return mapFile;
        }

        public static Map StringToMap(MapFile mapFile, bool factionThings, bool nonFactionThings, bool factionHumans, bool nonFactionHumans,
            bool factionAnimals, bool nonFactionAnimals, bool lessLoot = false)
        {
            Map map = SetEmptyMap(mapFile, SessionHandler.ChosenSettlement.Tile);

            SetMapTerrain(mapFile, map);

            if (factionThings || nonFactionThings) SetMapThings(mapFile, map, factionThings, nonFactionThings, lessLoot);

            if (factionHumans || nonFactionHumans) SetMapHumans(mapFile, map, factionHumans, nonFactionHumans);

            if (factionAnimals || nonFactionAnimals) SetMapAnimals(mapFile, map, factionAnimals, nonFactionAnimals);

            SetWeatherData(mapFile, map);

            SetMapFog(map);

            SetMapRoofs(map);

            return map;
        }

        //Getters

        private static void GetMapTerrain(MapFile mapFile, Map map)
        {
            try
            {
                List<MapTileDetail> toGet = new List<MapTileDetail>();

                for (int z = 0; z < map.Size.z; ++z)
                {
                    for (int x = 0; x < map.Size.x; ++x)
                    {
                        MapTileDetail component = new MapTileDetail();
                        IntVec3 vectorToCheck = new IntVec3(x, map.Size.y, z);
                        component.DefName = map.terrainGrid.TerrainAt(vectorToCheck).defName;
                        component.IsPolluted = map.pollutionGrid.IsPolluted(vectorToCheck);

                        if (map.roofGrid.RoofAt(vectorToCheck) == null) component.RoofDefName = "null";
                        else component.RoofDefName = map.roofGrid.RoofAt(vectorToCheck).defName;

                        toGet.Add(component);
                    }
                }

                mapFile.Tiles = toGet.ToArray();
            }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
        }

        private static void GetMapThings(MapFile mapFile, Map map, bool factionThings, bool nonFactionThings)
        {
            try
            {
                List<string> tempFactionThings = new List<string>();
                List<string> tempNonFactionThings = new List<string>();

                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (!ScriberH.CheckIfThingIsHuman(thing) && !ScriberH.CheckIfThingIsAnimal(thing))
                    {
                        string data = ScribeManager.SerializeToString(thing, ScribeManager.SerializableType.Thing, thing.stackCount);

                        if (thing.def.alwaysHaulable && factionThings) tempFactionThings.Add(data);
                        else if (!thing.def.alwaysHaulable && nonFactionThings) tempNonFactionThings.Add(data);
                    }
                }

                mapFile.FactionThings = tempFactionThings.ToArray();
                mapFile.NonFactionThings = tempNonFactionThings.ToArray();
            }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
        }

        private static void GetMapHumans(MapFile mapFile, Map map, bool factionHumans, bool nonFactionHumans)
        {
            try
            {
                List<HumanFile> tempFactionHumans = new List<HumanFile>();
                List<HumanFile> tempNonFactionHumans = new List<HumanFile>();

                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (ScriberH.CheckIfThingIsHuman(thing))
                    {
                        HumanFile humanData = ScribeManager.HumanToString(thing as Pawn);

                        if (thing.Faction == Faction.OfPlayer && factionHumans) tempFactionHumans.Add(humanData);
                        else if (thing.Faction != Faction.OfPlayer && nonFactionHumans) tempNonFactionHumans.Add(humanData);
                    }
                }

                mapFile.FactionHumans = tempFactionHumans.ToArray();
                mapFile.NonFactionHumans = tempNonFactionHumans.ToArray();
            }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
        }

        private static void GetMapAnimals(MapFile mapFile, Map map, bool factionAnimals, bool nonFactionAnimals)
        {
            try
            {
                List<string> tempFactionAnimals = new List<string>();
                List<string> tempNonFactionAnimals = new List<string>();

                foreach (Thing thing in map.listerThings.AllThings)
                {
                    if (ScriberH.CheckIfThingIsAnimal(thing))
                    {
                        string animalData = ScribeManager.SerializeToString(thing as Pawn, ScribeManager.SerializableType.Thing);

                        if (thing.Faction == Faction.OfPlayer && factionAnimals) tempFactionAnimals.Add(animalData);
                        else if (thing.Faction != Faction.OfPlayer && nonFactionAnimals) tempNonFactionAnimals.Add(animalData);
                    }
                }

                mapFile.FactionAnimals = tempFactionAnimals.ToArray();
                mapFile.NonFactionAnimals = tempNonFactionAnimals.ToArray();
            }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
        }

        private static void GetMapMods(MapFile mapFile)
        {
            try { mapFile.Mods = ModManagerH.GetRunningModList(); }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
        }


        private static string GetSettlementName(Map map)
        {
            try
            {
                object mapInfo = GetMemberValue(map, "Info", "info");
                if (mapInfo == null) return string.Empty;

                object parent = GetMemberValue(mapInfo, "parent", "Parent");
                if (parent == null) return string.Empty;

                object label = GetMemberValue(parent, "Label", "label");
                return label as string ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string GetFactionName(Map map)
        {
            try
            {
                object mapInfo = GetMemberValue(map, "Info", "info");
                if (mapInfo == null) return Faction.OfPlayer != null ? (Faction.OfPlayer.Name ?? string.Empty) : string.Empty;

                object parent = GetMemberValue(mapInfo, "parent", "Parent");
                if (parent == null) return Faction.OfPlayer != null ? (Faction.OfPlayer.Name ?? string.Empty) : string.Empty;

                object factionObj = GetMemberValue(parent, "Faction", "faction");
                if (factionObj == null) return Faction.OfPlayer != null ? (Faction.OfPlayer.Name ?? string.Empty) : string.Empty;

                object nameObj = GetMemberValue(factionObj, "Name", "name");
                string name = nameObj as string;

                if (string.IsNullOrWhiteSpace(name) && Faction.OfPlayer != null)
                    name = Faction.OfPlayer.Name;

                return name ?? string.Empty;
            }
            catch
            {
                return Faction.OfPlayer != null ? (Faction.OfPlayer.Name ?? string.Empty) : string.Empty;
            }
        }

        private static double GetRealPlayTimeInteractingSeconds()
        {
            try
            {
                if (Current.Game == null) return -1;

                object gameInfo = GetMemberValue(Current.Game, "Info", "info");
                if (gameInfo == null) return -1;

                object v = GetMemberValue(gameInfo, "RealPlayTimeInteracting", "realPlayTimeInteracting");
                if (v == null) return -1;

                if (v is float f) return f;
                if (v is double d) return d;

                return -1;
            }
            catch
            {
                return -1;
            }
        }

        private static object GetMemberValue(object obj, params string[] names)
        {
            if (obj == null) return null;

            Type t = obj.GetType();

            foreach (string n in names)
            {
                try
                {
                    PropertyInfo p = t.GetProperty(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p != null) return p.GetValue(obj);

                    FieldInfo f = t.GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (f != null) return f.GetValue(obj);
                }
                catch
                {
                }
            }

            return null;
        }

        //Setters

        private static Map SetEmptyMap(MapFile mapFile, int tileToUse)
        {
            Map toReturn = null;

            try
            {
                IntVec3 mapSize = ValueParser.ArrayToIntVec3(mapFile.Size);

                PlanetManagerHelper.SetOverrideGenerators();
                toReturn = GetOrGenerateMapUtility.GetOrGenerateMap(tileToUse, mapSize, null);
                PlanetManagerHelper.SetDefaultGenerators();

                return toReturn;
            }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }

            return toReturn;
        }

        private static void SetMapTerrain(MapFile mapFile, Map map)
        {
            try
            {
                int index = 0;

                for (int z = 0; z < map.Size.z; ++z)
                {
                    for (int x = 0; x < map.Size.x; ++x)
                    {
                        MapTileDetail component = mapFile.Tiles[index];
                        IntVec3 vectorToCheck = new IntVec3(x, map.Size.y, z);

                        try
                        {
                            TerrainDef terrainToUse = DefDatabase<TerrainDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == component.DefName);
                            map.terrainGrid.SetTerrain(vectorToCheck, terrainToUse);
                            map.pollutionGrid.SetPolluted(vectorToCheck, component.IsPolluted);
                        }
                        catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }

                        try
                        {
                            RoofDef roofToUse = DefDatabase<RoofDef>.AllDefs.FirstOrDefault(fetch => fetch.defName == component.RoofDefName);
                            map.roofGrid.SetRoof(vectorToCheck, roofToUse);
                        }
                        catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }

                        index++;
                    }
                }
            }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
        }

        private static void SetMapThings(MapFile mapFile, Map map, bool factionThings, bool nonFactionThings, bool lessLoot)
        {
            try
            {
                List<Thing> thingsToGetInThisTile = new List<Thing>();

                if (factionThings)
                {
                    Random rnd = new Random();

                    foreach (string item in mapFile.FactionThings)
                    {
                        try
                        {
                            Thing toGet = (Thing)ScribeManager.SerializeFromString<Thing>(item);

                            if (lessLoot)
                            {
                                if (rnd.Next(1, 100) > 70) thingsToGetInThisTile.Add(toGet);
                                else continue;
                            }
                            else thingsToGetInThisTile.Add(toGet);
                        }
                        catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
                    }
                }

                if (nonFactionThings)
                {
                    foreach (string item in mapFile.NonFactionThings)
                    {
                        try
                        {
                            Thing toGet = (Thing)ScribeManager.SerializeFromString<Thing>(item);
                            thingsToGetInThisTile.Add(toGet);
                        }
                        catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
                    }
                }

                foreach (Thing thing in thingsToGetInThisTile)
                {
                    try
                    {
                        if (thing.def.CanHaveFaction) thing.SetFaction(SessionHandler.NeutralFaction);
                        GenPlace.TryPlaceThing(thing, thing.Position, map, ThingPlaceMode.Direct, rot: thing.Rotation);
                    }
                    catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
                }
            }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
        }

        private static void SetMapHumans(MapFile mapFile, Map map, bool factionHumans, bool nonFactionHumans)
        {
            try
            {
                if (factionHumans)
                {
                    foreach (HumanFile pawn in mapFile.FactionHumans)
                    {
                        try
                        {
                            Pawn human = ScribeManager.StringtoHuman(pawn);
                            human.SetFaction(SessionHandler.NeutralFaction);

                            GenSpawn.Spawn(human, human.Position, map, human.Rotation);
                        }
                        catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
                    }
                }

                if (nonFactionHumans)
                {
                    foreach (HumanFile pawn in mapFile.NonFactionHumans)
                    {
                        try
                        {
                            Pawn human = ScribeManager.StringtoHuman(pawn);
                            GenSpawn.Spawn(human, human.Position, map, human.Rotation);
                        }
                        catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
                    }
                }
            }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
        }

        private static void SetMapAnimals(MapFile mapFile, Map map, bool factionAnimals, bool nonFactionAnimals)
        {
            try
            {
                if (factionAnimals)
                {
                    foreach (string pawn in mapFile.FactionAnimals)
                    {
                        try
                        {
                            Pawn animal = (Pawn)ScribeManager.SerializeFromString<Pawn>(pawn);
                            animal.SetFaction(SessionHandler.NeutralFaction);

                            GenSpawn.Spawn(animal, animal.Position, map, animal.Rotation);
                        }
                        catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
                    }
                }

                if (nonFactionAnimals)
                {
                    foreach (string pawn in mapFile.NonFactionAnimals)
                    {
                        try
                        {
                            Pawn animal = (Pawn)ScribeManager.SerializeFromString<Pawn>(pawn);
                            GenSpawn.Spawn(animal, animal.Position, map, animal.Rotation);
                        }
                        catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
                    }
                }
            }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
        }

        private static void SetWeatherData(MapFile mapFile, Map map)
        {
            try
            {
                WeatherDef weatherDef = DefDatabase<WeatherDef>.AllDefs.First(fetch => fetch.defName == mapFile.CurWeatherDefName);
                map.weatherManager.TransitionTo(weatherDef);
            }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
        }

        private static void SetMapFog(Map map)
        {
            try { FloodFillerFog.FloodUnfog(MapGenerator.PlayerStartSpot, map); }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
        }

        private static void SetMapRoofs(Map map)
        {
            try
            {
                map.roofCollapseBuffer.Clear();
                map.roofGrid.Drawer.SetDirty();
            }
            catch (Exception e) { Printer.Warning(e.ToString(), LogImportanceMode.Verbose); }
        }
    }
}
