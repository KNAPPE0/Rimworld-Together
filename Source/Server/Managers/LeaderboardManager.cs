using GameServer.Core;
using Shared;
using Shared.Files;
using Shared.Files.Maps;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;

namespace GameServer.Managers
{
    public static class LeaderboardManager
    {
        private static readonly ReaderWriterLockSlim CacheLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        private static readonly Dictionary<int, LeaderboardEntryFile> EntriesByTile = new Dictionary<int, LeaderboardEntryFile>();

        private static volatile bool _cacheBuilt = false;

        private static int _lastFullRebuildTick = 0;
        private const int FullRebuildCooldownMs = 15000;

        private const string StatsCacheFolderName = "StatsCache";
        private const string LeaderboardCacheFileName = "leaderboard-cache.json";

        private const string TombstonesFileName = "leaderboard-tombstones.json";
        private static readonly Dictionary<int, long> TombstonesByTile = new Dictionary<int, long>();

        private static Timer _persistTimer;
        private static int _persistScheduled = 0;
        private const int PersistDebounceMs = 2000;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true,
            IncludeFields = true
        };

        public static void Warmup()
        {
            EnsureCacheBuilt();
        }

        public static void UpsertFromStats(MapStatsFile stats)
        {
            if (stats == null || stats.Tile < 0) return;

            EnsureCacheBuilt();

            if (stats.LastSavedUtcTicks <= 0)
                stats.LastSavedUtcTicks = DateTime.UtcNow.Ticks;

            CacheLock.EnterWriteLock();
            try
            {
                TryClearTombstoneIfNewerUnsafe(stats.Tile, stats.LastSavedUtcTicks);

                if (IsTombstonedAndNotNewerUnsafe(stats.Tile, stats.LastSavedUtcTicks))
                    return;

                EntriesByTile[stats.Tile] = new LeaderboardEntryFile
                {
                    Tile = stats.Tile,
                    Username = stats.Username ?? string.Empty,
                    SettlementName = stats.SettlementName ?? string.Empty,
                    FactionName = stats.FactionName ?? string.Empty,
                    Wealth = stats.Wealth,
                    WealthExact = stats.WealthExact,
                    ColonistCount = stats.ColonistCount,
                    GameTicks = stats.GameTicks,
                    RealPlayTimeSeconds = stats.RealPlayTimeSeconds,
                    RealPlayTimeInteractingSeconds = stats.RealPlayTimeInteractingSeconds,
                    LastSavedUtcTicks = stats.LastSavedUtcTicks
                };
            }
            finally
            {
                CacheLock.ExitWriteLock();
            }

            SchedulePersist();
            TryWriteStatsCacheFile(stats);
        }

        public static void RemoveTile(int tile)
        {
            if (tile < 0) return;

            CacheLock.EnterWriteLock();
            try
            {
                EntriesByTile.Remove(tile);

                TombstonesByTile[tile] = DateTime.UtcNow.Ticks;

                TryDeleteStatsCacheFileUnsafe(tile);

                PersistTombstonesUnsafe();
            }
            finally
            {
                CacheLock.ExitWriteLock();
            }

            SchedulePersist();
        }

        public static void SendLeaderboard(ServerClient client, InformationData data)
        {
            if (client == null || data == null) return;

            EnsureCacheBuilt();
            TryCooldownRebuildIfEmptyOrOld();

            int limit = data._leaderboardLimit;
            if (limit <= 0) limit = 10;
            if (limit > 100) limit = 100;

            int offset = data._leaderboardOffset;
            if (offset < 0) offset = 0;

            InformationData.LeaderboardSortMode sort = data._leaderboardSort;
            InformationData.LeaderboardOrder order = data._leaderboardOrder;

            List<LeaderboardEntryFile> snapshot;

            CacheLock.EnterReadLock();
            try
            {
                snapshot = EntriesByTile.Values.ToList();
            }
            finally
            {
                CacheLock.ExitReadLock();
            }

            snapshot.Sort((a, b) => CompareEntries(a, b, sort, order));

            int total = snapshot.Count;
            data._leaderboardTotal = total;

            if (offset >= total)
            {
                data._leaderboardEntries = Array.Empty<LeaderboardEntryFile>();
                client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
                return;
            }

            int take = Math.Min(limit, total - offset);
            data._leaderboardEntries = snapshot.Skip(offset).Take(take).ToArray();

            client.Listener.EnqueuePacket(PacketHeader.InformationManager, data);
        }

