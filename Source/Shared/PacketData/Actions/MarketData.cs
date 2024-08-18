using System;
using System.Collections.Generic;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class MarketData
    {
        // The current step mode of the market transaction
        public MarketStepMode MarketStepMode { get; set; } = MarketStepMode.Add; // Default to 'Add', adjust as necessary

        // Quantity and index for the item being managed in the market
        public int QuantityToManage { get; set; } = 0; // Default to 0, represents the quantity of items to manage
        public int IndexToManage { get; set; } = 0;    // Default to 0, represents the index of the item to manage

        // List of things (items) to be transferred in the market transaction
        public List<ThingData> TransferThings { get; set; } = new List<ThingData>(); // Initialized to an empty list of ThingData
    }
}