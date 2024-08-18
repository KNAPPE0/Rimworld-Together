using System;
using System.Collections.Generic;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class TransferData
    {
        // Step mode of the transfer process
        public TransferStepMode TransferStepMode { get; set; } = TransferStepMode.TradeRequest; // Default to 'TradeRequest', adjust as necessary

        // Mode of the transfer (e.g., Gift, Trade)
        public TransferMode TransferMode { get; set; } = TransferMode.Gift; // Default to 'Gift', adjust as necessary

        // Tiles representing the source and destination of the transfer
        public int FromTile { get; set; } = 0; // Default to 0, represents the tile from which the transfer originates
        public int ToTile { get; set; } = 0;   // Default to 0, represents the destination tile

        // Data related to humans, animals, and items involved in the transfer
        public List<HumanData> HumanDatas { get; set; } = new List<HumanData>(); // Initialize with an empty list of HumanData
        public List<AnimalData> AnimalDatas { get; set; } = new List<AnimalData>(); // Initialize with an empty list of AnimalData
        public List<ThingData> ItemDatas { get; set; } = new List<ThingData>(); // Initialize with an empty list of ThingData
    }
}