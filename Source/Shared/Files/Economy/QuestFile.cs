using System.Collections.Generic;

namespace Shared.Files.Economy
{
    // Community-posted quest with escrowed bounty.
    // DeliverItem = server-verifiable; Bounty = manual sign-off.
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
        // Defaults to poster's treasury.
        public string TargetTreasuryKey { get; set; } = string.Empty;

        // -- lifecycle --
        public long PostedUtcTicks { get; set; }
        public long ExpiresUtcTicks { get; set; }

        public string ClaimedByUsername { get; set; } = string.Empty;
        public long ClaimedUtcTicks { get; set; }
        public long CompletedUtcTicks { get; set; }

        // Defaults Public — preserves pre-Visibility-field data.
        public QuestVisibility Visibility { get; set; } = QuestVisibility.Public;

        public bool IsVisibleTo(string username, string guildName)
        {
            if (Visibility == QuestVisibility.Public) return true;
            if (string.IsNullOrEmpty(guildName)) return false;
            // Personal posters (no guild) can't post GuildOnly.
            if (string.IsNullOrEmpty(PosterTreasuryKey) || PosterTreasuryKey.StartsWith("_personal:", System.StringComparison.OrdinalIgnoreCase))
                return false;
            return string.Equals(PosterTreasuryKey, guildName, System.StringComparison.OrdinalIgnoreCase);
        }

        public bool IsExpired(long nowTicks) => ExpiresUtcTicks > 0 && nowTicks >= ExpiresUtcTicks && State == QuestState.Open;
    }

    public enum QuestVisibility
    {
        Public = 0,
        GuildOnly = 1
    }

    public enum QuestKind
    {
        DeliverItem = 0,
        Bounty = 1
    }

    public enum QuestState
    {
        Open,
        Claimed,    // reserved; others can't double-claim
        Submitted,  // awaiting auto-verify or poster sign-off
        Completed,
        Expired,    // refunded
        Cancelled   // refunded
    }
}
