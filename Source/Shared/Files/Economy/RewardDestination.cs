namespace Shared.Files.Economy
{
    /// <summary>
    /// Where a site's reward share is delivered when a cycle completes.
    /// Set per-owner at site build time, and per-worker when they join.
    /// </summary>
    public enum RewardDestination
    {
        /// <summary>Items materialise on the player's home map / caravan.</summary>
        Caravan = 0,

        /// <summary>Items go into the player's guild (or personal) treasury vault.</summary>
        Treasury = 1,

        /// <summary>Items are auto-listed on the marketplace at the site's configured unit price.</summary>
        Marketplace = 2
    }
}
