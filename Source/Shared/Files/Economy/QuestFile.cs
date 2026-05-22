using System.Collections.Generic;

namespace Shared.Files.Economy
{
    /// <summary>
    /// A community-posted quest with a bounty escrowed from the poster's
    /// treasury. Two flavours:
    ///
    ///   * <see cref="QuestKind.DeliverItem"/> — server-verifiable. The
    ///     completer hands in items via Submit, the server validates,
    ///     transfers items to the recipient treasury, and pays out the bounty.
    ///
    ///   * <see cref="QuestKind.Bounty"/> — free-form text. Manual completion
    ///     by the poster (or an admin). Useful for "raid this guy", "build a
    ///     monument at tile X", etc.
    /// </summary>
    public class QuestFile
    {
        public long Id { get; set; }

        public QuestKind Kind { get; set; } = QuestKind.DeliverItem;
        public QuestState State { get; set; } = QuestState.Open;

        // -- poster + bounty source --
        public string PosterUsername { get; set; } = string.Empty;
        public string PosterTreasuryKey { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // -- bounty (escrowed at post time) --
        public int BountySilver { get; set; }
        public Dictionary<string, int> BountyItems { get; set; } = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

        // -- delivery target (DeliverItem only) --
        public string TargetItemDefName { get; set; } = string.Empty;
        public int TargetItemQty { get; set; }
        /// <summary>Treasury key the items get delivered to. Defaults to poster's.</summary>
        public string TargetTreasuryKey { get; set; } = string.Empty;

        // -- lifecycle --
        public long PostedUtcTicks { get; set; }
        public long ExpiresUtcTicks { get; set; }

        public string ClaimedByUsername { get; set; } = string.Empty;
        public long ClaimedUtcTicks { get; set; }
        public long CompletedUtcTicks { get; set; }

        // KMH: Visibility scope. Defaults to Public so existing data is unaffected.
        public QuestVisibility Visibility { get; set; } = QuestVisibility.Public;

        /// <summary>
        /// Returns true if a player with the given guild membership can see this quest.
        /// </summary>
        public bool IsVisibleTo(string username, string guildName)
        {
            if (Visibility == QuestVisibility.Public) return true;
            // GuildOnly: viewer must share the poster's guild.
            if (string.IsNullOrEmpty(guildName)) return false;
            // Personal-poster (treasury key starts with "_personal:") never has a guild scope.
            if (string.IsNullOrEmpty(PosterTreasuryKey) || PosterTreasuryKey.StartsWith("_personal:", System.StringComparison.OrdinalIgnoreCase))
                return false;
            return string.Equals(PosterTreasuryKey, guildName, System.StringComparison.OrdinalIgnoreCase);
        }

        public bool IsExpired(long nowTicks) => ExpiresUtcTicks > 0 && nowTicks >= ExpiresUtcTicks && State == QuestState.Open;
    }

    public enum QuestVisibility
    {
        /// <summary>Anyone on the server can see and claim.</summary>
        Public = 0,

        /// <summary>Only members of the poster's guild can see/claim.</summary>
        GuildOnly = 1
    }

    public enum QuestKind
    {
        /// <summary>Deliver X of an item to a target treasury for a bounty.</summary>
        DeliverItem = 0,

        /// <summary>Free-form bounty completed manually by the poster.</summary>
        Bounty = 1
    }

    public enum QuestState
    {
        /// <summary>Anyone can claim it.</summary>
        Open,

        /// <summary>Reserved by a single user — others can't double-claim.</summary>
        Claimed,

        /// <summary>Submitted by claimer; awaiting auto-verification or poster sign-off.</summary>
        Submitted,

        /// <summary>Bounty paid out, quest closed.</summary>
        Completed,

        /// <summary>Lifetime exceeded; bounty refunded to poster.</summary>
        Expired,

        /// <summary>Poster cancelled before completion; bounty refunded.</summary>
        Cancelled
    }
}
