using GameServer.Core;
using GameServer.Misc;
using Shared;
using Shared.Files;
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
            SaveUserMap(client, data._mapFile);
        }

        public static void SaveUserMap(ServerClient client, MapFile file)
        {
            file.Username = client.UserFile.Username;

            Directory.CreateDirectory(Master.MapsPath);

            string mapPath = Path.Combine(Master.MapsPath, file.Tile + CommonValues.DefaultSaveFormat);
            Serializer.ObjectBytesToFile(mapPath, file);

            TryWriteStatsSnapshot(file);

            InformationDisplayer.DisplaySaveMap(client);
        }

        public static void DeleteMap(MapFile mapFile)
        {
            if (mapFile == null) return;
            DeleteMapByTile(mapFile.Tile);
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

        public static MapFile GetMapFromTile(int mapTileToGet)
        {
            string path = Path.Combine(Master.MapsPath, mapTileToGet + CommonValues.DefaultSaveFormat);
            if (File.Exists(path)) return Serializer.FileBytesToObject<MapFile>(path);
            else return null;
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
            stats.NonFactionAnimalCount = mapFile.NonFactionAnimals != null ? mapFile.NonFactionAnimals.Length : -1;

            stats.ColonistCount = stats.FactionHumanCount;

            return stats;
        }

        private static string GetStatsPathForTile(int tile)
        {
            return Path.Combine(Master.MapsPath, $"{tile}.stats{CommonValues.DefaultSaveFormat}");
        }
    }
}