// File: StatisticalManager.cs  (Client File)
using System;
using GameClient.Values;
using GameClient.TCP;
using RimWorld;
using Shared;
using Shared.Packets.Data;
using Verse;

namespace GameClient.Managers
{
    public static class StatisticalManager
    {
        /// <summary>
        /// Gathers in-game statistics (wealth, colonists, playtime, days passed, etc.)
        /// and sends them to the server as a single packet.
        /// Call this just before (or right after) SaveManager.SendSaveToServer().
        /// </summary>
        public static void SendCurrentStats()
        {
            if (Current.Game == null) return;

            var uid = ClientValues.Username ?? string.Empty;
            var storyteller = Current.Game.storyteller?.def?.defName ?? "Unknown";

            double totalWealth = 0.0;
            int colonistCount = 0;
            foreach (var map in Find.Maps)
            {
                if (map.IsPlayerHome)
                {
                    totalWealth += map.wealthWatcher.WealthTotal;
                    colonistCount += map.mapPawns.FreeColonistsCount;
                }
            }

            double playtimeSeconds = Current.Game.Info?.RealPlayTimeInteracting ?? 0.0;
            int daysPassed = (int)GenDate.DaysPassed;
            long nowUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var stats = new StatisticsData
            {
                _uid             = uid,
                _storyteller     = storyteller,
                _difficulty      = "Unknown",
                _wealth          = totalWealth,
                _colonistCount   = colonistCount,
                _playtimeSeconds = playtimeSeconds,
                _daysPassed      = daysPassed,
                _timestampUtc    = nowUtc
            };

            Network.Listener.EnqueuePacket(PacketHeader.StatsManager, stats);
        }
    }
}