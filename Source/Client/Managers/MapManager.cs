using GameClient.Misc;
using Shared;
using Shared.Files.Maps;
using Shared.Misc;
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

            MapFile mapFile = MapSaveLoader.MapToString(map, true, true, true, true, true, true);
            if (mapFile == null) return;

            MapData mapData = new MapData();
            mapData._mapTile = mapFile.Tile;
            mapData._mapFile = mapFile;

            mapData._rawData = Serializer.ConvertObjectToBytes(mapFile);

            ClientNetwork.Instance.ClientListener.EnqueuePacket(PacketHeader.MapManager, mapData);
        }
    }
}