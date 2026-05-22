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

        // Cache for the rich-entries snapshot. Every
        // BuildRichEntries call used to:
        //   1. Directory.GetFiles on the maps directory
        //   2. Deserialize every MapFile from disk
        //   3. For each, look up its settlement + Discord handle
        // Called by SendLeaderboard (every leaderboard window open) AND
        // HandleLeaderboardRequest (every page change / sort flip). On a
        // server with 50 settlements that's 50 JSON parses per click.
        //
        // Invalidates whenever UpdateLeaderboard fires (which is the only
        // mutation point — called from PM_Maps.SaveUserMap after a map is
        // written).
        private static readonly object EntriesCacheLock = new object();
        private static LeaderboardEntryFile[] _entriesCache;

        public static void InvalidateEntriesCache()
        {
            lock (EntriesCacheLock) { _entriesCache = null; }
        }

        private static LeaderboardEntryFile[] GetCachedEntries()
        {
            lock (EntriesCacheLock)
            {
                if (_entriesCache != null) return _entriesCache;
                _entriesCache = BuildRichEntries();
                return _entriesCache;
            }
        }

        [HandlesPacket(PacketHeader.LeaderboardManager)]
        public override void Receive(ServerClient client, byte[] bytes, PacketHeader header) { SendLeaderboard(client); }

        private static void SendLeaderboard(ServerClient client)
        {
            PKT_Leaderboard data = new PKT_Leaderboard();
            data._file = (LeaderboardFile)LeaderboardFile.Load<LeaderboardFile>(LeaderboardFile.SavePath);

            // KMH: Populate rich entries from all map files for the enhanced UI
            try
            {
                // Read through the cache (rebuilt only on save).
                data._file.Entries = GetCachedEntries();
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

            // Was iterating the entire Scores dictionary as
            // an allocated array just to find the user's existing score and
            // remove+re-add it. Direct dictionary access is O(1).
            string user = client?.UserFile?.Username;
            if (!string.IsNullOrEmpty(user))
            {
                if (file.Scores.TryGetValue(user, out double existing))
                    file.Scores[user] = existing + scoreValue;
                else
                    file.Scores[user] = scoreValue;
            }

            LeaderboardFile.Save(LeaderboardFile.SavePath, file);

            // A map just changed → drop the rich-entries
            // cache so the next leaderboard request rebuilds with the new
            // wealth/colonist data instead of returning stale numbers.
            InvalidateEntriesCache();
        }
        // KMH: Handle rich leaderboard request via PKT_Information (paginated + sorted)
        public static void HandleLeaderboardRequest(ServerClient client, PKT_Information data)
        {
            try
            {
                // Pull from cache. Page changes / sort flips
                // can fire many times per second as a user scrolls — without
                // the cache that was a full disk-scan per click.
                LeaderboardEntryFile[] all = GetCachedEntries();

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
