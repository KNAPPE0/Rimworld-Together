using GameServer.Core;
using Shared;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using static Shared.Misc.Printer;

namespace GameServer.Managers
{
    /// <summary>
    /// KMH 2.7: Server-side cache of (defName → human label).
    ///
    /// Populated by <c>PKT_ItemLabels</c> from clients on login. Persisted to
    /// disk so that even before any client has reconnected, queued Discord
    /// commands and the leaderboard can render proper labels.
    ///
    /// Resolution order in <see cref="LabelFor"/>:
    ///   1. Cached label (set by a connected client)
    ///   2. <see cref="DefNameHumanizer.Humanize"/> fallback (works without DefDatabase)
    /// </summary>
    public static class ItemLabelCache
    {
        private static string SavePath => Path.Combine(Master.AssetsPath, "ItemLabels.json");

        private static readonly object Lock = new object();
        private static Dictionary<string, string> _byDefName =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void Initialize()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    var loaded = Serializer.SerializeFromFile<Dictionary<string, string>>(SavePath);
                    if (loaded != null)
                    {
                        lock (Lock)
                        {
                            _byDefName = new Dictionary<string, string>(loaded, StringComparer.OrdinalIgnoreCase);
                        }
                        Printer.Warning($"[ItemLabels] Loaded {_byDefName.Count} cached labels from disk.", LogImportanceMode.Verbose);
                    }
                }
            }
            catch (Exception e) { Printer.Warning($"[ItemLabels] Load failed: {e}"); }
        }

        public static int Count
        {
            get { lock (Lock) return _byDefName.Count; }
        }

        public static void ApplySnapshot(IDictionary<string, string> incoming)
        {
            if (incoming == null || incoming.Count == 0) return;

            int added = 0, updated = 0;
            lock (Lock)
            {
                foreach (var kv in incoming)
                {
                    if (string.IsNullOrEmpty(kv.Key) || string.IsNullOrEmpty(kv.Value)) continue;
                    string clean = kv.Value.Trim();
                    if (clean.Length == 0) continue;

                    if (_byDefName.TryGetValue(kv.Key, out string existing))
                    {
                        if (!string.Equals(existing, clean, StringComparison.Ordinal))
                        {
                            _byDefName[kv.Key] = clean;
                            updated++;
                        }
                    }
                    else
                    {
                        _byDefName[kv.Key] = clean;
                        added++;
                    }
                }
            }

            if (added > 0 || updated > 0)
            {
                SaveAsync();
                Printer.Warning($"[ItemLabels] +{added} new, ~{updated} updated (total {Count})", LogImportanceMode.Verbose);
            }
        }

        /// <summary>
        /// Returns the prettiest label we know for this defName.
        /// Never returns null — falls back to a humanized form of the defName.
        /// </summary>
        public static string LabelFor(string defName)
        {
            if (string.IsNullOrEmpty(defName)) return string.Empty;
            lock (Lock)
            {
                if (_byDefName.TryGetValue(defName, out string label) && !string.IsNullOrEmpty(label))
                    return label;
            }
            return DefNameHumanizer.Humanize(defName);
        }

        /// <summary>
        /// Try to resolve a user-typed query (e.g. "plasteel", "knife", "melee weapon")
        /// to a defName from the cache. Returns the best match or null.
        ///
        /// Search order:
        ///   1. Exact defName match (case-insensitive)
        ///   2. Exact label match (case-insensitive)
        ///   3. Single label that contains the query as a whole word
        ///   4. Single label that starts with the query
        ///   5. null (ambiguous or unknown)
        ///
        /// Populates <paramref name="candidates"/> with up to 8 matches when
        /// the query is ambiguous so the caller can show a helpful list.
        /// </summary>
        public static string ResolveDefNameByQuery(string query, out List<string> candidates)
        {
            candidates = new List<string>();
            if (string.IsNullOrWhiteSpace(query)) return null;
            string q = query.Trim();

            // KMH 26.5.20.1 security: cap query length. Without this, a
            // hostile Discord user can pass a 1500-char string (Discord's
            // message cap is 2000) and the per-label .Contains() inside the
            // loop below becomes O(label×query) over EVERY label in the
            // cache — easily 10k items × 1500 chars per call. Pure
            // server-CPU DoS. Real defNames / labels never exceed 64 chars.
            const int MaxQueryLen = 64;
            if (q.Length > MaxQueryLen) q = q.Substring(0, MaxQueryLen);

            string qLower = q.ToLowerInvariant();

            lock (Lock)
            {
                // 1. exact defName
                if (_byDefName.ContainsKey(q)) return q;

                // 2. exact label match
                List<string> exactLabel = new List<string>();
                List<string> wordContains = new List<string>();
                List<string> startsWith = new List<string>();
                List<string> contains = new List<string>();

                foreach (var kv in _byDefName)
                {
                    if (string.IsNullOrEmpty(kv.Value)) continue;
                    string labelLower = kv.Value.ToLowerInvariant();

                    if (string.Equals(labelLower, qLower, StringComparison.Ordinal))
                        exactLabel.Add(kv.Key);
                    else if (ContainsWord(labelLower, qLower))
                        wordContains.Add(kv.Key);
                    else if (labelLower.StartsWith(qLower, StringComparison.Ordinal))
                        startsWith.Add(kv.Key);
                    else if (labelLower.Contains(qLower))
                        contains.Add(kv.Key);
                }

                if (exactLabel.Count == 1) return exactLabel[0];
                if (exactLabel.Count > 1) { candidates.AddRange(Take8(exactLabel)); return null; }

                if (wordContains.Count == 1) return wordContains[0];
                if (wordContains.Count > 1) { candidates.AddRange(Take8(wordContains)); return null; }

                if (startsWith.Count == 1) return startsWith[0];
                if (startsWith.Count > 1) { candidates.AddRange(Take8(startsWith)); return null; }

                if (contains.Count == 1) return contains[0];
                if (contains.Count > 1) { candidates.AddRange(Take8(contains)); return null; }
            }
            return null;
        }

        private static IEnumerable<string> Take8(List<string> src)
        {
            int n = Math.Min(8, src.Count);
            List<string> r = new List<string>(n);
            for (int i = 0; i < n; i++) r.Add(src[i]);
            return r;
        }

        private static bool ContainsWord(string haystack, string needle)
        {
            int idx = haystack.IndexOf(needle, StringComparison.Ordinal);
            while (idx >= 0)
            {
                bool leftOk = idx == 0 || !char.IsLetterOrDigit(haystack[idx - 1]);
                int end = idx + needle.Length;
                bool rightOk = end == haystack.Length || !char.IsLetterOrDigit(haystack[end]);
                if (leftOk && rightOk) return true;
                idx = haystack.IndexOf(needle, idx + 1, StringComparison.Ordinal);
            }
            return false;
        }

        private static void SaveAsync()
        {
            try
            {
                Dictionary<string, string> copy;
                lock (Lock)
                {
                    copy = new Dictionary<string, string>(_byDefName, StringComparer.OrdinalIgnoreCase);
                }
                System.Threading.Tasks.Task.Run(() =>
                {
                    try { Serializer.SerializeToFile(SavePath, copy); }
                    catch (Exception e) { Printer.Warning($"[ItemLabels] Save failed: {e}"); }
                });
            }
            catch (Exception e) { Printer.Warning($"[ItemLabels] SaveAsync failed: {e}"); }
        }
    }
}
