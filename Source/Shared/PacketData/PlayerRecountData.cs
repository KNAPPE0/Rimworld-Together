using System;
using System.Collections.Generic;

namespace Shared
{
    [Serializable]
    public class PlayerRecountData
    {
        // A string representing the current number of players or a summary
        public string CurrentPlayers { get; set; } = string.Empty; // Default to an empty string

        // A list of names of the current players
        public List<string> CurrentPlayerNames { get; set; } = new List<string>(); // Initialize with an empty list
    }
}