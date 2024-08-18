using System;
using System.Collections.Generic;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class FactionGoodwillData
    {
        // Tile representing the faction's main location
        public int Tile { get; set; } = 0; // Default to 0, representing the map tile

        // Name of the faction's owner
        public string Owner { get; set; } = string.Empty; // Default to an empty string

        // Overall goodwill level associated with the faction
        public Goodwill Goodwill { get; set; } = Goodwill.Neutral; // Default to 'Neutral', adjust as necessary

        // List of tiles where settlements are located
        public List<int> SettlementTiles { get; set; } = new List<int>(); // Initialize with an empty list of settlement tiles

        // Array of goodwill levels associated with each settlement
        public Goodwill[] SettlementGoodwills { get; set; } = new Goodwill[0]; // Initialize with an empty array

        // List of tiles where sites are located
        public List<int> SiteTiles { get; set; } = new List<int>(); // Initialize with an empty list of site tiles

        // Array of goodwill levels associated with each site
        public Goodwill[] SiteGoodwills { get; set; } = new Goodwill[0]; // Initialize with an empty array
    }
}