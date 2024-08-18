using System;
using System.Collections.Generic;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class SiteData
    {
        // The mode/step of the site operation
        public SiteStepMode SiteStepMode { get; set; } = SiteStepMode.Accept; // Default to 'Accept', adjust as necessary

        // The tile where the site is located
        public int Tile { get; set; } = 0; // Default to 0, representing the map tile

        // The type of the site
        public int Type { get; set; } = 0; // Default to 0

        // The owner of the site
        public string Owner { get; set; } = string.Empty; // Default to an empty string

        // Data related to the workers at the site
        public byte[] WorkerData { get; set; } = new byte[0]; // Initialize with an empty byte array

        // Goodwill associated with the site
        public Goodwill Goodwill { get; set; } = Goodwill.Neutral; // Default to 'Neutral', adjust as necessary

        // Indicates if the site belongs to a faction
        public bool IsFromFaction { get; set; } = false; // Default to false

        // List of sites with rewards available
        public List<int> SitesWithRewards { get; set; } = new List<int>(); // Initialize with an empty list
    }
}