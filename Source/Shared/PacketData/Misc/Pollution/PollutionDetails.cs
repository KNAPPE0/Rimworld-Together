using System;

namespace Shared
{
    [Serializable]
    public class PollutionDetails
    {
        // The tile where the pollution is located
        public int Tile { get; set; } = 0; // Default to 0, representing the map tile

        // The quantity of pollution on the tile
        public float Quantity { get; set; } = 0f; // Default to 0, representing no pollution
    }
}