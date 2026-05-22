using GameServer.Core;
using GameServer.PacketManager;
using Shared;
using Shared.Files.Economy;
using Shared.Files.Guilds;
using Shared.Misc;
using System;
using System.Collections.Generic;
using System.IO;
using TCPNetwork.Files.Client;

namespace GameServer.Managers
{
    /// <summary>
    /// Server-side treasury vault manager — backs <see cref="TreasuryFile"/>s
    /// for both guilds and individual non-guild players.
    ///
    /// All public methods are thread-safe; an in-memory cache fronts disk
    /// to avoid re-deserialising on every reward cycle.
    ///
    /// Treasury identification:
    ///   * Guild treasury  → file <c>Treasuries/&lt;guildname&gt;.json</c>, OwnerKey = guild name
    ///   * Personal vault → file <c>Treasuries/_personal__&lt;username&gt;.json</c>,
    ///                       OwnerKey = "_personal:&lt;username&gt;"
    /// </summary>
    public static class TreasuryManager
    {
        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, TreasuryFile> Cache =
            new Dictionary<string, TreasuryFile>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Fired AFTER a deposit lands in a treasury. Used by GuildManager to
        /// bump per-member contribution counters; downstream consumers can hook
        /// other side-effects (analytics, leaderboards) without coupling here.
        /// </summary>
        public static event System.Action<string /*ownerKey*/, string /*username*/, int /*silverDelta*/, string /*itemDef*/, int /*itemDelta*/> OnDepositLanded;

        // -- key/path resolution --

        public static string ResolveKeyForUser(ServerClient client)
        {
            if (client?.UserFile == null) return null;
            string guild = client.UserFile.GuildName;
            return string.IsNullOrEmpty(guild)
                ? TreasuryFile.PersonalKeyFor(client.UserFile.Username)
                : guild;
        }

        public static string ResolveKeyForUsername(string username)
        {
            if (string.IsNullOrEmpty(username)) return null;
            UserFile uf = UserManagerH.GetUserFileFromName(username);
            string guild = uf?.GuildName;
            return string.IsNullOrEmpty(guild)
                ? TreasuryFile.PersonalKeyFor(username)
                : guild;
        }

        private static string PathForKey(string ownerKey)
        {
            // Disk-safe filename — replace ':' with '__'.
            string safe = (ownerKey ?? string.Empty).Replace(":", "__");
            return Path.Combine(Master.TreasuriesPath, safe + ".json");
        }

        // -- load / save with cache --

        public static TreasuryFile GetOrCreate(string ownerKey, bool isGuildOwned)
        {
            if (string.IsNullOrEmpty(ownerKey)) return null;

            lock (CacheLock)
            {
                if (Cache.TryGetValue(ownerKey, out TreasuryFile cached) && cached != null)
                    return cached;

                string path = PathForKey(ownerKey);
                TreasuryFile file = null;

                if (File.Exists(path))
                {
                    try { file = Serializer.SerializeFromFile<TreasuryFile>(path); }
                    catch (Exception e) { Printer.Warning($"[Treasury] Failed to load {path}: {e}"); }
                }

                if (file == null)
                {
                    file = new TreasuryFile
                    {
                        OwnerKey = ownerKey,
                        IsGuildOwned = isGuildOwned
                    };
                }

                // Backfill in case of older saves.
                file.OwnerKey = ownerKey;
                file.IsGuildOwned = isGuildOwned;
                if (file.Items == null) file.Items = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (file.RecentTransactions == null) file.RecentTransactions = new List<TreasuryTransaction>();

                Cache[ownerKey] = file;
                Save(file);
                return file;
            }
        }

        public static void Save(TreasuryFile file)
        {
            if (file == null || string.IsNullOrEmpty(file.OwnerKey)) return;
            try
            {
                Serializer.SerializeToFile(PathForKey(file.OwnerKey), file);
            }
            catch (Exception e) { Printer.Warning($"[Treasury] Save failed: {e}"); }
        }

        public static void InvalidateCache()
        {
            lock (CacheLock) { Cache.Clear(); }
        }

        // -- mutation API (server-side only) --

        public static void DepositSilverForUser(string username, int amount, TreasuryTransaction.TxKind kind, string note)
        {
            if (amount <= 0 || string.IsNullOrEmpty(username)) return;
            string key = ResolveKeyForUsername(username);
            if (key == null) return;
            bool isGuild = !key.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
            TreasuryFile t = GetOrCreate(key, isGuild);

            long previousLifetimeIn;
            lock (CacheLock)
            {
                previousLifetimeIn = t.LifetimeSilverIn;
                t.SilverBalance += amount;
                t.LifetimeSilverIn += amount;
                t.RecordTransaction(new TreasuryTransaction
                {
                    UtcTicks = DateTime.UtcNow.Ticks,
                    Username = username,
                    Kind = kind,
                    Amount = amount,
                    Note = note ?? string.Empty
                });
                Save(t);
            }

            try { OnDepositLanded?.Invoke(key, username, amount, null, 0); }
            catch { }

            // KMH: Lifetime silver milestone announcement (10k / 100k / 1M).
            try
            {
                GameServer.Integrations.Discord.DiscordAnnouncer.TreasuryMilestone(
                    t.OwnerKey, t.IsGuildOwned, t.LifetimeSilverIn, previousLifetimeIn);
            }
            catch { }
        }

