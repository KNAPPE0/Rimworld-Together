using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameServer.TCP;
using Shared;
using Shared.Packets.Data;
using GameServer.Core;
using GameServer.Misc;
using static Shared.CommonEnumerators;

namespace GameServer.Managers
{
    public static class StatsManager
    {
        private static readonly string StatsFilePath = Path.Combine(
            string.IsNullOrEmpty(Master.ConfigsPath) ? "." : Master.ConfigsPath,
            "Stats.json"
        );

        // In-memory caches
        private static Dictionary<string, StatisticsData> _latestStats;
        private static Dictionary<string, List<StatisticsData>> _dailySnapshots;

        static StatsManager()
        {
            LoadAllStats();
        }

        /// <summary>Expose all live stats (one per UID).</summary>
        public static IEnumerable<StatisticsData> GetAllLiveStats()
        {
            return _latestStats.Values;
        }

        [HandlesPacket(PacketHeader.StatsManager)]
        private static void ParsePacket(ServerClient client, byte[] bytes)
        {
            try
            {
                var data = Serializer.ConvertBytesToObject<StatisticsData>(bytes);
                MergeStats(data);
            }
            catch (Exception ex)
            {
                Printer.Error($"[StatsManager] Error parsing Stats packet: {ex.Message}");
            }
        }

        private static void MergeStats(StatisticsData incoming)
        {
            // update “latest”
            _latestStats[incoming._uid] = incoming;
            SaveAllStats();

            // bucket into daily snapshot
            var dt = DateTimeOffset.FromUnixTimeSeconds(incoming._timestampUtc).UtcDateTime;
            string dateKey = dt.ToString("yyyy-MM-dd");
            if (!_dailySnapshots.ContainsKey(dateKey))
                _dailySnapshots[dateKey] = new List<StatisticsData>();

            var dayList = _dailySnapshots[dateKey];
            var existing = dayList.FirstOrDefault(s => s._uid == incoming._uid);
            if (existing != null)
                dayList.Remove(existing);
            dayList.Add(incoming);
            SaveAllStats();
        }

        private static void LoadAllStats()
        {
            try
            {
                if (File.Exists(StatsFilePath))
                {
                    var wrapper = Serializer.SerializeFromFile<StatsFileWrapper>(StatsFilePath);
                    _latestStats    = wrapper.Latest  ?? new Dictionary<string, StatisticsData>(StringComparer.OrdinalIgnoreCase);
                    _dailySnapshots = wrapper.Daily   ?? new Dictionary<string, List<StatisticsData>>(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    _latestStats    = new Dictionary<string, StatisticsData>(StringComparer.OrdinalIgnoreCase);
                    _dailySnapshots = new Dictionary<string, List<StatisticsData>>(StringComparer.OrdinalIgnoreCase);
                    SaveAllStats();
                }
            }
            catch (Exception ex)
            {
                Printer.Error($"[StatsManager] Failed to load Stats.json: {ex.Message}");
                _latestStats    = new Dictionary<string, StatisticsData>(StringComparer.OrdinalIgnoreCase);
                _dailySnapshots = new Dictionary<string, List<StatisticsData>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void SaveAllStats()
        {
            try
            {
                var wrapper = new StatsFileWrapper
                {
                    Latest = _latestStats,
                    Daily  = _dailySnapshots
                };
                Serializer.SerializeToFile(StatsFilePath, wrapper);
            }
            catch (Exception ex)
            {
                Printer.Error($"[StatsManager] Failed to save Stats.json: {ex.Message}");
            }
        }

        /// <summary>Top N players by wealth on a specific date.</summary>
        public static List<StatisticsData> GetTopForDate(string dateKey, int topN)
        {
            if (!_dailySnapshots.TryGetValue(dateKey, out var dayList))
                return new List<StatisticsData>();
            return dayList.OrderByDescending(s => s._wealth).Take(topN).ToList();
        }

        /// <summary>Live top N players by wealth.</summary>
        public static List<StatisticsData> GetLiveTop(int topN)
        {
            return _latestStats.Values.OrderByDescending(s => s._wealth).Take(topN).ToList();
        }
    }

    [Serializable]
    public class StatsFileWrapper
    {
        public Dictionary<string, StatisticsData> Latest = new();
        public Dictionary<string, List<StatisticsData>> Daily = new();
    }
}