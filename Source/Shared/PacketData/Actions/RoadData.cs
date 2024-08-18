using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class RoadData
    {
        // The current step mode of the road activity
        public RoadStepMode StepMode { get; set; } = RoadStepMode.Add; // Default to 'Add', adjust as necessary

        // Details about the road
        public RoadDetails Details { get; set; } = new RoadDetails(); // Initialize with a new RoadDetails object
    }
}