        public static void DepositItemForUser(string username, string defName, int amount, TreasuryTransaction.TxKind kind, string note)
        {
            if (amount <= 0 || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(defName)) return;

            // Silver shortcut so item + silver both flow through one API for routing.
            if (string.Equals(defName, "Silver", StringComparison.OrdinalIgnoreCase))
            {
                DepositSilverForUser(username, amount, kind, note);
                return;
            }

            string key = ResolveKeyForUsername(username);
            if (key == null) return;
            bool isGuild = !key.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase);
            TreasuryFile t = GetOrCreate(key, isGuild);

            lock (CacheLock)
            {
                if (!t.Items.TryGetValue(defName, out int current)) current = 0;
                t.Items[defName] = current + amount;
                t.RecordTransaction(new TreasuryTransaction
                {
                    UtcTicks = DateTime.UtcNow.Ticks,
                    Username = username,
                    Kind = kind,
                    Amount = amount,
                    ItemDefName = defName,
                    Note = note ?? string.Empty
                });
                Save(t);
            }

            try { OnDepositLanded?.Invoke(key, username, 0, defName, amount); }
            catch { }
        }

        /// <summary>Try to remove silver — returns true if it succeeded.</summary>
        public static bool TryWithdrawSilver(TreasuryFile t, string username, int amount, TreasuryTransaction.TxKind kind, string note)
        {
            if (t == null || amount <= 0) return false;
            lock (CacheLock)
            {
                if (t.SilverBalance < amount) return false;
                t.SilverBalance -= amount;
                t.LifetimeSilverOut += amount;
                t.RecordTransaction(new TreasuryTransaction
                {
                    UtcTicks = DateTime.UtcNow.Ticks,
                    Username = username,
                    Kind = kind,
                    Amount = amount,
                    Note = note ?? string.Empty
                });
                Save(t);
                return true;
            }
        }

        /// <summary>Try to remove items — returns the actually-withdrawn count (clamped to stock).</summary>
        public static int TryWithdrawItem(TreasuryFile t, string username, string defName, int amount, TreasuryTransaction.TxKind kind, string note)
        {
            if (t == null || amount <= 0 || string.IsNullOrEmpty(defName)) return 0;
            if (string.Equals(defName, "Silver", StringComparison.OrdinalIgnoreCase))
                return TryWithdrawSilver(t, username, amount, kind, note) ? amount : 0;

            lock (CacheLock)
            {
                if (!t.Items.TryGetValue(defName, out int stock) || stock <= 0) return 0;
                int taken = Math.Min(stock, amount);
                int remaining = stock - taken;
                if (remaining > 0) t.Items[defName] = remaining;
                else t.Items.Remove(defName);

                t.RecordTransaction(new TreasuryTransaction
                {
                    UtcTicks = DateTime.UtcNow.Ticks,
                    Username = username,
                    Kind = kind,
                    Amount = taken,
                    ItemDefName = defName,
                    Note = note ?? string.Empty
                });
                Save(t);
                return taken;
            }
        }

        // -- permission helpers --

        public static GuildMember.GuildRanks? ResolveRankInGuild(string username, string guildName)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(guildName)) return null;
            try
            {
                GuildFile g = GuildManagerH.GetFactionFromName(guildName);
                if (g == null) return null;
                foreach (GuildMember m in g.GuildMembers)
                {
                    if (string.Equals(m.Username, username, StringComparison.OrdinalIgnoreCase))
                        return m.Rank;
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// KMH: Username-based access check for Discord/headless code paths
        /// where there's no <see cref="ServerClient"/>. Same rules as
        /// <see cref="ClientCanAccessTreasury"/>.
        /// </summary>
        public static bool UsernameCanAccessTreasury(string username, TreasuryFile t, bool needsWithdrawPermission)
        {
            if (string.IsNullOrEmpty(username) || t == null) return false;

            if (!t.IsGuildOwned)
            {
                bool ownerMatches = string.Equals(t.OwnerKey, TreasuryFile.PersonalKeyFor(username), StringComparison.OrdinalIgnoreCase);
                return ownerMatches;
            }

            // Guild treasury: caller must currently belong to that guild AND meet the rank threshold.
            UserFile uf = UserManagerH.GetUserFileFromName(username);
            if (uf == null || !string.Equals(uf.GuildName, t.OwnerKey, StringComparison.OrdinalIgnoreCase))
                return false;

            GuildMember.GuildRanks? rank = ResolveRankInGuild(username, t.OwnerKey);
            return needsWithdrawPermission ? t.CanWithdraw(username, rank) : t.CanDeposit(username, rank);
        }

        public static bool ClientCanAccessTreasury(ServerClient client, TreasuryFile t, bool needsWithdrawPermission)
        {
            if (client?.UserFile == null || t == null) return false;
            string username = client.UserFile.Username;

            if (!t.IsGuildOwned)
            {
                bool ownerMatches = string.Equals(t.OwnerKey, TreasuryFile.PersonalKeyFor(username), StringComparison.OrdinalIgnoreCase);
                return ownerMatches;
            }

            // Guild-owned: must be in the guild named by OwnerKey.
            if (!string.Equals(client.UserFile.GuildName, t.OwnerKey, StringComparison.OrdinalIgnoreCase)) return false;
            GuildMember.GuildRanks? rank = ResolveRankInGuild(username, t.OwnerKey);
            return needsWithdrawPermission ? t.CanWithdraw(username, rank) : t.CanDeposit(username, rank);
        }
    }
}
