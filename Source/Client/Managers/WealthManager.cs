// File: WealthManager.cs  (Client File)
using System;
using GameClient.TCP;
using Shared;
using Shared.Packets.Data;
using Verse;
using RimWorld;
using GameClient.Values;
using static Shared.CommonEnumerators;

namespace GameClient.Managers
{
    public static class WealthManager
    {
        /// <summary>
        /// Send one map’s wealth (if it’s a player home) to the server.
        /// </summary>
        public static void Send(Map map)
        {
            if (map == null) return;

            var currentWealth = map.wealthWatcher.WealthTotal;
            var uid = ClientValues.Username ?? string.Empty;

            var packet = new WealthData
            {
                _uid    = uid,
                _wealth = currentWealth
            };
            Network.Listener.EnqueuePacket(PacketHeader.WealthManager, packet);
        }

        /// <summary>
        /// Call this to broadcast all home maps’ wealth. Typically before sending a save.
        /// </summary>
        public static void SendCurrentWealth()
        {
            foreach (var map in Find.Maps)
            {
                if (map.IsPlayerHome)
                    Send(map);
            }
        }
    }
}