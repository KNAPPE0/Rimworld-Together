using GameServer.Core;
using Shared;
using Shared.Files;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;

namespace GameServer.Managers
{
    public static class LeaderboardManager
    {
        private const int DefaultLimit = 10;
        private const int MaxLimit = 100;

        public static void SendLeaderboard(ServerClient client, InformationData data)
        {
            try
            {
                int limit = data._leaderboardLimit <= 0 ? DefaultLimit : data._leaderboardLimit;
                if (limit > MaxLimit) limit = MaxLimit;

                int offset = data._leaderboardOffset < 0 ? 0 : data._leaderboardOffset;

                List<MapStatsFile> allStats = LoadAllStatsSnapshots();
                data._leaderboardTotal = allStats.Count;

                IEnumerable<MapStatsFile> ordered = SortStats(allStats, data._leaderboardSort, data._leaderboardOrder);

                MapStatsFile[] page = ordered.Skip(offset).Take(limit).ToArray();

                LeaderboardEntryFile[] entries = new LeaderboardEntryFile[page.Length];
                for (int i = 0; i < page.Length; i++)
                {
                    MapStatsFile s = page[i];
                    entries[i] = BuildEntry(s, offset + i + 1);
                }

                data._leaderboardEntries = entries;

                client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
            }
            catch
            {
                try
                {
                    data._leaderboardTotal = -1;
                    data._leaderboardEntries = new LeaderboardEntryFile[0];
                    client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
                }
                catch { }
            }
        }

        private static List<MapStatsFile> LoadAllStatsSnapshots()
        {
            List<MapStatsFile> list = new List<MapStatsFile>();

            try
            {
                string suffix = $".stats{CommonValues.DefaultSaveFormat}";
                string[] files = Directory.GetFiles(Master.MapsPath, "*" + suffix);

                foreach (string path in files)
                {
                    try
                    {
                        MapStatsFile stats = Serializer.FileBytesToObject<MapStatsFile>(path);
                        if (stats != null && stats.Tile >= 0) list.Add(stats);
                    }
                    catch { }
                }
            }
            catch { }

            return list;
        }

        private static IEnumerable<MapStatsFile> SortStats(IEnumerable<MapStatsFile> stats,
            InformationData.LeaderboardSortMode sortMode,
            InformationData.LeaderboardOrder order)
        {
            bool desc = order == InformationData.LeaderboardOrder.Desc;

            switch (sortMode)
            {
                case InformationData.LeaderboardSortMode.Wealth:
                    return desc
                        ? stats.OrderBy(s => s.Wealth < 0).ThenByDescending(s => s.Wealth).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
                        : stats.OrderBy(s => s.Wealth < 0).ThenBy(s => s.Wealth).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase);

                case InformationData.LeaderboardSortMode.WealthExact:
                    return desc
                        ? stats.OrderBy(s => s.WealthExact < 0).ThenByDescending(s => s.WealthExact).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
                        : stats.OrderBy(s => s.WealthExact < 0).ThenBy(s => s.WealthExact).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase);

                case InformationData.LeaderboardSortMode.Colonists:
                    return desc
                        ? stats.OrderBy(s => s.ColonistCount < 0).ThenByDescending(s => s.ColonistCount).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
                        : stats.OrderBy(s => s.ColonistCount < 0).ThenBy(s => s.ColonistCount).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase);

                case InformationData.LeaderboardSortMode.PlaytimeTicks:
                    return desc
                        ? stats.OrderBy(s => s.GameTicks < 0).ThenByDescending(s => s.GameTicks).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
                        : stats.OrderBy(s => s.GameTicks < 0).ThenBy(s => s.GameTicks).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase);

                case InformationData.LeaderboardSortMode.Days:
                    return desc
                        ? stats.OrderBy(s => s.GameTicks < 0).ThenByDescending(s => s.GameTicks < 0 ? -1 : (s.GameTicks / 60000)).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
                        : stats.OrderBy(s => s.GameTicks < 0).ThenBy(s => s.GameTicks < 0 ? int.MaxValue : (s.GameTicks / 60000)).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase);

                case InformationData.LeaderboardSortMode.SettlementName:
                    return desc
                        ? stats.OrderBy(s => string.IsNullOrWhiteSpace(s.SettlementName)).ThenByDescending(s => s.SettlementName, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
                        : stats.OrderBy(s => string.IsNullOrWhiteSpace(s.SettlementName)).ThenBy(s => s.SettlementName, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase);

                case InformationData.LeaderboardSortMode.FactionName:
                    return desc
                        ? stats.OrderBy(s => string.IsNullOrWhiteSpace(s.FactionName)).ThenByDescending(s => s.FactionName, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
                        : stats.OrderBy(s => string.IsNullOrWhiteSpace(s.FactionName)).ThenBy(s => s.FactionName, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase);

                case InformationData.LeaderboardSortMode.LastSavedUtcTicks:
                    return desc
                        ? stats.OrderBy(s => s.LastSavedUtcTicks <= 0).ThenByDescending(s => s.LastSavedUtcTicks).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
                        : stats.OrderBy(s => s.LastSavedUtcTicks <= 0).ThenBy(s => s.LastSavedUtcTicks).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase);

                default:
                    return desc
                        ? stats.OrderBy(s => s.WealthExact < 0).ThenByDescending(s => s.WealthExact).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase)
                        : stats.OrderBy(s => s.WealthExact < 0).ThenBy(s => s.WealthExact).ThenBy(s => s.Username, StringComparer.OrdinalIgnoreCase);
            }
        }

        private static LeaderboardEntryFile BuildEntry(MapStatsFile stats, int rank)
        {
            LeaderboardEntryFile e = new LeaderboardEntryFile();

            e.Rank = rank;
            e.Tile = stats.Tile;

            e.Username = stats.Username ?? string.Empty;

            e.SettlementName = stats.SettlementName ?? string.Empty;
            e.FactionName = stats.FactionName ?? string.Empty;

            e.Wealth = stats.Wealth;
            e.WealthExact = stats.WealthExact;

            e.ColonistCount = stats.ColonistCount;
            e.GameTicks = stats.GameTicks;

            e.RealPlayTimeInteractingSeconds = stats.RealPlayTimeInteractingSeconds;
            e.LastSavedUtcTicks = stats.LastSavedUtcTicks;

            return e;
        }
    }
}