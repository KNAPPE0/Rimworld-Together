using GameServer.Core;
using Shared;
using Shared.Files.Sites;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork;
using TCPNetwork.Files.Client;
using TCPNetwork.Packets;
using static TCPNetwork.Packets.PKT_Site;

namespace GameServer.PacketManager
{
    /// <summary>
    /// On-disk site file lookup with an in-memory cache.
    ///
    /// Hot path SendRewardsToPlayer used to read+deserialize every site file
    /// from disk per call, per player. The cache eliminates that I/O storm.
    /// Mutations call <see cref="InvalidateCache"/> so subsequent reads see
    /// the new on-disk state.
    /// </summary>
    public static class SiteManagerHelper
    {
        // Cache of all SiteFile objects keyed by Tile.
        private static readonly object SiteCacheLock = new object();
        private static Dictionary<int, SiteFile> SiteCache;

        // Cache of CustomSiteData by file path. Updated in place on save.
        private static readonly object CustomCacheLock = new object();
        private static readonly Dictionary<string, CustomSiteData> CustomDataCache =
            new Dictionary<string, CustomSiteData>(StringComparer.OrdinalIgnoreCase);

        public static void InvalidateCache()
        {
            lock (SiteCacheLock) { SiteCache = null; }
            lock (CustomCacheLock) { CustomDataCache.Clear(); }
        }

        private static Dictionary<int, SiteFile> GetSiteCache()
        {
            lock (SiteCacheLock)
            {
                if (SiteCache != null) return SiteCache;

                Dictionary<int, SiteFile> cache = new Dictionary<int, SiteFile>();
                try
                {
                    string[] sites = Directory.GetFiles(Master.SitesPath);
                    foreach (string site in sites)
                    {
                        if (site.IndexOf("_custom", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                        try
                        {
                            SiteFile siteFile = Serializer.SerializeFromFile<SiteFile>(site);
                            if (siteFile != null) cache[siteFile.Tile] = siteFile;
                        }
                        catch { }
                    }
                }
                catch (Exception ex) { Printer.Error($"Sites could not be loaded: {ex.Message}"); }

                SiteCache = cache;
                return cache;
            }
        }

        public static CustomSiteData LoadCustomSiteData(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            lock (CustomCacheLock)
            {
                if (CustomDataCache.TryGetValue(path, out CustomSiteData cached))
                    return cached;
            }

            if (!File.Exists(path)) return null;

            try
            {
                CustomSiteData data = Serializer.SerializeFromFile<CustomSiteData>(path);
                if (data == null) return null;

                lock (CustomCacheLock)
                {
                    CustomDataCache[path] = data;
                }
                return data;
            }
            catch
            {
                return null;
            }
        }

        public static void SaveCustomSiteData(string path, CustomSiteData data)
        {
            if (string.IsNullOrEmpty(path) || data == null) return;
            try
            {
                Serializer.SerializeToFile(path, data);
                lock (CustomCacheLock)
                {
                    CustomDataCache[path] = data;
                }
            }
            catch (Exception e) { Printer.Warning($"[Sites] Failed to save custom site data: {e}"); }
        }

        public static SiteFile[] GetAllSitesFromUsername(string username)
        {
            // KMH 26.5.20.1: Enumerate cache.Values INSIDE the lock — the
            // OnSaveCustomSiteData hook + InvalidateCache mutate this
            // dictionary concurrently, and C# Dictionary enumerators throw
            // on structural modification. Same fix applied to
            // UserManagerH.GetAllUserFiles earlier.
            if (string.IsNullOrEmpty(username)) return Array.Empty<SiteFile>();
            List<SiteFile> sitesList = new List<SiteFile>();
            lock (SiteCacheLock)
            {
                Dictionary<int, SiteFile> cache = GetSiteCache();
                foreach (SiteFile siteFile in cache.Values)
                {
                    if (siteFile != null && siteFile.Username == username) sitesList.Add(siteFile);
                }
            }
            return sitesList.ToArray();
        }

        public static SiteFile GetSiteFileFromTile(int tileToGet)
        {
            if (tileToGet < 0) return null;
            // KMH 26.5.20.1: TryGetValue inside the lock — the dictionary
            // can be mutated by SaveCustomSiteData on another thread, and
            // Dictionary.TryGetValue is not thread-safe.
            lock (SiteCacheLock)
            {
                GetSiteCache().TryGetValue(tileToGet, out SiteFile siteFile);
                return siteFile;
            }
        }

        public static void GetSiteInfo(ServerClient client, PKT_Site data)
        {
            SiteFile siteFile = GetSiteFileFromTile(data._file.Tile);
            data._stepMode = SiteStepMode.Info;
            data._file = siteFile;

            client.Listener.EnqueuePacket(PacketHeader.SiteManager, data);
        }

        public static SiteFile[] GetAllSites()
        {
            // KMH 26.5.20.1: Snapshot inside the lock — see
            // GetAllSitesFromUsername for rationale.
            lock (SiteCacheLock)
            {
                Dictionary<int, SiteFile> cache = GetSiteCache();
                SiteFile[] result = new SiteFile[cache.Count];
                int i = 0;
                foreach (SiteFile site in cache.Values) result[i++] = site;
                return result;
            }
        }

        public static bool CheckIfTileIsInUse(int tileToCheck)
        {
            if (tileToCheck < 0) return false;
            return GetSiteCache().ContainsKey(tileToCheck);
        }

        // KMH 26.5.20.1: SiteTypes is a config list that doesn't change at
        // runtime — but the previous lookup was a LINQ FirstOrDefault scan
        // called on every site reward, site creation, and site info request.
        // Cache as a dictionary keyed by defName, rebuild lazily if the
        // underlying SiteTypes reference changes (config reload).
        private static readonly object SiteTypeIndexLock = new object();
        private static Dictionary<string, SiteType> _siteTypeByDef;
        private static object _siteTypeIndexSource;

        public static SiteType GetTypeFromDef(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return null;
            var current = Master.ActionConfigs?.SiteAction?.SiteTypes;
            if (current == null || current.Count == 0) return null;

            lock (SiteTypeIndexLock)
            {
                if (_siteTypeByDef == null || !ReferenceEquals(_siteTypeIndexSource, current))
                {
                    var dict = new Dictionary<string, SiteType>(current.Count, StringComparer.OrdinalIgnoreCase);
                    foreach (SiteType st in current)
                    {
                        if (st != null && !string.IsNullOrEmpty(st.DefName)) dict[st.DefName] = st;
                    }
                    _siteTypeByDef = dict;
                    _siteTypeIndexSource = current;
                }
                return _siteTypeByDef.TryGetValue(defName, out SiteType hit) ? hit : null;
            }
        }
    }
}
