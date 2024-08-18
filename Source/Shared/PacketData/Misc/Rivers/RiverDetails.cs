using System;

namespace Shared
{
    [Serializable]
    public class RiverDetails
    {
        // The definition name of the river
        public string RiverDefName { get; set; } = string.Empty; // Default to an empty string, representing no river defined

        // The starting tile of the river
        public int TileA { get; set; } = 0; // Default to 0, representing an uninitialized state

        // The ending tile of the river
        public int TileB { get; set; } = 0; // Default to 0, representing an uninitialized state
    }
}