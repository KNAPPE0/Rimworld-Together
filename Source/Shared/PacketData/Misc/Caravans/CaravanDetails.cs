using System;

namespace Shared
{
    [Serializable]
    public class CaravanDetails
    {
        // Unique identifier for the caravan
        public int Id { get; set; } = 0; // Default value is 0, represents the caravan's ID

        // Tile on the map where the caravan is located
        public int Tile { get; set; } = 0; // Default value is 0, represents the map tile

        // Name of the caravan owner
        public string Owner { get; set; } = string.Empty; // Default to an empty string, represents the owner's name

        // Time since the caravan was last refreshed (in whatever units your system uses, e.g., hours)
        public double TimeSinceRefresh { get; set; } = 0.0; // Default to 0.0, represents time since last refresh
    }
}