        private static void EnsureCacheBuilt()
        {
            if (_cacheBuilt) return;

            CacheLock.EnterUpgradeableReadLock();
            try
            {
                if (_cacheBuilt) return;

                CacheLock.EnterWriteLock();
                try
                {
                    if (_cacheBuilt) return;

                    FullRebuildFromDiskUnsafe();
                    _cacheBuilt = true;
                }
                finally
                {
                    CacheLock.ExitWriteLock();
                }
            }
            finally
            {
                CacheLock.ExitUpgradeableReadLock();
            }
        }

        private static void TryCooldownRebuildIfEmptyOrOld()
        {
            int now = Environment.TickCount;

            CacheLock.EnterReadLock();
            try
            {
                if (EntriesByTile.Count > 0) return;
            }
            finally
            {
                CacheLock.ExitReadLock();
            }

            if (_lastFullRebuildTick != 0 && (now - _lastFullRebuildTick) < FullRebuildCooldownMs) return;
            _lastFullRebuildTick = now;

            CacheLock.EnterWriteLock();
            try
            {
                FullRebuildFromDiskUnsafe();
                _cacheBuilt = true;
            }
            finally
            {
                CacheLock.ExitWriteLock();
            }
        }

        private static void FullRebuildFromDiskUnsafe()
        {
            EntriesByTile.Clear();

            LoadTombstonesUnsafe();

            TryLoadLeaderboardCacheUnsafe();

            string mapsPath = Master.MapsPath;
            if (string.IsNullOrWhiteSpace(mapsPath)) return;
            if (!Directory.Exists(mapsPath)) return;

            try
            {
                foreach (string file in Directory.GetFiles(mapsPath, "*", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileName(file);
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    if (name.Equals(LeaderboardCacheFileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (name.IndexOf(".stats", StringComparison.OrdinalIgnoreCase) >= 0)
                        TryUpsertFromStatsLikeFileUnsafe(file, preferNewer: true);
                }
            }
            catch { }

            string statsCachePath = Path.Combine(mapsPath, StatsCacheFolderName);
            if (Directory.Exists(statsCachePath))
            {
                try
                {
                    foreach (string file in Directory.GetFiles(statsCachePath, "*", SearchOption.TopDirectoryOnly))
                    {
                        if (file.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) continue;
                        TryUpsertFromStatsLikeFileUnsafe(file, preferNewer: true);
                    }
                }
                catch { }
            }

            try
            {
                foreach (string file in Directory.GetFiles(mapsPath, "*", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileName(file);
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    if (name.Equals(LeaderboardCacheFileName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (name.IndexOf(".stats", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    if (name.Equals(StatsCacheFolderName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string baseName = Path.GetFileNameWithoutExtension(file);
                    if (!int.TryParse(baseName, out int tile))
                    {
                        if (!int.TryParse(name, out tile))
                            continue;
                    }

                    TryUpsertFromMapSaveUnsafe(file, tile);
                }
            }
            catch { }

            SchedulePersist();
            PersistTombstonesUnsafe();
        }

        private static void TryUpsertFromMapSaveUnsafe(string file, int tileFromName)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) return;

            long fileTicks = 0;
            try { fileTicks = File.GetLastWriteTimeUtc(file).Ticks; } catch { fileTicks = 0; }

            if (IsTombstonedAndNotNewerUnsafe(tileFromName, fileTicks))
                return;

            MapFile map = null;
            try { map = Serializer.FileBytesToObject<MapFile>(file); }
            catch { map = null; }

            if (map == null)
                return;

            MapStatsFile stats = BuildStatsFromMapFile(map, tileFromName, file);
            TryWriteStatsCacheFileIfMissing(stats);

            UpsertEntryUnsafe(stats, preferNewer: true);
        }

        private static MapStatsFile BuildStatsFromMapFile(MapFile mapFile, int tileFromName, string sourcePath)
        {
            MapStatsFile stats = new MapStatsFile();

            int tile = mapFile.Tile >= 0 ? mapFile.Tile : tileFromName;
            stats.Tile = tile;

            stats.Username = mapFile.Username ?? string.Empty;
            stats.SettlementName = mapFile.SettlementName ?? string.Empty;
            stats.FactionName = mapFile.FactionName ?? string.Empty;

            stats.Wealth = mapFile.Wealth;
            stats.WealthExact = mapFile.WealthExact >= 0 ? mapFile.WealthExact : -1;

            stats.GameTicks = mapFile.GameTicks;

            stats.RealPlayTimeSeconds = mapFile.RealPlayTimeSeconds >= 0 ? mapFile.RealPlayTimeSeconds : -1;
            stats.RealPlayTimeInteractingSeconds = mapFile.RealPlayTimeInteractingSeconds >= 0 ? mapFile.RealPlayTimeInteractingSeconds : -1;

            long savedTicks = mapFile.LastSavedUtcTicks;
            if (savedTicks <= 0)
            {
                try { savedTicks = File.GetLastWriteTimeUtc(sourcePath).Ticks; }
                catch { savedTicks = DateTime.UtcNow.Ticks; }
            }
            stats.LastSavedUtcTicks = savedTicks;

            stats.FactionThingCount = mapFile.FactionThings != null ? mapFile.FactionThings.Count : -1;
            stats.NonFactionThingCount = mapFile.NonFactionThings != null ? mapFile.NonFactionThings.Count : -1;

            stats.FactionHumanCount = mapFile.FactionHumans != null ? mapFile.FactionHumans.Count : -1;
            stats.NonFactionHumanCount = mapFile.NonFactionHumans != null ? mapFile.NonFactionHumans.Count : -1;

            stats.FactionAnimalCount = mapFile.FactionAnimals != null ? mapFile.FactionAnimals.Count : -1;
            stats.NonFactionAnimalCount = mapFile.NonFactionAnimals != null ? mapFile.NonFactionAnimals.Count : -1;

            stats.ColonistCount = stats.FactionHumanCount;

            TryBackfillFromSettlement(tile, stats);

            return stats;
        }

        private static void TryBackfillFromSettlement(int tile, MapStatsFile stats)
        {
            try
            {
                SettlementFile sf = SettlementManager.GetSettlementFileFromTile(tile);
                if (sf == null) return;

                if (string.IsNullOrWhiteSpace(stats.Username))
                    stats.Username = sf.Username ?? string.Empty;

                if (string.IsNullOrWhiteSpace(stats.SettlementName) && !string.IsNullOrWhiteSpace(sf.Name))
                    stats.SettlementName = sf.Name;
            }
            catch
            {
            }
        }

        private static void TryUpsertFromStatsLikeFileUnsafe(string file, bool preferNewer)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) return;

            long fileTicks = 0;
            try { fileTicks = File.GetLastWriteTimeUtc(file).Ticks; } catch { fileTicks = 0; }

            try
            {
                MapStatsFile stats = Serializer.FileBytesToObject<MapStatsFile>(file);
                if (stats != null && stats.Tile >= 0)
                {
                    if (stats.LastSavedUtcTicks <= 0)
                        stats.LastSavedUtcTicks = fileTicks > 0 ? fileTicks : DateTime.UtcNow.Ticks;

                    if (IsTombstonedAndNotNewerUnsafe(stats.Tile, stats.LastSavedUtcTicks))
                        return;

                    TryClearTombstoneIfNewerUnsafe(stats.Tile, stats.LastSavedUtcTicks);

                    UpsertEntryUnsafe(stats, preferNewer);
                    return;
                }
            }
            catch { }

            try
            {
                string json = File.ReadAllText(file);
                if (string.IsNullOrWhiteSpace(json)) return;

                MapStatsFile stats = JsonSerializer.Deserialize<MapStatsFile>(json, JsonOptions);
                if (stats == null || stats.Tile < 0) return;

                if (stats.LastSavedUtcTicks <= 0)
                    stats.LastSavedUtcTicks = fileTicks > 0 ? fileTicks : DateTime.UtcNow.Ticks;

                if (IsTombstonedAndNotNewerUnsafe(stats.Tile, stats.LastSavedUtcTicks))
                    return;

                TryClearTombstoneIfNewerUnsafe(stats.Tile, stats.LastSavedUtcTicks);

                UpsertEntryUnsafe(stats, preferNewer);
            }
            catch { }
        }

        private static void UpsertEntryUnsafe(MapStatsFile stats, bool preferNewer)
        {
            if (stats == null || stats.Tile < 0) return;

            LeaderboardEntryFile candidate = new LeaderboardEntryFile
            {
                Tile = stats.Tile,
                Username = stats.Username ?? string.Empty,
                SettlementName = stats.SettlementName ?? string.Empty,
                FactionName = stats.FactionName ?? string.Empty,
                Wealth = stats.Wealth,
                WealthExact = stats.WealthExact,
                ColonistCount = stats.ColonistCount,
                GameTicks = stats.GameTicks,
                RealPlayTimeSeconds = stats.RealPlayTimeSeconds,
                RealPlayTimeInteractingSeconds = stats.RealPlayTimeInteractingSeconds,
                LastSavedUtcTicks = stats.LastSavedUtcTicks
            };

            if (!EntriesByTile.TryGetValue(candidate.Tile, out LeaderboardEntryFile existing) || existing == null)
            {
                EntriesByTile[candidate.Tile] = candidate;
                return;
            }

            if (!preferNewer)
            {
                EntriesByTile[candidate.Tile] = candidate;
                return;
            }

            long eSaved = existing.LastSavedUtcTicks;
            long cSaved = candidate.LastSavedUtcTicks;

            if (cSaved > 0 && (eSaved <= 0 || cSaved > eSaved))
            {
                EntriesByTile[candidate.Tile] = candidate;
                return;
            }

            if (cSaved == eSaved)
            {
                bool existingMissingNames =
                    string.IsNullOrWhiteSpace(existing.Username) ||
                    string.IsNullOrWhiteSpace(existing.SettlementName) ||
                    string.IsNullOrWhiteSpace(existing.FactionName);

                bool candidateHasNames =
                    !string.IsNullOrWhiteSpace(candidate.Username) ||
                    !string.IsNullOrWhiteSpace(candidate.SettlementName) ||
                    !string.IsNullOrWhiteSpace(candidate.FactionName);

                if (existingMissingNames && candidateHasNames)
                {
                    EntriesByTile[candidate.Tile] = candidate;
                    return;
                }

                bool existingExact = existing.WealthExact >= 0;
                bool candidateExact = candidate.WealthExact >= 0;
                if (!existingExact && candidateExact)
                {
                    EntriesByTile[candidate.Tile] = candidate;
                    return;
                }
            }
        }

        private static bool TryLoadLeaderboardCacheUnsafe()
        {
            try
            {
                string mapsPath = Master.MapsPath;
                if (string.IsNullOrWhiteSpace(mapsPath)) return false;

                string cachePath = Path.Combine(mapsPath, LeaderboardCacheFileName);
                if (!File.Exists(cachePath)) return false;

                string json = File.ReadAllText(cachePath);
                if (string.IsNullOrWhiteSpace(json)) return false;

                List<LeaderboardEntryFile> list = JsonSerializer.Deserialize<List<LeaderboardEntryFile>>(json, JsonOptions);
                if (list == null || list.Count == 0) return false;

                foreach (LeaderboardEntryFile e in list)
                {
                    if (e == null || e.Tile < 0) continue;

                    if (IsTombstonedAndNotNewerUnsafe(e.Tile, e.LastSavedUtcTicks))
                        continue;

                    EntriesByTile[e.Tile] = e;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SchedulePersist()
        {
            if (_persistTimer == null)
                _persistTimer = new Timer(_ => PersistCacheToDiskSafe(), null, Timeout.Infinite, Timeout.Infinite);

            if (Interlocked.Exchange(ref _persistScheduled, 1) == 1) return;

            try
            {
                _persistTimer.Change(PersistDebounceMs, Timeout.Infinite);
            }
            catch
            {
                Interlocked.Exchange(ref _persistScheduled, 0);
            }
        }

        private static void PersistCacheToDiskSafe()
        {
            try
            {
                PersistCacheToDiskUnsafe();
            }
            catch
            {
            }
            finally
            {
                Interlocked.Exchange(ref _persistScheduled, 0);
            }
        }

        private static void PersistCacheToDiskUnsafe()
        {
            string mapsPath = Master.MapsPath;
            if (string.IsNullOrWhiteSpace(mapsPath)) return;
            if (!Directory.Exists(mapsPath)) return;

            List<LeaderboardEntryFile> snapshot;

            CacheLock.EnterReadLock();
            try
            {
                snapshot = EntriesByTile.Values.ToList();
            }
            finally
            {
                CacheLock.ExitReadLock();
            }

            string cachePath = Path.Combine(mapsPath, LeaderboardCacheFileName);
            string tmpPath = cachePath + ".tmp";

            try
            {
                string json = JsonSerializer.Serialize(snapshot, JsonOptions);
                File.WriteAllText(tmpPath, json);
                File.Move(tmpPath, cachePath, true);
            }
            catch
            {
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
            }
        }

        private static void TryWriteStatsCacheFileIfMissing(MapStatsFile stats)
        {
            try
            {
                string mapsPath = Master.MapsPath;
                if (string.IsNullOrWhiteSpace(mapsPath)) return;
                if (!Directory.Exists(mapsPath)) return;

                string dir = Path.Combine(mapsPath, StatsCacheFolderName);
                Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, stats.Tile.ToString());
                if (File.Exists(path)) return;

                string tmp = path + ".tmp";

                string json = JsonSerializer.Serialize(stats, JsonOptions);
                File.WriteAllText(tmp, json);
                File.Move(tmp, path, true);
            }
            catch
            {
            }
        }

        private static void TryWriteStatsCacheFile(MapStatsFile stats)
        {
            try
            {
                string mapsPath = Master.MapsPath;
                if (string.IsNullOrWhiteSpace(mapsPath)) return;
                if (!Directory.Exists(mapsPath)) return;

                string dir = Path.Combine(mapsPath, StatsCacheFolderName);
                Directory.CreateDirectory(dir);

                string path = Path.Combine(dir, stats.Tile.ToString());
                string tmp = path + ".tmp";

                string json = JsonSerializer.Serialize(stats, JsonOptions);
                File.WriteAllText(tmp, json);
                File.Move(tmp, path, true);
            }
            catch
            {
            }
        }

        private static void TryDeleteStatsCacheFileUnsafe(int tile)
        {
            try
            {
                string mapsPath = Master.MapsPath;
                if (string.IsNullOrWhiteSpace(mapsPath)) return;
                if (!Directory.Exists(mapsPath)) return;

                string path = Path.Combine(mapsPath, StatsCacheFolderName, tile.ToString());
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private class Tombstone
        {
            public int Tile { get; set; }
            public long DeletedUtcTicks { get; set; }
        }

        private static void LoadTombstonesUnsafe()
        {
            TombstonesByTile.Clear();

            try
            {
                string mapsPath = Master.MapsPath;
                if (string.IsNullOrWhiteSpace(mapsPath)) return;

                string path = Path.Combine(mapsPath, TombstonesFileName);
                if (!File.Exists(path)) return;

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return;

                List<Tombstone> list = JsonSerializer.Deserialize<List<Tombstone>>(json, JsonOptions);
                if (list == null) return;

                foreach (Tombstone t in list)
                {
                    if (t == null) continue;
                    if (t.Tile < 0) continue;
                    if (t.DeletedUtcTicks <= 0) continue;

                    TombstonesByTile[t.Tile] = t.DeletedUtcTicks;
                }
            }
            catch
            {
            }
        }

        private static void PersistTombstonesUnsafe()
        {
            try
            {
                string mapsPath = Master.MapsPath;
                if (string.IsNullOrWhiteSpace(mapsPath)) return;
                if (!Directory.Exists(mapsPath)) return;

                string path = Path.Combine(mapsPath, TombstonesFileName);
                string tmp = path + ".tmp";

                List<Tombstone> list = TombstonesByTile
                    .Select(kv => new Tombstone { Tile = kv.Key, DeletedUtcTicks = kv.Value })
                    .ToList();

                string json = JsonSerializer.Serialize(list, JsonOptions);
                File.WriteAllText(tmp, json);
                File.Move(tmp, path, true);
            }
            catch
            {
            }
        }

        private static bool IsTombstonedAndNotNewerUnsafe(int tile, long candidateSavedTicks)
        {
            if (tile < 0) return true;

            if (!TombstonesByTile.TryGetValue(tile, out long deletedTicks))
                return false;

            if (candidateSavedTicks <= 0) return true;

            return candidateSavedTicks <= deletedTicks;
        }

        private static void TryClearTombstoneIfNewerUnsafe(int tile, long candidateSavedTicks)
        {
            if (tile < 0) return;
            if (candidateSavedTicks <= 0) return;

            if (!TombstonesByTile.TryGetValue(tile, out long deletedTicks))
                return;

            if (candidateSavedTicks > deletedTicks)
            {
                TombstonesByTile.Remove(tile);
            }
        }

        private static int CompareEntries(LeaderboardEntryFile a, LeaderboardEntryFile b, InformationData.LeaderboardSortMode sort, InformationData.LeaderboardOrder order)
        {
            int result;

            switch (sort)
            {
                case InformationData.LeaderboardSortMode.Wealth:
                    result = CompareInt(a.Wealth, b.Wealth);
                    break;

                case InformationData.LeaderboardSortMode.WealthExact:
                    result = CompareDouble(GetWealthExactOrRounded(a), GetWealthExactOrRounded(b));
                    break;

                case InformationData.LeaderboardSortMode.Colonists:
                    result = CompareInt(a.ColonistCount, b.ColonistCount);
                    break;

                case InformationData.LeaderboardSortMode.PlaytimeTicks:
                    result = CompareDouble(GetPlaytimeSeconds(a), GetPlaytimeSeconds(b));
                    if (result == 0) result = CompareDouble(a.RealPlayTimeInteractingSeconds, b.RealPlayTimeInteractingSeconds);
                    if (result == 0) result = CompareInt(a.GameTicks, b.GameTicks);
                    break;

                case InformationData.LeaderboardSortMode.Days:
                    result = CompareInt(GetDays(a.GameTicks), GetDays(b.GameTicks));
                    break;

                case InformationData.LeaderboardSortMode.SettlementName:
                    result = CompareString(a.SettlementName, b.SettlementName);
                    break;

                case InformationData.LeaderboardSortMode.FactionName:
                    result = CompareString(a.FactionName, b.FactionName);
                    break;

                case InformationData.LeaderboardSortMode.LastSavedUtcTicks:
                    result = CompareLong(a.LastSavedUtcTicks, b.LastSavedUtcTicks);
                    break;

                default:
                    result = CompareDouble(GetWealthExactOrRounded(a), GetWealthExactOrRounded(b));
                    break;
            }

            if (result == 0) result = CompareString(a.Username, b.Username);
            if (result == 0) result = CompareInt(a.Tile, b.Tile);

            if (order == InformationData.LeaderboardOrder.Desc)
                result = -result;

            return result;
        }

        private static double GetPlaytimeSeconds(LeaderboardEntryFile e)
        {
            if (e == null) return -1;
            if (e.RealPlayTimeSeconds >= 0) return e.RealPlayTimeSeconds;
            return -1;
        }

        private static double GetWealthExactOrRounded(LeaderboardEntryFile e)
        {
            if (e == null) return -1;
            if (e.WealthExact >= 0) return e.WealthExact;
            if (e.Wealth >= 0) return e.Wealth;
            return -1;
        }

        private static int GetDays(int ticks)
        {
            if (ticks < 0) return -1;
            return ticks / 60000;
        }

        private static int CompareInt(int x, int y)
        {
            bool xBad = x < 0;
            bool yBad = y < 0;

            if (xBad && yBad) return 0;
            if (xBad) return -1;
            if (yBad) return 1;

            return x.CompareTo(y);
        }

        private static int CompareLong(long x, long y)
        {
            bool xBad = x <= 0;
            bool yBad = y <= 0;

            if (xBad && yBad) return 0;
            if (xBad) return -1;
            if (yBad) return 1;

            return x.CompareTo(y);
        }

        private static int CompareDouble(double x, double y)
        {
            bool xBad = x < 0;
            bool yBad = y < 0;

            if (xBad && yBad) return 0;
            if (xBad) return -1;
            if (yBad) return 1;

            return x.CompareTo(y);
        }

        private static int CompareString(string a, string b)
        {
            a = a ?? string.Empty;
            b = b ?? string.Empty;

            bool aBad = string.IsNullOrWhiteSpace(a);
            bool bBad = string.IsNullOrWhiteSpace(b);

            if (aBad && bBad) return 0;
            if (aBad) return -1;
            if (bBad) return 1;

            return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }
}