using System;

namespace Shared
{
    [Serializable]
    public class RoadDetails
    {
        // The definition name of the road
        public string RoadDefName { get; set; } = string.Empty; // Default to an empty string, representing no road defined

        // The starting tile of the road
        public int TileA { get; set; } = 0; // Default to 0, representing an uninitialized state

        // The ending tile of the road
        public int TileB { get; set; } = 0; // Default to 0, representing an uninitialized state
    }
}