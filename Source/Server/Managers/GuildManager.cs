using GameServer.PacketManager;
using Shared.Files.Economy;
using Shared.Files.Guilds;
using Shared.Misc;
using System;
using System.Collections.Generic;
using TCPNetwork.Files.Client;

namespace GameServer.Managers
{
    /// <summary>
    /// Server-side guild policy + perk + tax + contribution surface.
    /// All gameplay-side guild logic lives here. The legacy
    /// <c>GuildManagerH</c> in <c>PM_Guilds</c> stays as a thin lookup helper.
    /// </summary>
    public static partial class GuildManager
    {
        // Hook: when a deposit lands in a guild treasury, increment the
        // depositing member's contribution counter. Also fires the live
        // broadcast so all online watchers see the new balance.
        static GuildManager()
        {
            TreasuryManager.OnDepositLanded += OnTreasuryDeposit;
            TreasuryManager.OnDepositLanded += OnAnyTreasuryDepositBroadcast;
        }

        private static void OnAnyTreasuryDepositBroadcast(string ownerKey, string username, int silverDelta, string itemDef, int itemDelta)
        {
            try { GameServer.PacketManager.PM_Treasury.BroadcastTreasurySnapshot(ownerKey); }
            catch { }
        }

        private static void OnTreasuryDeposit(string ownerKey, string username, int silverDelta, string itemDef, int itemDelta)
        {
            // Only guild-owned treasuries count toward member contributions.
            if (string.IsNullOrEmpty(ownerKey) || ownerKey.StartsWith("_personal:", StringComparison.OrdinalIgnoreCase))
                return;
            if (string.IsNullOrEmpty(username)) return;

            try
            {
                GuildFile g = GuildManagerH.GetFactionFromName(ownerKey);
                if (g == null) return;
                GuildMember m = FindMember(g, username);
                if (m == null) return;

                if (silverDelta > 0) m.SilverContributed += silverDelta;
                if (itemDelta > 0) m.ItemsContributed += itemDelta;
                g.Persist();

                // KMH 2.7: Push the updated guild file to every connected
                // member so anyone with the Guild Hall open sees fresh
                // contribution counters live. Cheap — same packet shape we
                // already send on perk/settings changes.
                try { PM_GuildHall.BroadcastSnapshotToGuild(ownerKey); } catch { }
            }
            catch (Exception e)
            {
                Printer.Warning($"[Guild] Contribution-counter update failed: {e}");
            }
        }

        // -- tax APIs (called from PM_Sites.Rewards / MarketplaceManager) --

        /// <summary>
        /// Skim guild silver tax off a site reward. Returns the player's net amount.
        /// The skimmed silver lands in the guild treasury (no-op if no guild).
        /// </summary>
        public static int ApplyGuildSiteRewardTax(string username, int silverGross)
        {
            if (silverGross <= 0 || string.IsNullOrEmpty(username)) return silverGross;

            UserFile uf = UserManagerH.GetUserFileFromName(username);
            if (uf == null || string.IsNullOrEmpty(uf.GuildName)) return silverGross;

            GuildFile g = GuildManagerH.GetFactionFromName(uf.GuildName);
            if (g == null) return silverGross;

            int tax = (int)Math.Floor(silverGross * (Math.Max(0, Math.Min(50, g.Settings.SiteRewardSilverTaxPercent)) / 100.0));
            if (tax <= 0) return silverGross;

            TreasuryManager.DepositSilverForUser(username, tax,
                TreasuryTransaction.TxKind.SiteRewardSilver, $"guild-tax:{uf.GuildName}");

            return silverGross - tax;
        }

        /// <summary>
        /// Skim guild silver tax off a marketplace sale. Returns the seller's net amount.
        /// </summary>
        public static int ApplyGuildMarketplaceSaleTax(string sellerUsername, int sellerSilverGross)
        {
            if (sellerSilverGross <= 0 || string.IsNullOrEmpty(sellerUsername)) return sellerSilverGross;

            UserFile uf = UserManagerH.GetUserFileFromName(sellerUsername);
            if (uf == null || string.IsNullOrEmpty(uf.GuildName)) return sellerSilverGross;

            GuildFile g = GuildManagerH.GetFactionFromName(uf.GuildName);
            if (g == null) return sellerSilverGross;

            int tax = (int)Math.Floor(sellerSilverGross * (Math.Max(0, Math.Min(50, g.Settings.MarketplaceSaleTaxPercent)) / 100.0));
            if (tax <= 0) return sellerSilverGross;

            TreasuryManager.DepositSilverForUser(sellerUsername, tax,
                TreasuryTransaction.TxKind.MarketplaceSale, $"guild-tax:{uf.GuildName}");

            return sellerSilverGross - tax;
        }

