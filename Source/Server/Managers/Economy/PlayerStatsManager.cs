using Shared.Files.Economy;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork.Files.Client;
using static Shared.Misc.Printer;

namespace GameServer.Managers
{
    /// <summary>
    /// Server-side surface for tracking and reading per-player
    /// lifetime statistics (donations, sales, quests, sites, worker XP).
    ///
    /// All counters live on the player's <see cref="UserFile"/> so they
    /// persist across restarts and survive guild changes — guild-side
    /// counters in <c>GuildMember</c> still exist for guild-internal
    /// rankings.
    /// </summary>
    public static class PlayerStatsManager
    {
        // Fired whenever any tracked stat is recorded. PM_PlayerStats
        // subscribes to this and broadcasts a fresh leaderboard snapshot to all
        // connected clients (throttled), so dialogs update live without needing
        // a save+quit cycle.
        public static event System.Action OnStatsChanged;

        private static void NotifyChanged()
        {
            try { OnStatsChanged?.Invoke(); }
            catch { }
        }

        // Per-username lock to protect read-modify-write on UserFile
        // lifetime counters. Two events for the SAME user (e.g. a marketplace
        // sale + a treasury deposit landing within the same tick) would
        // otherwise race on `uf.LifetimeXxx += amount` and lose one increment.
        // We use a per-username lock instead of a single global lock so
        // events for DIFFERENT users still run in parallel.
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> StatLocks =
            new System.Collections.Concurrent.ConcurrentDictionary<string, object>(System.StringComparer.OrdinalIgnoreCase);

        private static object LockFor(string username) =>
            StatLocks.GetOrAdd(username, _ => new object());

        // -- bootstrap --

        public static void Initialize()
        {
            // Hook treasury deposits → only count silver going INTO a guild
            // treasury as a donation. Personal-vault deposits don't.
            TreasuryManager.OnDepositLanded += OnTreasuryDeposit;
            Printer.Warning("[PlayerStats] Subscribed to treasury deposits.", LogImportanceMode.Verbose);
        }

        // -- mutators (call from gameplay paths) --

        public static void RecordSilverDonated(string username, long amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(username)) return;
            UserFile uf = UserManagerH.GetUserFileFromName(username);
            if (uf == null) return;
            lock (LockFor(username))
            {
                EnsureFirstSeen(uf);
                uf.LifetimeSilverDonated += amount;
                uf.SaveUserFile();
            }
            NotifyChanged();
        }

        public static void RecordMarketplaceSale(string sellerUsername, long sellerNetSilver, int unitsSold)
        {
            if (string.IsNullOrEmpty(sellerUsername) || sellerNetSilver <= 0) return;
            UserFile uf = UserManagerH.GetUserFileFromName(sellerUsername);
            if (uf == null) return;
            lock (LockFor(sellerUsername))
            {
                EnsureFirstSeen(uf);
                uf.LifetimeSilverEarnedFromSales += sellerNetSilver;
                uf.LifetimeMarketplaceSalesCount += 1;
                uf.SaveUserFile();
            }
            NotifyChanged();
        }

        public static void RecordMarketplacePurchase(string buyerUsername, long totalSilverPaid)
        {
            if (string.IsNullOrEmpty(buyerUsername) || totalSilverPaid <= 0) return;
            UserFile uf = UserManagerH.GetUserFileFromName(buyerUsername);
            if (uf == null) return;
            lock (LockFor(buyerUsername))
            {
                EnsureFirstSeen(uf);
                uf.LifetimeSilverSpentOnPurchases += totalSilverPaid;
                uf.SaveUserFile();
            }
            NotifyChanged();
        }

        public static void RecordQuestPosted(string posterUsername)
        {
            if (string.IsNullOrEmpty(posterUsername)) return;
            UserFile uf = UserManagerH.GetUserFileFromName(posterUsername);
            if (uf == null) return;
            lock (LockFor(posterUsername))
            {
                EnsureFirstSeen(uf);
                uf.LifetimeQuestsPosted += 1;
                uf.SaveUserFile();
            }
            NotifyChanged();
        }

        public static void RecordQuestCompleted(string completerUsername)
        {
            if (string.IsNullOrEmpty(completerUsername)) return;
            UserFile uf = UserManagerH.GetUserFileFromName(completerUsername);
            if (uf == null) return;
            lock (LockFor(completerUsername))
            {
                EnsureFirstSeen(uf);
                uf.LifetimeQuestsCompleted += 1;
                uf.SaveUserFile();
            }
            NotifyChanged();
        }

