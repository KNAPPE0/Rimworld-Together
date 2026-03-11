using GameClient.Hooks.TCPNetwork;
using GameClient.Misc;
using Shared;
using Shared.Files.Maps;
using GameClient.Hooks.TCPNetwork;
using Shared.Misc;
using TCPNetwork;
using TCPNetwork.Packets;
using Verse;
using static Shared.CommonEnumerators;

namespace GameClient.Managers
{
    public static class MapManager
    {
        public static void SendPlayerMapsToServer()
        {
            Printer.Message("Sending maps to server", LogImportanceMode.Verbose);

            foreach (Map map in Find.Maps.ToArray())
            {
                if (map != null && map.IsPlayerHome)
                {
                    SendMapToServer(map);
                }
            }
        }

        public static void SendMapToServer(Map map)
        {
            if (map == null) return;

            MapFile mapFile = MapSaveLoader.MapToString(map);
            if (mapFile == null) return;

            PKT_Map mapData = new PKT_Map();
            mapData._mapTile = mapFile.Tile;
            mapData._mapFile = mapFile;
            mapData._rawData = Serializer.ConvertObjectToBytes(mapFile);

            Network.ServerEndpoint.EnqueuePacket(PacketHeader.MapManager, mapData);
        }
    }
}