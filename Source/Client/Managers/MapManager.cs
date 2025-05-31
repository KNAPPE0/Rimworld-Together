﻿using GameClient.Misc;
using GameClient.TCP;
using Shared;
using Verse;
using RimWorld;
using Shared.Packets.Data;

namespace GameClient.Managers
{
    public static class MapManager
    {
        public static void SendPlayerMapsToServer()
        {
            foreach (Map map in Find.Maps.ToArray())
            {
                if (map.IsPlayerHome)
                {
                    SendMapToServer(map);
                }
            }
        }

        public static void SendMapToServer(Map map)
        {
            if (map == null) return;

            MapFile mapFile = MapSaveLoader.MapToString(
                map,
                factionThings:     true,
                nonFactionThings:  true,
                factionHumans:     true,
                nonFactionHumans:  true,
                factionAnimals:    true,
                nonFactionAnimals: true);

            WealthManager.Send(map);

            var mapData = new MapData
            {
                _mapFile = mapFile
            };

            Network.Listener.EnqueuePacket(PacketHeader.MapManager, mapData);
        }
    }
}