        public static void RecordSiteBuilt(string username)
        {
            if (string.IsNullOrEmpty(username)) return;
            UserFile uf = UserManagerH.GetUserFileFromName(username);
            if (uf == null) return;
            lock (LockFor(username))
            {
                EnsureFirstSeen(uf);
                uf.LifetimeSitesBuilt += 1;
                uf.SaveUserFile();
            }
            NotifyChanged();
        }

        public static void RecordSiteRaided(string username)
        {
            if (string.IsNullOrEmpty(username)) return;
            UserFile uf = UserManagerH.GetUserFileFromName(username);
            if (uf == null) return;
            lock (LockFor(username))
            {
                EnsureFirstSeen(uf);
                uf.LifetimeSitesRaided += 1;
                uf.SaveUserFile();
            }
            NotifyChanged();
        }

        public static void RecordWorkerXp(string username, long xpDelta)
        {
            if (string.IsNullOrEmpty(username) || xpDelta <= 0) return;
            UserFile uf = UserManagerH.GetUserFileFromName(username);
            if (uf == null) return;
            lock (LockFor(username))
            {
                EnsureFirstSeen(uf);
                uf.LifetimeWorkerXpEarned += xpDelta;
                uf.SaveUserFile();
            }
            NotifyChanged();
        }

        public static void RecordFirstSeen(string username)
        {
            if (string.IsNullOrEmpty(username)) return;
            UserFile uf = UserManagerH.GetUserFileFromName(username);
            if (uf == null) return;
            EnsureFirstSeen(uf);
        }

        // -- leaderboard --

        public class PlayerSummary
        {
            public string Username { get; set; }
            public string GuildName { get; set; }
            public long SilverDonated { get; set; }
            public long SalesEarned { get; set; }
            public long PurchasesSpent { get; set; }
            public int QuestsCompleted { get; set; }
            public int QuestsPosted { get; set; }
            public int MarketplaceSales { get; set; }
            public int SitesBuilt { get; set; }
            public int SitesRaided { get; set; }
            public long WorkerXp { get; set; }
            public long FirstSeenUtcTicks { get; set; }
            public bool IsLinkedToDiscord { get; set; }

            /// <summary>Sum of all silver-flavoured contributions; the default sort key.</summary>
            public long TotalEconomyScore =>
                SilverDonated + SalesEarned / 4 + (long)QuestsCompleted * 50 + (long)SitesBuilt * 100;
        }

        public static List<PlayerSummary> ComputeLeaderboard()
        {
            List<PlayerSummary> result = new List<PlayerSummary>();
            try
            {
                foreach (UserFile uf in UserManagerH.GetAllUserFiles())
                {
                    if (uf == null || string.IsNullOrEmpty(uf.Username)) continue;
                    if (uf.IsBanned) continue;
                    result.Add(new PlayerSummary
                    {
                        Username = uf.Username,
                        GuildName = uf.GuildName ?? string.Empty,
                        SilverDonated = uf.LifetimeSilverDonated,
                        SalesEarned = uf.LifetimeSilverEarnedFromSales,
                        PurchasesSpent = uf.LifetimeSilverSpentOnPurchases,
                        QuestsCompleted = uf.LifetimeQuestsCompleted,
                        QuestsPosted = uf.LifetimeQuestsPosted,
                        MarketplaceSales = uf.LifetimeMarketplaceSalesCount,
                        SitesBuilt = uf.LifetimeSitesBuilt,
                        SitesRaided = uf.LifetimeSitesRaided,
                        WorkerXp = uf.LifetimeWorkerXpEarned,
                        FirstSeenUtcTicks = uf.FirstSeenUtcTicks,
                        IsLinkedToDiscord = !string.IsNullOrEmpty(uf.DiscordUsername)
                    });
                }
            }
            catch (Exception e) { Printer.Warning($"[PlayerStats] Leaderboard build failed: {e}"); }
            return result;
        }

        // -- internals --

        private static void EnsureFirstSeen(UserFile uf)
        {
            if (uf.FirstSeenUtcTicks == 0)
                uf.FirstSeenUtcTicks = DateTime.UtcNow.Ticks;
        }

        private static void OnTreasuryDeposit(string ownerKey, string username, int silverDelta, string itemDef, int itemDelta)
        {
            // Only count silver going to a guild treasury as a "donation".
            if (string.IsNullOrEmpty(ownerKey) || string.IsNullOrEmpty(username)) return;
            if (silverDelta <= 0) return;
            if (ownerKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase)) return;

            UserFile uf = UserManagerH.GetUserFileFromName(username);
            if (uf == null) return;

            lock (LockFor(username))
            {
                EnsureFirstSeen(uf);
                uf.LifetimeSilverDonated += silverDelta;
                uf.SaveUserFile();
            }
            NotifyChanged();
        }
    }
}
