using GameServer.Core;
using Shared;
using Shared.Files;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TCPNetwork.Files.Client;
using TCPNetwork.PacketManagers;
using TCPNetwork.Packets;

namespace GameServer.PacketManager
{
    public class PM_Leaderboard : PM_Base
    {
        private static float ScoreMultiplier = 0.001f;

        [HandlesPacket(PacketHeader.LeaderboardManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header) { SendLeaderboard(client); }

        private static void SendLeaderboard(ServerClient client)
        {
            PKT_Leaderboard data = new PKT_Leaderboard();
            data._file = (LeaderboardFile)LeaderboardFile.Load<LeaderboardFile>(LeaderboardFile.SavePath);

            // KMH: Populate rich entries from all map files for the enhanced UI
            try
            {
                data._file.Entries = BuildRichEntries();
            }
            catch (Exception e)
            {
                Printer.Error($"[PM_Leaderboard] BuildRichEntries failed: {e}");
            }

            client.Listener.EnqueuePacket(PacketHeader.LeaderboardManager, data);
        }

        // KMH: Build rich per-settlement entries from all map files on disk
        private static string GetDiscordNameForUser(string username)
        {
            try
            {
                if (string.IsNullOrEmpty(username)) return null;
                var userFile = GameServer.Managers.UserManagerH.GetUserFileFromName(username);
                return userFile?.DiscordUsername;
            }
            catch { return null; }
        }

        private static LeaderboardEntryFile[] BuildRichEntries()
        {
            List<LeaderboardEntryFile> entries = new List<LeaderboardEntryFile>();

            try
            {
                if (!Directory.Exists(Master.MapsPath)) return entries.ToArray();

                string[] mapPaths = Directory.GetFiles(Master.MapsPath, "*.json");

                foreach (string mapPath in mapPaths)
                {
                    try
                    {
                        MapFile mapFile = Serializer.SerializeFromFile<MapFile>(mapPath);
                        if (mapFile == null) continue;

                        SettlementFile settlement = PM_Settlements.GetSettlementFileFromTile(mapFile.Tile);

                        LeaderboardEntryFile entry = new LeaderboardEntryFile
                        {
                            Tile = mapFile.Tile,
                            Username = settlement?.Username ?? mapFile.Username ?? "Unknown",
                            DiscordUsername = GetDiscordNameForUser(settlement?.Username ?? mapFile.Username),
                            SettlementName = settlement?.Name ?? mapFile.SettlementName ?? "Unknown",
                            FactionName = mapFile.FactionName ?? "Unknown",
                            Wealth = mapFile.Wealth,
                            WealthExact = mapFile.WealthExact,
                            ColonistCount = mapFile.ColonistCount,
                            GameTicks = mapFile.GameTicks,
                            RealPlayTimeSeconds = mapFile.RealPlayTimeSeconds,
                            RealPlayTimeInteractingSeconds = mapFile.RealPlayTimeInteractingSeconds,
                            LastSavedUtcTicks = mapFile.LastSavedUtcTicks
                        };

                        entries.Add(entry);
                    }
                    catch { }
                }
            }
            catch { }

            return entries.ToArray();
        }

        public static void UpdateLeaderboard(ServerClient client, MapFile map)
        {
            LeaderboardFile file = (LeaderboardFile)LeaderboardFile.Load<LeaderboardFile>(LeaderboardFile.SavePath);
            double scoreValue = Math.Round(map.Wealth * ScoreMultiplier) + 1;

            if (!file.Scores.Keys.Contains(client.UserFile.Username)) file.Scores.Add(client.UserFile.Username, scoreValue);
            else
            {
                foreach (KeyValuePair<string, double> pair in file.Scores.ToArray())
                {
                    if (pair.Key == client.UserFile.Username)
                    {
                        double currentScore = pair.Value;
                        file.Scores.Remove(pair.Key);
                        file.Scores.Add(client.UserFile.Username, currentScore + scoreValue);
                    }
                }
            }

            LeaderboardFile.Save(LeaderboardFile.SavePath, file);
        }
        // KMH: Handle rich leaderboard request via PKT_Information (paginated + sorted)
        public static void HandleLeaderboardRequest(ServerClient client, PKT_Information data)
        {
            try
            {
                LeaderboardEntryFile[] all = BuildRichEntries();

                // Apply sort
                IEnumerable<LeaderboardEntryFile> sorted = all;
                switch (data._leaderboardSort)
                {
                    case PKT_Information.LeaderboardSortMode.Wealth:
                        sorted = all.OrderBy(e => e.Wealth);
                        break;
                    case PKT_Information.LeaderboardSortMode.WealthExact:
                        sorted = all.OrderBy(e => e.WealthExact);
                        break;
                    case PKT_Information.LeaderboardSortMode.Colonists:
                        sorted = all.OrderBy(e => e.ColonistCount);
                        break;
                    case PKT_Information.LeaderboardSortMode.PlaytimeTicks:
                        sorted = all.OrderBy(e => e.RealPlayTimeSeconds);
                        break;
                    case PKT_Information.LeaderboardSortMode.Days:
                        sorted = all.OrderBy(e => e.GameTicks);
                        break;
                    case PKT_Information.LeaderboardSortMode.SettlementName:
                        sorted = all.OrderBy(e => e.SettlementName ?? string.Empty);
                        break;
                    case PKT_Information.LeaderboardSortMode.FactionName:
                        sorted = all.OrderBy(e => e.FactionName ?? string.Empty);
                        break;
                    case PKT_Information.LeaderboardSortMode.LastSavedUtcTicks:
                        sorted = all.OrderBy(e => e.LastSavedUtcTicks);
                        break;
                }

                if (data._leaderboardOrder == PKT_Information.LeaderboardOrder.Desc)
                    sorted = sorted.Reverse();

                int total = all.Length;
                int offset = Math.Max(0, data._leaderboardOffset);
                int limit = data._leaderboardLimit > 0 ? Math.Min(data._leaderboardLimit, 100) : 10;

                LeaderboardEntryFile[] page = sorted.Skip(offset).Take(limit).ToArray();

                data._leaderboardTotal = total;
                data._leaderboardEntries = page;

                client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
            }
            catch (Exception e)
            {
                Printer.Error($"[PM_Leaderboard] HandleLeaderboardRequest failed: {e}");
                data._leaderboardTotal = 0;
                data._leaderboardEntries = Array.Empty<LeaderboardEntryFile>();
                client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
            }
        }
    }
}
