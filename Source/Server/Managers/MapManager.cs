using GameServer.Core;
using GameServer.Misc;
using Shared;
using static Shared.CommonEnumerators;
using Shared.Files;
using TCPNetwork.Packets;
using TCPNetwork.Files.Client;
using System;
using System.IO;
using System.Linq;

namespace GameServer.Managers
{
    public static class MapManager
    {
        private const double MaxDeltaSecondsToCount = 10800d;

        [HandlesPacket(PacketHeader.MapManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes, PacketHeader header)
        {
            MapData data = Serializer.ConvertBytesToObject<MapData>(bytes);

            SaveUserMap(client, data._mapFile);
        }

        public static void SaveUserMap(ServerClient client, MapFile file)
        {
            file.Username = client.UserFile.Username;
            string mapPath = Path.Combine(Master.MapsPath, file.Tile + CommonValues.DefaultSaveFormat);
            Serializer.ObjectBytesToFile(mapPath, file);

            TryWriteStatsSnapshot(file);

            InformationDisplayer.DisplaySaveMap(client);
        }

        public static void DeleteMap(MapFile mapFile)
        {
            File.Delete(Path.Combine(Master.MapsPath, mapFile.Tile + CommonValues.DefaultSaveFormat));

            string statsPath = GetStatsPathForTile(mapFile.Tile);
            if (File.Exists(statsPath)) File.Delete(statsPath);

            InformationDisplayer.DisplayRemoveMap(mapFile.Tile.ToString());
        }

        public static string[] GetAllMaps()
        {
            return Directory.GetFiles(Master.MapsPath);
        }

        public static bool CheckIfMapExists(int mapTileToCheck)
        {
            string toFind = GetAllMaps().FirstOrDefault(fetch => Path.GetFileNameWithoutExtension(fetch) == mapTileToCheck.ToString());
            if (toFind != null) return true;
            else return false;
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

            MapStatsFile stats = BuildStatsFromMapFile(map, previous: null);
            TryWriteStatsSnapshot(stats);

            return stats;
        }

        private static void TryWriteStatsSnapshot(MapFile mapFile)
        {
            try
            {
                MapStatsFile previous = null;

                try
                {
                    string statsPath = GetStatsPathForTile(mapFile.Tile);
                    if (File.Exists(statsPath))
                        previous = Serializer.FileBytesToObject<MapStatsFile>(statsPath);
                }
                catch
                {
                    previous = null;
                }

                MapStatsFile stats = BuildStatsFromMapFile(mapFile, previous);
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
            }
            catch
            {
            }
        }

        private static MapStatsFile BuildStatsFromMapFile(MapFile mapFile, MapStatsFile previous)
        {
            MapStatsFile stats = new MapStatsFile();

            stats.Tile = mapFile.Tile;

            stats.Username = mapFile.Username ?? string.Empty;
            if (string.IsNullOrWhiteSpace(stats.Username) && previous != null && !string.IsNullOrWhiteSpace(previous.Username))
                stats.Username = previous.Username;

            stats.SettlementName = mapFile.SettlementName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(stats.SettlementName) && previous != null && !string.IsNullOrWhiteSpace(previous.SettlementName))
                stats.SettlementName = previous.SettlementName;

            if (string.IsNullOrWhiteSpace(stats.SettlementName))
            {
                try
                {
                    SettlementFile s = SettlementManager.GetSettlementFileFromTile(mapFile.Tile);
                    if (s != null && !string.IsNullOrWhiteSpace(s.Name))
                        stats.SettlementName = s.Name;
                    if (s != null && string.IsNullOrWhiteSpace(stats.Username) && !string.IsNullOrWhiteSpace(s.Username))
                        stats.Username = s.Username;
                }
                catch
                {
                }
            }

            stats.FactionName = mapFile.FactionName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(stats.FactionName) && previous != null && !string.IsNullOrWhiteSpace(previous.FactionName))
                stats.FactionName = previous.FactionName;

            stats.Wealth = mapFile.Wealth;
            stats.WealthExact = mapFile.WealthExact >= 0 ? mapFile.WealthExact : -1;

            stats.GameTicks = mapFile.GameTicks;

            long newLastSaved = mapFile.LastSavedUtcTicks > 0 ? mapFile.LastSavedUtcTicks : DateTime.UtcNow.Ticks;
            stats.LastSavedUtcTicks = newLastSaved;

            double playtime = -1;

            if (mapFile.RealPlayTimeInteractingSeconds >= 0)
            {
                playtime = mapFile.RealPlayTimeInteractingSeconds;

                if (previous != null && previous.RealPlayTimeInteractingSeconds >= 0)
                    playtime = Math.Max(previous.RealPlayTimeInteractingSeconds, playtime);
            }
            else
            {
                if (previous != null && previous.RealPlayTimeInteractingSeconds >= 0)
                    playtime = previous.RealPlayTimeInteractingSeconds;
                else
                    playtime = 0;

                if (previous != null && previous.LastSavedUtcTicks > 0)
                {
                    double delta = (newLastSaved - previous.LastSavedUtcTicks) / (double)TimeSpan.TicksPerSecond;

                    if (delta < 0) delta = 0;
                    if (delta > MaxDeltaSecondsToCount) delta = 0;

                    playtime += delta;
                }
            }

            stats.RealPlayTimeInteractingSeconds = playtime;

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