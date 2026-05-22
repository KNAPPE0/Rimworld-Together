namespace TCPNetwork.Files.Client
{
    // Per-player Want-To-Buy entry — mirror of a marketplace listing.
    public class WantToBuyEntry
    {
        // Resolved by ItemLabelCache at command time.
        public string ItemDefName { get; set; } = string.Empty;

        public int MaxQty { get; set; } = 1;

        public int MaxUnitPriceSilver { get; set; } = 0;

        public long AddedUtcTicks { get; set; } = 0;
    }
}
