using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class OnlineSiteFile
    {
        // Tile of the site, defaults to 0 if not set.
        public int Tile { get; set; } = 0;

        // Owner of the site, defaults to an empty string to prevent null issues.
        public string Owner { get; set; } = string.Empty;

        // Goodwill associated with the site, defaults to Goodwill.Neutral.
        public Goodwill Goodwill { get; set; } = Goodwill.Neutral;

        // Type of the site, defaults to 0 if not set.
        public int Type { get; set; } = 0;

        // Indicates if the site is from a faction, defaults to false.
        public bool FromFaction { get; set; } = false;

        // Parameterless constructor for serialization/deserialization purposes.
        public OnlineSiteFile() { }

        // Constructor with parameters for easier instantiation.
        public OnlineSiteFile(int tile, string owner, Goodwill goodwill, int type, bool fromFaction)
        {
            Tile = tile;
            Owner = owner ?? string.Empty; // Handle potential null owner by defaulting to an empty string.
            Goodwill = goodwill;
            Type = type;
            FromFaction = fromFaction;
        }
    }
}