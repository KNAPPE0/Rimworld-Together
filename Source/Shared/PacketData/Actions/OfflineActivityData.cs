using System;
using static Shared.CommonEnumerators;

namespace Shared
{
    [Serializable]
    public class OfflineActivityData
    {
        // The current step mode of the offline activity
        public OfflineActivityStepMode ActivityStepMode { get; set; } = OfflineActivityStepMode.Request; // Default to 'Request', adjust as necessary

        // Tile representing the target location for the activity
        public int TargetTile { get; set; } = 0; // Default to 0, represents the target map tile

        // Data related to the map for the offline activity
        public MapData MapData { get; set; } = new MapData(); // Initialize with a new MapData object to avoid null references
    }
}