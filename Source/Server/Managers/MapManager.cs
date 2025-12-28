using GameServer.Core;
using GameServer.Misc;
using Shared;
using Shared.Files;
using Shared.Files.Maps;
using System;
using System.IO;
using System.Linq;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static Shared.CommonEnumerators;

namespace GameServer.Managers
{
    public static class MapManager
    {
        [HandlesPacket(PacketHeader.MapManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes, PacketHeader header)
        {
            MapData data = Serializer.ConvertBytesToObject<MapData>(bytes);
            if (data == null) return;

            SaveUserMap(client, data);
        }

        public static void SaveUserMap(ServerClient client, MapData data)
        {
            if (client == null || data == null) return;

            Directory.CreateDirectory(Master.MapsPath);

            int tile = data._mapTile;

            MapFile mapFile = data._mapFile;

            if (!IsMapFileValid(mapFile))
            {
                if (tile < 0 && mapFile != null && mapFile.Tile >= 0) tile = mapFile.Tile;

                if (tile >= 0 && data._rawData != null && data._rawData.Length > 0)
                {
                    try
                    {
                        mapFile = Serializer.ConvertBytesToObject<MapFile>(data._rawData);
                    }
                    catch
                    {
                        mapFile = null;
                    }
                }
            }

            if (mapFile == null) return;

            if (mapFile.Tile < 0 && tile >= 0) mapFile.Tile = tile;
            if (mapFile.Tile < 0) return;

            mapFile.Username = client.UserFile?.Username ?? mapFile.Username ?? string.Empty;

            if (mapFile.LastSavedUtcTicks <= 0)
                mapFile.LastSavedUtcTicks = DateTime.UtcNow.Ticks;

            SaveUserMap(client, mapFile);
        }

        public static void SaveUserMap(ServerClient client, MapFile file)
        {
            if (client == null || file == null) return;
            if (file.Tile < 0) return;

            Directory.CreateDirectory(Master.MapsPath);

            try
            {
                string mapPath = Path.Combine(Master.MapsPath, file.Tile + CommonValues.DefaultSaveFormat);
                Serializer.ObjectBytesToFile(mapPath, file);
            }
            catch
            {
            }

            TryWriteStatsSnapshot(file);

            InformationDisplayer.DisplaySaveMap(client);
        }

        public static void DeleteMapByTile(int tile)
        {
            if (tile < 0) return;

            try
            {
                string mapPath = Path.Combine(Master.MapsPath, tile + CommonValues.DefaultSaveFormat);
                if (File.Exists(mapPath)) File.Delete(mapPath);
            }
            catch { }

            try
            {
                string statsPath = GetStatsPathForTile(tile);
                if (File.Exists(statsPath)) File.Delete(statsPath);
            }
            catch { }

            try
            {
                LeaderboardManager.RemoveTile(tile);
            }
            catch { }

            InformationDisplayer.DisplayRemoveMap(tile.ToString());
        }

        public static string[] GetAllMaps()
        {
            if (!Directory.Exists(Master.MapsPath)) return Array.Empty<string>();
            return Directory.GetFiles(Master.MapsPath);
        }

        public static bool CheckIfMapExists(int mapTileToCheck)
        {
            string toFind = GetAllMaps().FirstOrDefault(fetch => Path.GetFileNameWithoutExtension(fetch) == mapTileToCheck.ToString());
            return toFind != null;
        }

        public static byte[] GetMapBytesFromTile(int mapTileToGet)
        {
            string path = Path.Combine(Master.MapsPath, mapTileToGet + CommonValues.DefaultSaveFormat);
            if (!File.Exists(path)) return null;

            try { return File.ReadAllBytes(path); }
            catch { return null; }
        }

        public static MapFile GetMapFromTile(int mapTileToGet)
        {
            string path = Path.Combine(Master.MapsPath, mapTileToGet + CommonValues.DefaultSaveFormat);
            if (!File.Exists(path)) return null;

            try
            {
                return Serializer.FileBytesToObject<MapFile>(path);
            }
            catch
            {
                try
                {
                    byte[] raw = File.ReadAllBytes(path);
                    if (raw == null || raw.Length == 0) return null;
                    return Serializer.ConvertBytesToObject<MapFile>(raw);
                }
                catch
                {
                    return null;
                }
            }
        }

        public static MapStatsFile GetOrCreateMapStatsFromTile(int mapTileToGet)
        {
            string statsPath = GetStatsPathForTile(mapTileToGet);

            try
            {
                if (File.Exists(statsPath))
                    return Serializer.FileBytesToObject<MapStatsFile>(statsPath);
            }
            catch
            {
            }

            MapFile map = GetMapFromTile(mapTileToGet);
            if (map == null) return null;

            long savedTicks = map.LastSavedUtcTicks;
            if (savedTicks <= 0)
            {
                try
                {
                    string mapPath = Path.Combine(Master.MapsPath, mapTileToGet + CommonValues.DefaultSaveFormat);
                    savedTicks = File.Exists(mapPath) ? File.GetLastWriteTimeUtc(mapPath).Ticks : DateTime.UtcNow.Ticks;
                }
                catch { savedTicks = DateTime.UtcNow.Ticks; }
            }

            MapStatsFile stats = BuildStatsFromMapFile(map, mapTileToGet, savedTicks);
            TryWriteStatsSnapshot(stats);

            return stats;
        }

        private static void TryWriteStatsSnapshot(MapFile mapFile)
        {
            try
            {
                if (mapFile == null) return;

                long savedTicks = mapFile.LastSavedUtcTicks > 0 ? mapFile.LastSavedUtcTicks : DateTime.UtcNow.Ticks;

                MapStatsFile stats = BuildStatsFromMapFile(mapFile, mapFile.Tile, savedTicks);
                TryWriteStatsSnapshot(stats);
            }
            catch
            {
            }
        }

        private static void TryWriteStatsSnapshot(MapStatsFile stats)
        {
            try
            {
                if (stats == null || stats.Tile < 0) return;

                if (stats.LastSavedUtcTicks <= 0)
                    stats.LastSavedUtcTicks = DateTime.UtcNow.Ticks;

                string statsPath = GetStatsPathForTile(stats.Tile);
                Serializer.ObjectBytesToFile(statsPath, stats);

                LeaderboardManager.UpsertFromStats(stats);
            }
            catch
            {
            }
        }

        private static MapStatsFile BuildStatsFromMapFile(MapFile mapFile, int tileFallback, long savedTicksFallback)
        {
            MapStatsFile stats = new MapStatsFile();

            int tile = mapFile.Tile >= 0 ? mapFile.Tile : tileFallback;
            stats.Tile = tile;

            stats.Username = mapFile.Username ?? string.Empty;
            stats.SettlementName = mapFile.SettlementName ?? string.Empty;
            stats.FactionName = mapFile.FactionName ?? string.Empty;

            stats.Wealth = mapFile.Wealth;
            stats.WealthExact = mapFile.WealthExact >= 0 ? mapFile.WealthExact : -1;

            stats.GameTicks = mapFile.GameTicks;
            stats.RealPlayTimeInteractingSeconds = mapFile.RealPlayTimeInteractingSeconds >= 0 ? mapFile.RealPlayTimeInteractingSeconds : -1;

            stats.LastSavedUtcTicks = savedTicksFallback > 0 ? savedTicksFallback : DateTime.UtcNow.Ticks;

            stats.FactionThingCount = mapFile.FactionThings != null ? mapFile.FactionThings.Count : -1;
            stats.NonFactionThingCount = mapFile.NonFactionThings != null ? mapFile.NonFactionThings.Count : -1;

            stats.FactionHumanCount = mapFile.FactionHumans != null ? mapFile.FactionHumans.Count : -1;
            stats.NonFactionHumanCount = mapFile.NonFactionHumans != null ? mapFile.NonFactionHumans.Count : -1;

            stats.FactionAnimalCount = mapFile.FactionAnimals != null ? mapFile.FactionAnimals.Count : -1;
            stats.NonFactionAnimalCount = mapFile.NonFactionAnimals != null ? mapFile.NonFactionAnimals.Count : -1;

            stats.ColonistCount = stats.FactionHumanCount;

            TryBackfillFromSettlement(tile, stats);

            return stats;
        }

        private static void TryBackfillFromSettlement(int tile, MapStatsFile stats)
        {
            try
            {
                SettlementFile sf = SettlementManager.GetSettlementFileFromTile(tile);
                if (sf == null) return;

                if (string.IsNullOrWhiteSpace(stats.Username))
                    stats.Username = sf.Username ?? string.Empty;

                if (string.IsNullOrWhiteSpace(stats.SettlementName) && !string.IsNullOrWhiteSpace(sf.Name))
                    stats.SettlementName = sf.Name;
            }
            catch
            {
            }
        }

        private static string GetStatsPathForTile(int tile)
        {
            return Path.Combine(Master.MapsPath, $"{tile}.stats{CommonValues.DefaultSaveFormat}");
        }

        private static bool IsMapFileValid(MapFile file)
        {
            if (file == null) return false;
            if (file.Tile >= 0) return true;

            if (file.Size != null && file.Size.Length > 0) return true;
            if (file.Wealth >= 0) return true;
            if (file.WealthExact >= 0) return true;
            if (file.GameTicks >= 0) return true;

            return false;
        }
    }
}