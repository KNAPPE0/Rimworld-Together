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

            try
            {
                File.WriteAllBytes(Path.Combine(Master.MapsPath, data._mapTile + CommonValues.DefaultSaveFormat), data._rawData);
            }
            catch
            {
            }

            TryWriteStatsSnapshotFromRaw(client, data);

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
            if (File.Exists(path)) return File.ReadAllBytes(path);
            return null;
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

            MapStatsFile stats = BuildStatsFromMapFile(map);
            TryWriteStatsSnapshot(stats);

            return stats;
        }

        private static void TryWriteStatsSnapshotFromRaw(ServerClient client, MapData data)
        {
            try
            {
                if (data == null || data._rawData == null || data._rawData.Length == 0) return;

                MapFile map = Serializer.ConvertBytesToObject<MapFile>(data._rawData);
                if (map == null) return;

                map.Tile = data._mapTile;
                map.Username = client?.UserFile?.Username ?? map.Username ?? string.Empty;

                TryWriteStatsSnapshot(map);
            }
            catch
            {
            }
        }

        private static void TryWriteStatsSnapshot(MapFile mapFile)
        {
            try
            {
                MapStatsFile stats = BuildStatsFromMapFile(mapFile);
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

        private static MapStatsFile BuildStatsFromMapFile(MapFile mapFile)
        {
            MapStatsFile stats = new MapStatsFile();

            stats.Tile = mapFile.Tile;
            stats.Username = mapFile.Username ?? string.Empty;

            stats.SettlementName = mapFile.SettlementName ?? string.Empty;
            stats.FactionName = mapFile.FactionName ?? string.Empty;

            stats.Wealth = mapFile.Wealth;
            stats.WealthExact = mapFile.WealthExact >= 0 ? mapFile.WealthExact : -1;

            stats.GameTicks = mapFile.GameTicks;
            stats.RealPlayTimeInteractingSeconds = mapFile.RealPlayTimeInteractingSeconds >= 0 ? mapFile.RealPlayTimeInteractingSeconds : -1;

            stats.LastSavedUtcTicks = mapFile.LastSavedUtcTicks > 0 ? mapFile.LastSavedUtcTicks : DateTime.UtcNow.Ticks;

            stats.FactionThingCount = mapFile.FactionThings != null ? mapFile.FactionThings.Length : -1;
            stats.NonFactionThingCount = mapFile.NonFactionThings != null ? mapFile.NonFactionThings.Length : -1;

            stats.FactionHumanCount = mapFile.FactionHumans != null ? mapFile.FactionHumans.Length : -1;
            stats.NonFactionHumanCount = mapFile.NonFactionHumans != null ? mapFile.NonFactionHumans.Length : -1;

            stats.FactionAnimalCount = mapFile.FactionAnimals != null ? mapFile.FactionAnimals.Length : -1;
            stats.NonFactionAnimalCount = mapFile.NonFactionAnimals.Length != null ? mapFile.NonFactionAnimals.Length : -1;

            stats.ColonistCount = stats.FactionHumanCount;

            return stats;
        }

        private static string GetStatsPathForTile(int tile)
        {
            return Path.Combine(Master.MapsPath, $"{tile}.stats{CommonValues.DefaultSaveFormat}");
        }
    }
}