        // -- perk effect resolvers --

        public static int GetMaxWorkerBonus(string guildName)
        {
            GuildFile g = GuildManagerH.GetFactionFromName(guildName);
            return g?.Perks?.SiteMaxWorkersBonus ?? 0;
        }

        public static double GetWorkerXpMultiplier(string guildName)
        {
            GuildFile g = GuildManagerH.GetFactionFromName(guildName);
            return g?.Perks?.WorkerXpMultiplier ?? 1.0;
        }

        public static double GetCustomSiteCostMultiplier(string guildName)
        {
            GuildFile g = GuildManagerH.GetFactionFromName(guildName);
            return g?.Perks?.CustomSiteCostMultiplier ?? 1.0;
        }

        public static int GetMarketplaceTaxReductionPoints(string guildName)
        {
            GuildFile g = GuildManagerH.GetFactionFromName(guildName);
            return g?.Perks?.MarketplaceTaxReductionPoints ?? 0;
        }

        // -- perk purchase --

        public enum PerkKind { SiteMaxWorkers, MarketplaceTax, WorkerXp, CustomSiteCost }

        public static (bool ok, string note) PurchasePerk(string requestingUsername, string guildName, PerkKind kind)
        {
            if (string.IsNullOrEmpty(requestingUsername) || string.IsNullOrEmpty(guildName)) return (false, "No guild context.");
            GuildFile g = GuildManagerH.GetFactionFromName(guildName);
            if (g == null) return (false, "Guild not found.");

            GuildMember.GuildRanks? rank = TreasuryManager.ResolveRankInGuild(requestingUsername, guildName);
            // Officer+ may purchase perks (lighter than withdraw permissions, since this is for guild benefit).
            if (rank == null || rank.Value == GuildMember.GuildRanks.Member)
                return (false, "Officer or higher required to purchase perks.");

            int currentLevel = ReadPerkLevel(g, kind);
            if (currentLevel >= GuildPerks.MaxLevel) return (false, "Perk already at max level.");

            int cost = GuildPerks.CostFor(currentLevel);

            TreasuryFile t = TreasuryManager.GetOrCreate(guildName, isGuildOwned: true);
            if (!TreasuryManager.TryWithdrawSilver(t, requestingUsername, cost,
                    TreasuryTransaction.TxKind.Withdraw, $"perk-buy:{kind}"))
                return (false, $"Treasury short on silver ({cost} required).");

            WritePerkLevel(g, kind, currentLevel + 1);
            g.Persist();

            try { Integrations.Discord.DiscordAnnouncer.GuildPerkPurchased(guildName, kind.ToString(), currentLevel + 1, cost, requestingUsername); }
            catch { }

            return (true, $"Purchased {kind} L{currentLevel + 1} for {cost} silver.");
        }

        private static int ReadPerkLevel(GuildFile g, PerkKind kind)
        {
            switch (kind)
            {
                case PerkKind.SiteMaxWorkers: return g.Perks.SiteMaxWorkersBonusLevel;
                case PerkKind.MarketplaceTax: return g.Perks.MarketplaceTaxReductionLevel;
                case PerkKind.WorkerXp: return g.Perks.WorkerXpBonusLevel;
                case PerkKind.CustomSiteCost: return g.Perks.CustomSiteCostDiscountLevel;
                default: return 0;
            }
        }

        private static void WritePerkLevel(GuildFile g, PerkKind kind, int newLevel)
        {
            switch (kind)
            {
                case PerkKind.SiteMaxWorkers: g.Perks.SiteMaxWorkersBonusLevel = newLevel; break;
                case PerkKind.MarketplaceTax: g.Perks.MarketplaceTaxReductionLevel = newLevel; break;
                case PerkKind.WorkerXp: g.Perks.WorkerXpBonusLevel = newLevel; break;
                case PerkKind.CustomSiteCost: g.Perks.CustomSiteCostDiscountLevel = newLevel; break;
            }
        }

        // -- settings + MOTD --

