using Shared.Files.Guilds;
using System.Collections.Generic;

namespace Shared.Files.Economy
{
    // Pooled silver + item vault. Same shape for guild and personal — path/permissions differ.
    public class TreasuryFile
    {
        public const int MaxTransactionLogEntries = 100;

        // Guild name, or "_personal:<username>".
        public string OwnerKey { get; set; } = string.Empty;

        public bool IsGuildOwned { get; set; }

        public int SilverBalance { get; set; }

        public Dictionary<string, int> Items { get; set; } = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        // Bounded ring — oldest pruned first.
        public List<TreasuryTransaction> RecentTransactions { get; set; } = new List<TreasuryTransaction>();

        public long LifetimeSilverIn { get; set; }
        public long LifetimeSilverOut { get; set; }

        public void RecordTransaction(TreasuryTransaction tx)
        {
            if (tx == null) return;
            RecentTransactions.Add(tx);
            while (RecentTransactions.Count > MaxTransactionLogEntries)
                RecentTransactions.RemoveAt(0);
        }

        // Personal: owner only. Guild: Moderator+ can withdraw.
        public bool CanWithdraw(string username, GuildMember.GuildRanks? rank)
        {
            if (string.IsNullOrEmpty(username)) return false;
            if (!IsGuildOwned)
            {
                return string.Equals(OwnerKey, PersonalKeyFor(username), System.StringComparison.OrdinalIgnoreCase);
            }

            if (rank == null) return false;
            return rank.Value == GuildMember.GuildRanks.Moderator
                || rank.Value == GuildMember.GuildRanks.Admin;
        }

        // Personal: owner only. Guild: any member.
        public bool CanDeposit(string username, GuildMember.GuildRanks? rank)
        {
            if (string.IsNullOrEmpty(username)) return false;
            if (!IsGuildOwned)
                return string.Equals(OwnerKey, PersonalKeyFor(username), System.StringComparison.OrdinalIgnoreCase);
            return rank != null;
        }

        public static string PersonalKeyFor(string username) => "_personal:" + (username ?? string.Empty).ToLowerInvariant();
    }

    public class TreasuryTransaction
    {
        public enum TxKind { Deposit, Withdraw, SiteRewardSilver, SiteRewardItem, MarketplaceSale, MarketplaceTax, MarketplaceRefund }

        public long UtcTicks { get; set; }
        public string Username { get; set; } = string.Empty;
        public TxKind Kind { get; set; }

        // Silver tx: amount; item tx: count.
        public int Amount { get; set; }

        // Item tx only.
        public string ItemDefName { get; set; } = string.Empty;

        public string Note { get; set; } = string.Empty;
    }
}
