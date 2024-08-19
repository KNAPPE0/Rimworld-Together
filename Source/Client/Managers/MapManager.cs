using Shared;
using Verse;
using System.Linq;

namespace GameClient
{
    // Class that handles map functions for the mod to use
    public static class MapManager
    {
        // Sends all the player maps to the server
        public static void SendPlayerMapsToServer()
        {
            foreach (var map in Find.Maps.ToArray())
            {
                if (map.IsPlayerHome)
                {
                    SendMapToServerSingle(map);
                }
            }
        }

        // Sends a desired map to the server
        private static void SendMapToServerSingle(Map map)
        {
            var mapData = ParseMap(map, true, true, true, true);
            if (mapData == null)
            {
                Logger.Error("Failed to parse map data.");
                return;
            }

            var mapFileData = new MapFileData
            {
                MapTile = mapData.MapTile,
                MapData = mapData
            };

            var packet = Packet.CreatePacketFromObject(nameof(PacketHandler.MapPacket), mapFileData);
            Network.Listener.EnqueuePacket(packet);
        }

        // Parses a desired map into a usable mod class
        public static MapData? ParseMap(Map map, bool includeThings, bool includeHumans, bool includeAnimals, bool includeMods)
        {
            var mapData = MapScribeManager.MapToString(map, includeThings, includeThings, includeHumans, includeHumans, includeAnimals, includeAnimals);

            if (mapData == null)
            {
                Logger.Error("Failed to convert map to string.");
                return null;
            }

            if (includeMods)
            {
                mapData.MapMods = ModManager.GetRunningModList();
            }

            return mapData;
        }
    }
}