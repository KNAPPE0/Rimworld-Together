namespace Shared.Files.Guilds
{
    /// <summary>
    /// Diplomatic state between two guilds.
    /// Stored on each guild's <c>Relationships</c> dict so both sides keep
    /// a local copy (avoids cross-guild lookups during chat / marketplace fan-out).
    /// </summary>
    public enum AllianceRelation
    {
        /// <summary>No declared relationship.</summary>
        None = 0,

        /// <summary>This guild has proposed alliance to the other; awaits accept.</summary>
        AlliedRequested = 1,

        /// <summary>Both sides have accepted; counts as ally for marketplace + quest + chat.</summary>
        Allied = 2,

        /// <summary>Declared hostile. Informational only — no PvP enforcement.</summary>
        Hostile = 3
    }
}