        public static (bool ok, string note) UpdateSettings(string requestingUsername, string guildName, GuildSettings updated)
        {
            if (updated == null) return (false, "No payload.");
            GuildFile g = GuildManagerH.GetFactionFromName(guildName);
            if (g == null) return (false, "Guild not found.");

            GuildMember.GuildRanks? rank = TreasuryManager.ResolveRankInGuild(requestingUsername, guildName);
            if (rank == null || rank.Value != GuildMember.GuildRanks.Admin)
                return (false, "Admin required to change guild settings.");

            // Sanitise.
            updated.SiteRewardSilverTaxPercent = Clamp0to50(updated.SiteRewardSilverTaxPercent);
            updated.MarketplaceSaleTaxPercent = Clamp0to50(updated.MarketplaceSaleTaxPercent);
            if (updated.MessageOfTheDay != null && updated.MessageOfTheDay.Length > 512)
                updated.MessageOfTheDay = updated.MessageOfTheDay.Substring(0, 512);

            g.Settings = updated;
            g.Persist();
            return (true, "Settings updated.");
        }

        // -- daily withdraw cap (called from PM_Treasury before letting silver go) --

        /// <summary>
        /// Check + reserve a silver withdraw against the member's daily cap.
        /// Returns (ok, note). If ok, the cap counter has already been incremented.
        /// </summary>
        public static (bool ok, string note) ReserveDailyWithdraw(string username, string guildName, int silverAmount)
        {
            if (silverAmount <= 0) return (true, null);
            GuildFile g = GuildManagerH.GetFactionFromName(guildName);
            if (g == null) return (true, null);

            GuildMember m = FindMember(g, username);
            if (m == null) return (true, null);

            int cap = ResolveCap(g, m.Rank);
            if (cap < 0) return (true, null); // unlimited

            int todayKey = DateOfYearKey();
            if (m.DailyWithdrawDayKey != todayKey)
            {
                m.DailyWithdrawDayKey = todayKey;
                m.DailyWithdrawnSilver = 0;
            }

            if (m.DailyWithdrawnSilver + silverAmount > cap)
            {
                int remaining = (int)Math.Max(0, cap - m.DailyWithdrawnSilver);
                return (false, $"Daily withdraw cap hit ({m.DailyWithdrawnSilver}/{cap}). Remaining today: {remaining}.");
            }

            m.DailyWithdrawnSilver += silverAmount;
            g.Persist();
            return (true, null);
        }

        private static int ResolveCap(GuildFile g, GuildMember.GuildRanks rank)
        {
            switch (rank)
            {
                case GuildMember.GuildRanks.Admin: return g.Settings.AdminDailyWithdrawCap;
                case GuildMember.GuildRanks.Moderator: return g.Settings.ModeratorDailyWithdrawCap;
                case GuildMember.GuildRanks.Officer: return g.Settings.OfficerDailyWithdrawCap;
                default: return g.Settings.MemberDailyWithdrawCap;
            }
        }

        // -- promotions to/from Officer --

        public static (bool ok, string note) SetMemberRank(string requestingUsername, string guildName, string targetUsername, GuildMember.GuildRanks newRank)
        {
            GuildFile g = GuildManagerH.GetFactionFromName(guildName);
            if (g == null) return (false, "Guild not found.");

            GuildMember.GuildRanks? requesterRank = TreasuryManager.ResolveRankInGuild(requestingUsername, guildName);
            if (requesterRank == null || requesterRank.Value != GuildMember.GuildRanks.Admin)
                return (false, "Admin required to change ranks.");

            GuildMember target = FindMember(g, targetUsername);
            if (target == null) return (false, "Member not found.");

            target.Rank = newRank;
            g.Persist();

            try { Integrations.Discord.DiscordAnnouncer.GuildMemberRankChanged(guildName, targetUsername, newRank); }
            catch { }

            return (true, $"{targetUsername} → {newRank}.");
        }

        // -- MOTD broadcast on login --

        public static string GetMotdForUser(string username)
        {
            UserFile uf = UserManagerH.GetUserFileFromName(username);
            if (uf == null || string.IsNullOrEmpty(uf.GuildName)) return null;
            GuildFile g = GuildManagerH.GetFactionFromName(uf.GuildName);
            string motd = g?.Settings?.MessageOfTheDay;
            return string.IsNullOrWhiteSpace(motd) ? null : motd;
        }

        // -- helpers --

        public static GuildMember FindMember(GuildFile g, string username)
        {
            if (g?.GuildMembers == null || string.IsNullOrEmpty(username)) return null;
            foreach (GuildMember m in g.GuildMembers)
            {
                if (string.Equals(m.Username, username, StringComparison.OrdinalIgnoreCase))
                    return m;
            }
            return null;
        }

        private static int Clamp0to50(int v) => Math.Max(0, Math.Min(50, v));

        private static int DateOfYearKey()
        {
            DateTime now = DateTime.UtcNow;
            return now.Year * 1000 + now.DayOfYear;
        }
    }
}
