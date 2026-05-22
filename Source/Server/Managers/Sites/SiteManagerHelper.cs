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
    // Site file lookup with in-memory cache. Mutations call InvalidateCache.
    public static class SiteManagerHelper
    {
        private static readonly object SiteCacheLock = new object();
        private static Dictionary<int, SiteFile> SiteCache;

        // CustomSiteData by file path; updated in place on save.
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
                    foreach (string site in Directory.GetFiles(Master.SitesPath))
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
            if (string.IsNullOrEmpty(username)) return Array.Empty<SiteFile>();
            List<SiteFile> sitesList = new List<SiteFile>();
            lock (SiteCacheLock)
            {
                Dictionary<int, SiteFile> cache = GetSiteCache();
                foreach (SiteFile siteFile in cache.Values)
                {
                    if (siteFile != null && string.Equals(siteFile.Username, username, StringComparison.OrdinalIgnoreCase))
                        sitesList.Add(siteFile);
                }
            }
            return sitesList.ToArray();
        }

        public static SiteFile GetSiteFileFromTile(int tileToGet)
        {
            if (tileToGet < 0) return null;
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
            // Was reading the dict outside the lock — Dictionary.ContainsKey is not concurrent-safe.
            lock (SiteCacheLock)
            {
                return GetSiteCache().ContainsKey(tileToCheck);
            }
        }

        // SiteTypes is config and stable per process; rebuild the def index only on config swap.
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
