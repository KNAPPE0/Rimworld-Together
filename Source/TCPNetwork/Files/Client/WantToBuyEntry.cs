namespace TCPNetwork.Files.Client
{
    /// <summary>
    /// KMH 26.5.20: One Want-To-Buy entry on a player's UserFile. The
    /// mirror of a marketplace listing — instead of "I'm selling N×X at Y
    /// silver", it says "I'm buying up to N×X at Y silver". Stored on
    /// UserFile because WTBs are per-player and short-lived (a player
    /// typically removes one once they get what they wanted).
    /// </summary>
    public class WantToBuyEntry
    {
        /// <summary>Item defName (resolved by ItemLabelCache at command time).</summary>
        public string ItemDefName { get; set; } = string.Empty;

        /// <summary>How many units the player is looking for.</summary>
        public int MaxQty { get; set; } = 1;

        /// <summary>Max silver per unit the player will pay.</summary>
        public int MaxUnitPriceSilver { get; set; } = 0;

        /// <summary>UTC ticks when this entry was added (display only).</summary>
        public long AddedUtcTicks { get; set; } = 0;
    }
}
