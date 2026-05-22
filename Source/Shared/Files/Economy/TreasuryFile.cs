using Shared.Files.Guilds;
using System.Collections.Generic;

namespace Shared.Files.Economy
{
    /// <summary>
    /// A pooled silver + item vault, owned by either a guild or a single
    /// (non-guild) player. The same shape is used for both — the difference
    /// is the file path and the permission resolution.
    ///
    /// Kept intentionally simple:
    ///   * Silver is a single int.
    ///   * Items are a defName→count dictionary (no quality, no hit-points;
    ///     server delivers fresh items at full HP).
    ///   * The transaction log is a bounded ring buffer (last N entries) so
    ///     audit history is queryable without unbounded growth.
    /// </summary>
    public class TreasuryFile
    {
        public const int MaxTransactionLogEntries = 100;

        /// <summary>Identifier — guild name, or <c>_personal:&lt;username&gt;</c>.</summary>
        public string OwnerKey { get; set; } = string.Empty;

        /// <summary>True if this treasury is shared by a guild.</summary>
        public bool IsGuildOwned { get; set; }

        /// <summary>Pooled silver balance.</summary>
        public int SilverBalance { get; set; }

        /// <summary>Items in the vault, keyed by defName.</summary>
        public Dictionary<string, int> Items { get; set; } = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>Most recent N transactions (oldest pruned first).</summary>
        public List<TreasuryTransaction> RecentTransactions { get; set; } = new List<TreasuryTransaction>();

        /// <summary>Lifetime totals — handy for guild leaderboards.</summary>
        public long LifetimeSilverIn { get; set; }
        public long LifetimeSilverOut { get; set; }

        public void RecordTransaction(TreasuryTransaction tx)
        {
            if (tx == null) return;
            RecentTransactions.Add(tx);
            while (RecentTransactions.Count > MaxTransactionLogEntries)
                RecentTransactions.RemoveAt(0);
        }

        /// <summary>Returns true if the username may withdraw, given guild membership/rank.</summary>
        public bool CanWithdraw(string username, GuildMember.GuildRanks? rank)
        {
            if (string.IsNullOrEmpty(username)) return false;
            if (!IsGuildOwned)
            {
                // Personal treasury: only the owning player.
                return string.Equals(OwnerKey, PersonalKeyFor(username), System.StringComparison.OrdinalIgnoreCase);
            }

            // Guild: Members can deposit & view, Mods can withdraw, Admins always.
            if (rank == null) return false;
            return rank.Value == GuildMember.GuildRanks.Moderator
                || rank.Value == GuildMember.GuildRanks.Admin;
        }

        /// <summary>Returns true if the username may deposit (anyone in the guild, or the personal owner).</summary>
        public bool CanDeposit(string username, GuildMember.GuildRanks? rank)
        {
            if (string.IsNullOrEmpty(username)) return false;
            if (!IsGuildOwned)
                return string.Equals(OwnerKey, PersonalKeyFor(username), System.StringComparison.OrdinalIgnoreCase);
            return rank != null;
        }

        public static string PersonalKeyFor(string username) => "_personal:" + (username ?? string.Empty).ToLowerInvariant();
    }

    /// <summary>One audit-log entry recorded against a treasury.</summary>
    public class TreasuryTransaction
    {
        public enum TxKind { Deposit, Withdraw, SiteRewardSilver, SiteRewardItem, MarketplaceSale, MarketplaceTax, MarketplaceRefund }

        public long UtcTicks { get; set; }
        public string Username { get; set; } = string.Empty;
        public TxKind Kind { get; set; }

        /// <summary>For silver tx, the absolute amount; for item tx, the item count.</summary>
        public int Amount { get; set; }

        /// <summary>For item tx, the defName; otherwise empty.</summary>
        public string ItemDefName { get; set; } = string.Empty;

        /// <summary>Free-form note for the audit log (e.g. site tile #).</summary>
        public string Note { get; set; } = string.Empty;
    }
}
