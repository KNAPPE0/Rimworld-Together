using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class OnlineSettlementFile
    {
        // Tile of the settlement, defaults to 0 if not set.
        public int Tile { get; set; } = 0;

        // Owner of the settlement, defaults to an empty string to prevent null issues.
        public string Owner { get; set; } = string.Empty;

        // Goodwill associated with the settlement, defaults to Goodwill.Neutral.
        public Goodwill Goodwill { get; set; } = Goodwill.Neutral;

        // Parameterless constructor for serialization/deserialization purposes.
        public OnlineSettlementFile() { }

        // Constructor with parameters for easier instantiation.
        public OnlineSettlementFile(int tile, string owner, Goodwill goodwill)
        {
            Tile = tile;
            Owner = owner ?? string.Empty; // Handle potential null owner by defaulting to an empty string.
            Goodwill = goodwill;
        }
    }
}