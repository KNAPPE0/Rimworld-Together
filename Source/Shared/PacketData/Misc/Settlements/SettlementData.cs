using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class SettlementData
    {
        // The current step mode for the settlement
        public SettlementStepMode SettlementStepMode { get; set; } = SettlementStepMode.Add; // Default to 'Add', adjust as necessary

        // Tile where the settlement is located
        public int Tile { get; set; } = 0; // Default to 0, representing the map tile

        // Name of the settlement owner
        public string Owner { get; set; } = string.Empty; // Default to an empty string

        // Goodwill level associated with the settlement
        public Goodwill Goodwill { get; set; } = Goodwill.Neutral; // Default to 'Neutral', adjust as necessary
    }
}