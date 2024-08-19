using Shared;

namespace GameServer
{
    public static class MapManager
    {
        // Variables
        public readonly static string fileExtension = ".mpmap";
        private static readonly object mapLock = new object();

        public static void SaveUserMap(ServerClient client, Packet packet)
        {
            // Deserialize the map data from the packet
            MapFileData mapFileData = Serializer.ConvertBytesToObject<MapFileData>(packet.Contents);
            if (mapFileData == null)
            {
                Logger.Error("[MapManager] > Failed to deserialize map data.");
                return;
            }
            mapFileData.MapOwner = client.userFile.Username;

            lock (mapLock)
            {
                try
                {
                    // Serialize and save the map data to a file
                    byte[] compressedMapBytes = Serializer.ConvertObjectToBytes(mapFileData);
                    File.WriteAllBytes(Path.Combine(Master.mapsPath, mapFileData.MapTile + fileExtension), compressedMapBytes);
                    Logger.Message($"[Save map] > {client.userFile.Username} > {mapFileData.MapTile}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to save map for tile {mapFileData.MapTile}: {ex.Message}");
                }
            }
        }

        public static void DeleteMap(MapFileData mapFile)
        {
            if (mapFile == null) return;

            lock (mapLock)
            {
                try
                {
                    string filePath = Path.Combine(Master.mapsPath, mapFile.MapTile + fileExtension);
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        Logger.Warning($"[Remove map] > {mapFile.MapTile}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to delete map for tile {mapFile.MapTile}: {ex.Message}");
                }
            }
        }

        public static MapFileData[] GetAllMapFiles()
        {
            var mapDatas = new List<MapFileData>();

            try
            {
                string[] maps = Directory.GetFiles(Master.mapsPath);
                foreach (string map in maps)
                {
                    if (!map.EndsWith(fileExtension)) continue;

                    byte[] decompressedBytes = File.ReadAllBytes(map);
                    var newMap = Serializer.ConvertBytesToObject<MapFileData>(decompressedBytes);
                    if (newMap != null)
                    {
                        mapDatas.Add(newMap);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to load map files: {ex.Message}");
            }

            return mapDatas.ToArray();
        }

        public static bool CheckIfMapExists(int mapTileToCheck)
        {
            return GetAllMapFiles().Any(map => map.MapTile == mapTileToCheck);
        }

        public static MapFileData[] GetAllMapsFromUsername(string Username)
        {
            var userMaps = new List<MapFileData>();

            try
            {
                SettlementFile[] userSettlements = SettlementManager.GetAllSettlementsFromUsername(Username);
                foreach (var settlementFile in userSettlements)
                {
                    var mapFile = GetUserMapFromTile(settlementFile.tile);
                    if (mapFile != null)
                    {
                        userMaps.Add(mapFile);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to retrieve maps for user {Username}: {ex.Message}");
            }

            return userMaps.ToArray();
        }

        public static MapFileData GetUserMapFromTile(int mapTileToGet)
        {
            return GetAllMapFiles().FirstOrDefault(map => map.MapTile == mapTileToGet);
        }

        internal static void DeleteAllMapsFromUsername(string Username)
        {
            try
            {
                var mapsToDelete = GetAllMapsFromUsername(Username);
                foreach (var map in mapsToDelete)
                {
                    DeleteMap(map);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to delete all maps for user {Username}: {ex.Message}");
            }
        }
    }
}